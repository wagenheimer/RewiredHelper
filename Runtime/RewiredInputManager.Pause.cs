using System;
using System.Collections;

using Rewired;

using UnityEngine;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>Pause policy: app background, controller disconnect, Steam overlay and manual pauses.</summary>
    public partial class RewiredInputManager
    {
        // Auto values are resolved here, when the game runs, so a scene saved under one build target
        // still behaves correctly in another. Mobile: the OS already suspends the app, touch play has no
        // controller to lose, and "tap anywhere" to resume leaks taps into gameplay.
#if UNITY_ANDROID || UNITY_IOS
        private const bool AutoIsMobile = true;
#else
        private const bool AutoIsMobile = false;
#endif

        private const float ConsoleDisconnectGraceSeconds = 2f;

        private PauseController _pauseController;
        private static SystemUiTracker _systemUi;

        private PausePolicy Policy => PausePolicy.Resolve(
            OverridePlatformDefaults, PauseOnAppBackground, PauseOnControllerDisconnect, ResumeOnAnyInput, AutoIsMobile);

        /// <summary>The app-background mode in effect on this platform (platform best practice unless overridden).</summary>
        public AppBackgroundPauseMode EffectiveAppBackgroundMode => Policy.AppBackground;

        /// <summary>Whether a controller disconnect pauses on this platform.</summary>
        public bool ShouldPauseOnControllerDisconnect => Policy.PauseOnControllerDisconnect;

        /// <summary>Whether any input resumes a frozen game on this platform.</summary>
        public bool ShouldResumeOnAnyInput => Policy.ResumeOnAnyInput;

        private static Func<bool> _steamManagerProbe;
        private static bool _steamProbeResolved;

        /// <summary>
        /// True when Steam is up: <see cref="SteamIsInitialized"/> if the host set it, otherwise auto-detected from a
        /// <c>SteamManager.Initialized</c> found in any loaded assembly (the standard Steamworks.NET bootstrap).
        /// </summary>
        private static bool IsSteamReady
        {
            get
            {
                if (SteamIsInitialized) return true;
                if (!_steamProbeResolved)
                {
                    _steamProbeResolved = true;
                    _steamManagerProbe = BuildSteamManagerProbe();
                }
                return _steamManagerProbe != null && _steamManagerProbe();
            }
        }

        private static Func<bool> BuildSteamManagerProbe()
        {
            var type = FindType("SteamManager");
            if (type == null) return null;

            var property = type.GetProperty("Initialized", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (property != null && property.PropertyType == typeof(bool))
                return () => (bool)property.GetValue(null);

            var field = type.GetField("Initialized", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (field != null && field.FieldType == typeof(bool))
                return () => (bool)field.GetValue(null);

            return null;
        }

        private static SystemUiTracker SystemUi => _systemUi ??= new SystemUiTracker(() => Time.realtimeSinceStartup);

        /// <summary>
        /// Call while an ad, IAP sheet, permission prompt or share sheet is on screen (dispose it when it closes).
        /// Automatic pauses are vetoed meanwhile plus a short grace window afterwards; a lost Dispose expires by itself.
        /// Needs no other setup and works from any package: <c>using (RewiredInputManager.BeginSystemUi()) { ... }</c>.
        /// </summary>
        public static IDisposable BeginSystemUi() => new SystemUiScope(SystemUi);

        /// <summary>Fire-and-forget variant: vetoes automatic pauses for the next <paramref name="seconds"/> (e.g. right before showing an ad).</summary>
        public static void SuppressPauseFor(float seconds) => SystemUi.Suppress(seconds);

        /// <summary>True while a system UI scope (or suppression window) is active.</summary>
        public static bool IsSystemUiActive => SystemUi.IsActive;

        private sealed class SystemUiScope : IDisposable
        {
            private readonly SystemUiTracker _tracker;
            private int _token;

            public SystemUiScope(SystemUiTracker tracker)
            {
                _tracker = tracker;
                _token = tracker.Begin();
            }

            public void Dispose()
            {
                if (_token == 0) return;
                _tracker.End(_token);
                _token = 0;
            }
        }

        /// <summary>Raised whenever the paused state changes. Args: (isPaused, isFrozen). Silent pauses have isFrozen == false.</summary>
        public static event Action<bool, bool> OnPauseChanged;

        private PauseController Pause
        {
            get
            {
                if (_pauseController == null)
                {
                    _pauseController = new PauseController(() => Time.timeScale, scale => Time.timeScale = scale);
                    _pauseController.Changed += HandlePauseChanged;
                }
                return _pauseController;
            }
        }

        /// <summary>True while any pause reason is active (including silent ones).</summary>
        public bool IsPaused => _pauseController != null && _pauseController.IsPaused;

        /// <summary>True while the game is frozen (Time.timeScale == 0 owned by this manager, overlay visible).</summary>
        public bool IsFrozen => _pauseController != null && _pauseController.IsFrozen;

        /// <summary>Freezes the game. Never vetoed by <see cref="IPauseGate"/>. Safe to wire to a UnityEvent.</summary>
        public void RequestPause() => Pause.Pause(PauseReason.Manual, freeze: true);

        /// <summary>Releases every pause reason. Wire this to the Resume button of the pause screen.</summary>
        public void Resume() => _pauseController?.ResumeAll();

        private void HandlePauseChanged(bool isPaused, bool isFrozen)
        {
            if (GamePaused != null) GamePaused.SetActive(isFrozen);
            OnPauseChanged?.Invoke(isPaused, isFrozen);
        }

        private bool TryAutoPause(PauseReason reason, bool freeze)
        {
            if (IsSystemUiActive || !_pauseGate.CanPause(reason)) return false;
            Pause.Pause(reason, freeze);
            return true;
        }

        private void UpdatePauseState()
        {
#if WAGENHEIMER_STEAMWORKS
            if (PauseOnSteamOverlay && IsSteamReady)
            {
                if (IsSteamOverlayActive && !Pause.Has(PauseReason.SteamOverlay))
                    TryAutoPause(PauseReason.SteamOverlay, freeze: true);
                else if (!IsSteamOverlayActive && Pause.Has(PauseReason.SteamOverlay))
                    Pause.Resume(PauseReason.SteamOverlay);
            }
#endif

            if (IsFrozen && ShouldResumeOnAnyInput && anyButton)
                _pauseController.ResumeFrozen();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            var mode = EffectiveAppBackgroundMode;
            if (mode == AppBackgroundPauseMode.Off) return;

            if (pauseStatus)
            {
                TryAutoPause(PauseReason.AppBackground, freeze: mode == AppBackgroundPauseMode.Overlay);
            }
            else if (mode == AppBackgroundPauseMode.Silent)
            {
                // A silent pause has nothing for the player to dismiss, so release it on return.
                _pauseController?.Resume(PauseReason.AppBackground);
            }
        }

        private void OnJoystickDisconnected(Controller controller)
        {
            if (!ShouldPauseOnControllerDisconnect || IsUsingTouch) return;

            // A pad the player wasn't using (keyboard/mouse is the active device) is not worth a pause.
            if (LastActiveController != null && LastActiveController != controller) return;

            if (IsConsole)
                StartCoroutine(DelayedControllerDisconnectAction());
            else
                TryAutoPause(PauseReason.ControllerDisconnected, freeze: true);
        }

        private IEnumerator DelayedControllerDisconnectAction()
        {
            yield return new WaitForSecondsRealtime(ConsoleDisconnectGraceSeconds);

            if (_controllerWasDisconnected && Player != null && Player.controllers.GetLastActiveController() == null)
                TryAutoPause(PauseReason.ControllerDisconnected, freeze: true);
        }

        private void ReleaseControllerDisconnectPause() => _pauseController?.Resume(PauseReason.ControllerDisconnected);
    }
}
