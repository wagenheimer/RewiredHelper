using System;
using System.Collections;

using Rewired;

#if WAGENHEIMER_STEAM_DECK_DETECTION && !UNITY_ANDROID && !UNITY_IOS
using Steamworks;
#endif

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
        private static bool _warnedAboutMissingPauseScreen;
        private static SystemUiTracker _systemUi;

        private const int AndroidUiModeTypeTelevision = 4;
        private static bool? _isTelevision;

        /// <summary>True when this build targets a mobile platform (Android/iOS), decided by the build's compile-time platform.</summary>
        public static bool IsMobileBuild => AutoIsMobile;

        /// <summary>True on Android TV / Fire TV (detected once at runtime through UiModeManager).</summary>
        public static bool IsTelevisionDevice
        {
            get
            {
                if (!_isTelevision.HasValue) _isTelevision = DetectTelevision();
                return _isTelevision.Value;
            }
        }

        /// <summary>True when the mobile pause policy applies on this device: a mobile build that is not a television.</summary>
        public static bool IsMobilePolicyActive => PausePolicy.IsMobileDevice(AutoIsMobile, IsTelevisionDevice);

        private static bool DetectTelevision()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var uiMode = activity.Call<AndroidJavaObject>("getSystemService", "uimode"))
                    return uiMode.Call<int>("getCurrentModeType") == AndroidUiModeTypeTelevision;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RewiredHelper] Could not detect Android TV, assuming a handheld: " + ex.Message);
                return false;
            }
#else
            return false;
#endif
        }

        private PausePolicy Policy => PausePolicy.Resolve(
            OverridePlatformDefaults, PauseOnAppBackground, PauseOnControllerDisconnect, ResumeOnAnyInput, IsMobilePolicyActive);

        /// <summary>The app-background mode in effect on this platform (platform best practice unless overridden).</summary>
        public AppBackgroundPauseMode EffectiveAppBackgroundMode => Policy.AppBackground;

        /// <summary>Whether a controller disconnect pauses on this platform.</summary>
        public bool ShouldPauseOnControllerDisconnect => Policy.PauseOnControllerDisconnect;

        /// <summary>Whether any input resumes a frozen game on this platform.</summary>
        public bool ShouldResumeOnAnyInput => Policy.ResumeOnAnyInput;

        private const float SteamProbeIntervalSeconds = 0.5f;

        private static Func<bool> _steamManagerProbe;
        private static bool _steamProbeResolved;
        private static float _nextSteamProbeTime;
        private static bool _lastSteamProbeResult;

        /// <summary>
        /// True when Steam is up: <see cref="SteamIsInitialized"/> if the host set it, otherwise auto-detected from the
        /// standard Steamworks.NET <c>SteamManager</c> in any loaded assembly. Polled at most every half second, and it
        /// never touches <c>SteamManager.Instance</c> (whose getter would spawn a SteamManager GameObject).
        /// </summary>
        private static bool IsSteamReady
        {
            get
            {
                if (SteamIsInitialized) return true;
                if (Time.realtimeSinceStartup < _nextSteamProbeTime) return _lastSteamProbeResult;

                if (!_steamProbeResolved)
                {
                    _steamProbeResolved = true;
                    _steamManagerProbe = BuildSteamManagerProbe();
                }

                _nextSteamProbeTime = Time.realtimeSinceStartup + SteamProbeIntervalSeconds;
                _lastSteamProbeResult = _steamManagerProbe != null && _steamManagerProbe();
                return _lastSteamProbeResult;
            }
        }

        private static Func<bool> BuildSteamManagerProbe()
        {
            var type = FindType("SteamManager");
            if (type == null) return null;

            const System.Reflection.BindingFlags staticAny =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;

            var initialized = type.GetProperty("Initialized", staticAny);
            if (initialized == null || initialized.PropertyType != typeof(bool)) return null;

            // Steamworks.NET's Initialized reads Instance.m_bInitialized and Instance creates the GameObject when
            // missing, so only ask once the singleton already exists (s_instance is its backing field).
            var instanceField = type.GetField("s_instance", staticAny);
            if (instanceField == null)
                return () => (bool)initialized.GetValue(null);

            return () => (instanceField.GetValue(null) as UnityEngine.Object) != null && (bool)initialized.GetValue(null);
        }

        private static bool? _isSteamDeckCache;

        /// <summary>
        /// True when running on Steam Deck. Only meaningful once Steam is up (see <see cref="IsSteamReady"/>); returns
        /// false (and is not cached) until then, so it keeps re-checking rather than latching a false negative during
        /// startup. Compiled directly against <c>Steamworks.SteamUtils.IsRunningOnSteamHardware()</c> when the
        /// installed Steamworks.NET is new enough (see <see cref="SupportsSteamDeckDetection"/>); on an older one
        /// this is always false instead of failing to compile — the Setup Audit flags that case.
        /// </summary>
        public static bool IsSteamDeck
        {
            get
            {
                if (_isSteamDeckCache.HasValue) return _isSteamDeckCache.Value;
                if (!IsSteamReady) return false;

                _isSteamDeckCache = DetectSteamDeck();
                return _isSteamDeckCache.Value;
            }
        }

        /// <summary>
        /// True when this package was compiled against a Steamworks.NET version new enough to detect Steam Deck
        /// hardware (<c>SteamUtils.IsRunningOnSteamHardware</c>, verified present in 2025.165.0 — the API that
        /// replaced the older, now-removed <c>IsSteamRunningOnSteamDeck</c>). False on an older install: update
        /// Steamworks.NET (<c>https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net</c>)
        /// to enable Deck-specific suspend/resume handling.
        /// </summary>
        public const bool SupportsSteamDeckDetection =
#if WAGENHEIMER_STEAM_DECK_DETECTION && !UNITY_ANDROID && !UNITY_IOS
            true;
#else
            false;
#endif

        private static bool DetectSteamDeck()
        {
#if WAGENHEIMER_STEAM_DECK_DETECTION && !UNITY_ANDROID && !UNITY_IOS
            try { return SteamUtils.IsRunningOnSteamHardware() == ESteamHardwareType.k_ESteamHardwareTypeSteamDeck; }
            catch (Exception ex)
            {
                Debug.LogWarning("[RewiredHelper] Could not query IsRunningOnSteamHardware: " + ex.Message);
                return false;
            }
#else
            return false;
#endif
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
            if (GamePaused != null)
                GamePaused.SetActive(isFrozen);
            else if (isFrozen)
                WarnAboutMissingPauseScreen();

            OnPauseChanged?.Invoke(isPaused, isFrozen);
        }

        /// <summary>The game froze but there is nothing on screen to tell the player: this is a setup mistake the audit fails on.</summary>
        private static void WarnAboutMissingPauseScreen()
        {
            if (_warnedAboutMissingPauseScreen) return;

            _warnedAboutMissingPauseScreen = true;
            Debug.LogWarning("[RewiredHelper] The game froze but no Game Paused screen is assigned on the RewiredInputManager, so " +
                             "players see a frozen game. Create one (Tools > Wagenheimer > Rewired Helper > Dashboard > Setup Audit).");
        }

        private bool TryAutoPause(PauseReason reason, bool freeze)
        {
            if (IsSystemUiActive || !_pauseGate.CanPause(reason)) return false;
            Pause.Pause(reason, freeze);
            return true;
        }

        private void UpdatePauseState()
        {
#if WAGENHEIMER_STEAMWORKS && !UNITY_ANDROID && !UNITY_IOS
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
