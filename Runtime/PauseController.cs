using System;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>Why the game is (or was asked to be) paused.</summary>
    public enum PauseReason
    {
        /// <summary>App went to the background (<c>OnApplicationPause</c>).</summary>
        AppBackground = 0,
        /// <summary>The active joystick was disconnected.</summary>
        ControllerDisconnected = 1,
        /// <summary>The Steam overlay opened.</summary>
        SteamOverlay = 2,
        /// <summary>Requested explicitly through <c>RewiredInputManager.RequestPause()</c>.</summary>
        Manual = 3
    }

    /// <summary>What the manager does when the app goes to the background.</summary>
    public enum AppBackgroundPauseMode
    {
        /// <summary>Resolved per platform when the game runs: <see cref="Silent"/> on Android/iOS, <see cref="Overlay"/> elsewhere.</summary>
        Auto = 0,
        /// <summary>Do nothing.</summary>
        Off = 1,
        /// <summary>Raise <c>OnPauseChanged</c> only: no overlay, no <c>Time.timeScale</c> change. Recommended on mobile, where the OS already suspends the app.</summary>
        Silent = 2,
        /// <summary>Freeze <c>Time.timeScale</c> and show the pause overlay (classic desktop/console behavior).</summary>
        Overlay = 3
    }

    /// <summary>On/Off switch whose <see cref="Auto"/> value is resolved per platform when the game runs, never baked into the scene.</summary>
    public enum AutoToggle
    {
        Auto = 0,
        On = 1,
        Off = 2
    }

    /// <summary>
    /// Tracks the set of active pause reasons and owns <c>Time.timeScale</c> only while it is the one
    /// that froze it. Several reasons can be active at once without releasing each other, and if the host
    /// changes the time scale while paused, resuming never overwrites the host's value.
    /// Pure logic (no Unity/Rewired calls) so it can be unit-tested.
    /// </summary>
    public sealed class PauseController
    {
        private const float FrozenScale = 0f;

        private readonly Func<float> _getTimeScale;
        private readonly Action<float> _setTimeScale;

        private int _reasons;
        private int _frozenReasons;
        private float _savedScale = 1f;
        private bool _ownsTimeScale;

        public PauseController(Func<float> getTimeScale, Action<float> setTimeScale)
        {
            _getTimeScale = getTimeScale ?? throw new ArgumentNullException(nameof(getTimeScale));
            _setTimeScale = setTimeScale ?? throw new ArgumentNullException(nameof(setTimeScale));
        }

        /// <summary>Raised when <see cref="IsPaused"/> or <see cref="IsFrozen"/> flips. Args: (isPaused, isFrozen).</summary>
        public event Action<bool, bool> Changed;

        /// <summary>Any reason is active (silent or frozen).</summary>
        public bool IsPaused => _reasons != 0;

        /// <summary>At least one active reason froze the game (time scale 0 / overlay visible).</summary>
        public bool IsFrozen => _frozenReasons != 0;

        public bool Has(PauseReason reason) => (_reasons & Bit(reason)) != 0;

        public bool HasFrozen(PauseReason reason) => (_frozenReasons & Bit(reason)) != 0;

        /// <summary>Activates <paramref name="reason"/>. When <paramref name="freeze"/> is false it is announced but leaves time untouched.</summary>
        public void Pause(PauseReason reason, bool freeze)
        {
            bool wasPaused = IsPaused;
            bool wasFrozen = IsFrozen;

            _reasons |= Bit(reason);
            if (freeze) _frozenReasons |= Bit(reason);

            if (!wasFrozen && IsFrozen)
            {
                _savedScale = _getTimeScale();

                // If the host already froze time (its own pause menu), it owns the scale: leave it alone.
                _ownsTimeScale = Math.Abs(_savedScale - FrozenScale) > float.Epsilon;
                if (_ownsTimeScale) _setTimeScale(FrozenScale);
            }

            RaiseIfChanged(wasPaused, wasFrozen);
        }

        /// <summary>Releases one reason. Returns true if it was active.</summary>
        public bool Resume(PauseReason reason)
        {
            if (!Has(reason)) return false;
            Release(Bit(reason));
            return true;
        }

        /// <summary>Releases every active reason.</summary>
        public void ResumeAll()
        {
            if (!IsPaused) return;
            Release(_reasons);
        }

        /// <summary>Releases every frozen reason but keeps silent ones (used by "tap to resume").</summary>
        public void ResumeFrozen()
        {
            if (!IsFrozen) return;
            Release(_frozenReasons);
        }

        private void Release(int mask)
        {
            bool wasPaused = IsPaused;
            bool wasFrozen = IsFrozen;

            _reasons &= ~mask;
            _frozenReasons &= ~mask;

            if (wasFrozen && !IsFrozen && _ownsTimeScale)
            {
                // Only restore if the time scale is still the one we set. If the host changed it
                // while we were paused (slow-mo, its own pause menu), the host wins.
                if (Math.Abs(_getTimeScale() - FrozenScale) < float.Epsilon)
                    _setTimeScale(_savedScale);
                _ownsTimeScale = false;
            }

            RaiseIfChanged(wasPaused, wasFrozen);
        }

        private void RaiseIfChanged(bool wasPaused, bool wasFrozen)
        {
            if (wasPaused != IsPaused || wasFrozen != IsFrozen)
                Changed?.Invoke(IsPaused, IsFrozen);
        }

        private static int Bit(PauseReason reason) => 1 << (int)reason;
    }
}

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Ref-counted "a system UI (ad, IAP sheet, permission prompt, share sheet) is on screen" flag, with a
    /// grace window after it ends (the OS focus events arrive around open/close) and a failsafe so a lost
    /// End() can never suppress pauses forever. While active, automatic pauses are vetoed.
    /// Pure logic (injected clock) so it can be unit-tested.
    /// </summary>
    public sealed class SystemUiTracker
    {
        public const float DefaultGraceSeconds = 1.5f;
        public const float MaxScopeSeconds = 180f;

        private readonly Func<float> _now;
        private readonly System.Collections.Generic.Dictionary<int, float> _open = new System.Collections.Generic.Dictionary<int, float>();
        private int _nextToken = 1;
        private float _graceUntil;

        public SystemUiTracker(Func<float> now) => _now = now ?? throw new ArgumentNullException(nameof(now));

        /// <summary>Marks a system UI as open. Returns a token to pass to <see cref="End"/>.</summary>
        public int Begin()
        {
            int token = _nextToken++;
            _open[token] = _now();
            return token;
        }

        /// <summary>Marks it closed and starts the grace window. Unknown/duplicate tokens are ignored.</summary>
        public void End(int token, float graceSeconds = DefaultGraceSeconds)
        {
            if (_open.Remove(token))
                Suppress(graceSeconds);
        }

        /// <summary>Fire-and-forget: suppress automatic pauses for the next <paramref name="seconds"/>.</summary>
        public void Suppress(float seconds) => _graceUntil = Math.Max(_graceUntil, _now() + Math.Max(0f, seconds));

        public bool IsActive
        {
            get
            {
                float now = _now();
                PruneExpired(now);
                return _open.Count > 0 || now < _graceUntil;
            }
        }

        private void PruneExpired(float now)
        {
            if (_open.Count == 0) return;

            System.Collections.Generic.List<int> expired = null;
            foreach (var pair in _open)
            {
                if (now - pair.Value <= MaxScopeSeconds) continue;
                (expired ??= new System.Collections.Generic.List<int>()).Add(pair.Key);
            }
            if (expired != null)
                foreach (var token in expired) _open.Remove(token);
        }
    }
}
