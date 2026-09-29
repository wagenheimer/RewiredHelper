using System;
using System.Collections.Generic;
using System.Reflection;

using Rewired;

#if WAGENHEIMER_STEAMWORKS
using Steamworks;
#endif

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Central input-type manager built on top of Rewired. Tracks whether the player is
    /// currently using mouse, touch, or a controller, drives cursor visibility accordingly,
    /// and routes Escape/Return to the right UI target.
    ///
    /// Game-specific behavior (UI-blocked state, modal stack, controller-help gating,
    /// cutscene/tutorial pause) is not hard-coded — plug it in via <see cref="Configure"/>
    /// using the optional <see cref="IUiBlocker"/>, <see cref="IModalStackProvider"/> and
    /// <see cref="IControllerHelpGate"/> interfaces. None of them are required.
    /// </summary>
    [MovedFrom(true, sourceClassName: "RewiredHelper")]
    public partial class RewiredInputManager : MonoBehaviour
    {
        #region Singleton Pattern
        public static RewiredInputManager Instance { get; protected set; }
        #endregion

        #region Public Properties
        [Tooltip("Reference to the Rewired player")]
        public Rewired.Player Player
        {
            get
            {
                if (_player == null && ReInput.isReady)
                {
                    _player = ReInput.players.GetPlayer(0);
                }
                return _player;
            }
            private set => _player = value;
        }
        private Rewired.Player _player;

        [Tooltip("Reference to the custom game cursor")]
        public Image GameCursor;

        [Tooltip("Your pause screen, shown while the game is frozen (not for silent pauses). Required on desktop/console/TV, where the game can freeze: the Setup Audit fails without it.")]
        public GameObject GamePaused;

        [Tooltip("If true, the Steam overlay pauses the game automatically")]
        public bool PauseOnSteamOverlay = true;

        [Tooltip("Off (recommended): pause behavior is chosen automatically for the build platform (mobile: silent, no tap-to-resume; desktop/console: overlay, resume on input). Turn on only to override the three settings below.")]
        public bool OverridePlatformDefaults;

        [Tooltip("What happens when the app goes to the background. Auto = Silent on Android/iOS, Overlay elsewhere. Off = nothing; Silent = raises OnPauseChanged only (no overlay, Time.timeScale untouched - the OS already suspends the app); Overlay = freezes time and shows GamePaused.")]
        public AppBackgroundPauseMode PauseOnAppBackground = AppBackgroundPauseMode.Auto;

        [Tooltip("Pause when the active controller disconnects. Auto = off on Android/iOS (Bluetooth pads and remotes come and go, touch play does not need it), on elsewhere.")]
        public AutoToggle PauseOnControllerDisconnect = AutoToggle.Auto;

        [Tooltip("While frozen, any tap/click/Back press resumes. Auto = off on Android/iOS (taps leak into gameplay; use a Resume button wired to Resume()), on elsewhere.")]
        public AutoToggle ResumeOnAnyInput = AutoToggle.Auto;

        /// <summary>Indicates whether the custom cursor can be displayed.</summary>
        public static bool CanShowCustomCursor { get; private set; }

        [Tooltip("Enables/disables the custom cursor for standalone builds. Can also be set at runtime by host save data.")]
        public bool CustomCursorEnabled;

        [Tooltip("Texture used by the custom cursor when Custom Cursor Enabled is checked. Can be assigned here or at runtime.")]
        public Texture2D CursorTexture;

        [Tooltip("If enabled, joystick/keyboard UI confirmation (UISubmit / Return / Button A) automatically invokes PointerDown / PointerUp on the selected GameObject, fixing audio listeners (like EventSounds / PointerDown SFX) that only listen to pointer clicks.")]
        public bool AutoBridgeSubmitToPointerDown = true;

        [SerializeField]
        private bool alreadyShowedControllerHelp;

        public bool IsSteamOverlayActive = false;

        /// <summary>
        /// Optional: Steam readiness is auto-detected from a <c>SteamManager.Initialized</c> in the project.
        /// Set this to true yourself only if your Steam bootstrap uses a different type.
        /// Only consulted when Steamworks.NET is installed (<c>WAGENHEIMER_STEAMWORKS</c>).
        /// </summary>
        public static bool SteamIsInitialized;

        [Tooltip("Current mouse position in the Rewired system")]
        public static Vector3 RewiredMousePosition { get; private set; }

        public static event Action<bool> OnInputTypeChanged;

        /// <summary>Triggered when the input/controller type changes, for components that need to re-specialize UI (e.g. localization).</summary>
        public static event Action OnInputSpecializationChanged;

        [Tooltip("Seconds since the last mouse movement or touch.")]
        public static float SecondsSinceLastMouseOrTouchMove { get; private set; }

        public static bool IsUsingTouch => Instance != null && Instance._isUsingTouch;

        /// <summary>Invoked instead of opening a controller help dialog directly — the game decides what to show.</summary>
        public UnityEvent OnShowControllerHelp;

        [Tooltip("If enabled, the manager will automatically configure itself on Start, using default providers.")]
        public bool AutoConfigureOnStart = true;

        [Tooltip("Scene names where the first-time controller-help prompt must NEVER appear. " +
                 "Matching is by active scene name, case-insensitive. Pressing a gamepad button in " +
                 "these scenes does not consume the prompt - it will still show the first time a " +
                 "gamepad button is pressed in an allowed scene.")]
        public List<string> ControllerHelpBlockedScenes = new List<string>();

        [Tooltip("If enabled, Custom Controllers are placed last in the Rewired glyph selector's controller type order. " +
                 "Prevents binds on auxiliary/orphaned Custom Controllers (e.g. AndroidRemote) from overriding mouse/keyboard glyphs on desktop. " +
                 "When a Custom Controller is the last active controller (e.g. using the remote on Android TV), it still takes priority.")]
        public bool ForceGlyphCustomControllerLast = true;

        [Tooltip("If AutoConfigureOnStart is enabled, this controls whether the default ModalDialogStack provider is used.")]
        public bool UseDefaultModalStack = true;

        /// <summary>Indicates whether Configure() was successfully called.</summary>
        public bool IsConfigured { get; private set; }
        #endregion

        #region Private Fields
        private float _lastMouseOrTouchMoveTime;
        private Controller lastActiveController;
        private bool _isUsingTouch;
        private bool _previousInputState;
        private bool lastInputWasTouch = false;
        private bool previousIsUsingTouch;
        private ControllerType _lastControllerType;

        private bool _controllerWasDisconnected = false;
        private ControllerType _lastKnownControllerType = ControllerType.Joystick;
        private float _controllerDisconnectedTime = 0f;
        private const float CONTROLLER_RECONNECT_DELAY = 0.5f;

        private bool IsConsole => Application.platform == RuntimePlatform.Switch ||
                                 Application.platform == RuntimePlatform.PS4 ||
                                 Application.platform == RuntimePlatform.PS5 ||
                                 Application.platform == RuntimePlatform.XboxOne;

        private static readonly HashSet<InputVisibilityController> _visibilityControllers = new();

        private IUiBlocker _uiBlocker = NullUiBlocker.Instance;
        private IModalStackProvider _modalStack = NullModalStackProvider.Instance;
        private IControllerHelpGate _controllerHelpGate = NullControllerHelpGate.Instance;
        private IPauseGate _pauseGate = NullPauseGate.Instance;
        #endregion

        #region Properties
        [Tooltip("Tracks the last active input device")]
        public Controller LastActiveController
        {
            get => lastActiveController;
            private set
            {
                if (lastActiveController != value)
                {
                    lastActiveController = value;
                    OnLastActiveControllerChanged();
                }
            }
        }

        /// <summary>
        /// Legacy alias for <see cref="LastActiveController"/>, kept so consumers ported from the
        /// original RewiredHelper keep compiling.
        /// </summary>
        public Controller UltimoControleAtivo => LastActiveController;

        public static bool anyButton => Instance != null && Instance.Player != null &&
                                        (Instance.Player.GetButtonDown("MouseLeftButton") ||
                                         Instance.Player.GetButtonDown("BackButton") ||
                                         (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began));

        public static bool anyButtonNow => Instance != null && Instance.Player != null &&
                                           (Instance.Player.GetButton("MouseLeftButton") ||
                                            Instance.Player.GetButton("BackButton") ||
                                            (Input.touchCount > 0));

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only test override. When set, <see cref="CurrentControllerType"/> returns this value
        /// regardless of what is actually plugged in, so gamepad-only behavior (e.g. the on-screen keyboard
        /// popping up on a TMP_InputField) can be previewed in Play Mode without a physical controller.
        /// Never compiled into a build; set from the Rewired Helper dashboard's Advanced section.
        /// </summary>
        public static ControllerType? DebugForceControllerType;
#endif

        public ControllerType CurrentControllerType
        {
            get
            {
#if UNITY_EDITOR
                if (DebugForceControllerType.HasValue)
                    return DebugForceControllerType.Value;
#endif
                if (Instance == null)
                    return ControllerType.Joystick;

                if (IsUsingTouch)
                    return ControllerType.Custom;

                return LastActiveController?.type ?? _lastKnownControllerType;
            }
        }
        #endregion

#if WAGENHEIMER_STEAMWORKS
        protected Callback<GameOverlayActivated_t> m_GameOverlayActivated;
#endif

        private static IUiBlocker _pendingUiBlocker;
        private static IModalStackProvider _pendingModalStack;
        private static IControllerHelpGate _pendingControllerHelpGate;
        private static IPauseGate _pendingPauseGate;
        private static bool _hasPendingConfig;

        /// <summary>
        /// Statically configures the integration hooks. If <see cref="Instance"/> is already active,
        /// applies immediately. Otherwise, stores configuration to apply as soon as the instance awakes.
        /// </summary>
        public static void SetGlobalConfiguration(IUiBlocker uiBlocker = null, IModalStackProvider modalStack = null,
            IControllerHelpGate controllerHelpGate = null, IPauseGate pauseGate = null)
        {
            _pendingUiBlocker = uiBlocker;
            _pendingModalStack = modalStack;
            _pendingControllerHelpGate = controllerHelpGate;
            _pendingPauseGate = pauseGate;
            _hasPendingConfig = true;

            if (Instance != null)
            {
                Instance.Configure(uiBlocker, modalStack, controllerHelpGate, pauseGate);
            }
        }

        #region Public API
        /// <summary>
        /// Wires up the optional integration hooks. Call this once after the manager exists
        /// (e.g. right after Awake/Start of your own bootstrap). Any argument left null keeps
        /// the harmless default (never blocked, no modals, help always allowed).
        /// </summary>
        public void Configure(IUiBlocker uiBlocker = null, IModalStackProvider modalStack = null,
            IControllerHelpGate controllerHelpGate = null, IPauseGate pauseGate = null)
        {
            _uiBlocker = uiBlocker ?? NullUiBlocker.Instance;
            _modalStack = modalStack ?? NullModalStackProvider.Instance;
            _controllerHelpGate = controllerHelpGate ?? NullControllerHelpGate.Instance;
            _pauseGate = pauseGate ?? NullPauseGate.Instance;
            IsConfigured = true;
        }
        #endregion

        #region Unity Lifecycle Methods
        private void Awake()
        {
            InitializeSingleton();
            if (_hasPendingConfig)
            {
                Configure(_pendingUiBlocker, _pendingModalStack, _pendingControllerHelpGate, _pendingPauseGate);
            }
        }

        private void Start()
        {
            InitializePlayer();

            if (IsConsole)
            {
                _lastKnownControllerType = ControllerType.Joystick;
            }
            else
            {
                // Default to Keyboard/PC on startup so any popup showing before first input
                // shows keyboard glyphs instead of joystick glyphs.
                _lastKnownControllerType = ControllerType.Keyboard;
                _lastInputWasPC = true;
                if (ReInput.isReady)
                {
                    var kb = ReInput.controllers.GetController(ControllerType.Keyboard, 0);
                    if (kb != null) lastActiveController = kb; // assign backing field directly to avoid event before register
                }

                // The scene's GameCursor Image may be enabled by default. HandleControllerType() has no
                // Keyboard case, so nothing would hide it on desktop until the first mouse/joystick
                // input — making the OS/hardware cursor and the joystick cursor show simultaneously
                // on startup. Hide it here: it only comes back once real joystick input is detected
                // (HandleJoystickOrCustomController).
                if (!IsConsole && GameCursor != null) GameCursor.enabled = false;
            }

            _lastMouseOrTouchMoveTime = Time.time;

#if WAGENHEIMER_STEAMWORKS
            if (IsSteamReady) m_GameOverlayActivated = Callback<GameOverlayActivated_t>.Create(OnGameOverlayActivated);
#endif

            // Auto-configure using default providers if enabled and not already configured via code
            if (AutoConfigureOnStart && !IsConfigured)
            {
                Configure(
                    null,
                    UseDefaultModalStack ? new UI.DefaultModalStackProvider() : null,
                    null
                );
            }
        }

#if WAGENHEIMER_STEAMWORKS
        private void OnGameOverlayActivated(GameOverlayActivated_t pCallback)
        {
            IsSteamOverlayActive = pCallback.m_bActive != 0;
        }
#endif

        private bool _isDelegateRegistered = false;
        private bool _glyphSelectorConfigured = false;

        private void Update()
        {
            if (Player == null && ReInput.isReady)
            {
                InitializePlayer();
            }

            if (ReInput.isReady && !_isDelegateRegistered)
            {
                ReInput.controllers.AddLastActiveControllerChangedDelegate(OnLastActiveControllerChangedCallback);
                _isDelegateRegistered = true;
            }

            if (!_glyphSelectorConfigured)
            {
                ConfigureGlyphControllerTypeOrder();
            }

            SecondsSinceLastMouseOrTouchMove = Time.time - _lastMouseOrTouchMoveTime;

            UpdateCursorPosition();
            HandleInputSystem();
            HandleEscapeButtons();

            if (_controllerWasDisconnected && Player != null && Time.time - _controllerDisconnectedTime > CONTROLLER_RECONNECT_DELAY)
                CheckForControllerReconnection();

            UpdatePauseState();
            HandleOnScreenKeyboard();
        }



        private void OnLastActiveControllerChangedCallback(Controller controller)
        {
            if (controller == null) return;

            if (controller.type == ControllerType.Joystick)
            {
                _lastInputWasPC = false;
                LastActiveController = controller;
                _lastKnownControllerType = ControllerType.Joystick;
                UpdateUIForInputType();
                Debug.Log($"[RewiredHelper] Switched to Joystick: {controller.name}");
            }
            else if (controller.type == ControllerType.Keyboard || controller.type == ControllerType.Mouse)
            {
                _lastInputWasPC = true;
                var keyboardController = ReInput.controllers.GetController(ControllerType.Keyboard, 0);
                if (keyboardController != null)
                {
                    LastActiveController = keyboardController;
                }
                _lastKnownControllerType = ControllerType.Keyboard;
                UpdateUIForInputType();
                Debug.Log($"[RewiredHelper] Switched to PC input: {controller.name}");
            }
        }

        private void CheckForControllerReconnection()
        {
            if (Player == null) return;
            var currentController = Player.controllers.GetLastActiveController();
            if (currentController != null && currentController.type == ControllerType.Joystick)
            {
                _controllerWasDisconnected = false;
                ReleaseControllerDisconnectPause();
                LastActiveController = currentController;
                _lastKnownControllerType = currentController.type;
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (Application.platform == RuntimePlatform.Switch && !hasFocus)
                StartCoroutine(HandleSwitchModeChange());

            // Steam Deck's suspend/resume (lid close, power button, Steam's own "Suspend Game") is delivered here
            // on a Standalone build, not through OnApplicationPause (which Unity does not reliably call outside
            // mobile/console) — reuse the exact same pause/resume path so Deck suspend behaves like any other
            // app-background pause (Valve's "seamless suspend" Deck Verified requirement).
            if (IsSteamDeck) OnApplicationPause(!hasFocus);
        }

        private System.Collections.IEnumerator HandleSwitchModeChange()
        {
            yield return new WaitForSeconds(0.5f);

            var currentController = Player.controllers.GetLastActiveController();
            if (currentController != null)
                _lastKnownControllerType = currentController.type;

            OnLastActiveControllerChanged();
            UpdateUIForInputType();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (_pauseController != null)
            {
                // Never leave Time.timeScale frozen with no owner (a scene load would strand it).
                _pauseController.ResumeAll();
                _pauseController.Changed -= HandlePauseChanged;
            }
            if (_isDelegateRegistered && ReInput.isReady)
            {
                ReInput.controllers.RemoveLastActiveControllerChangedDelegate(OnLastActiveControllerChangedCallback);
            }
            if (Instance == this) Instance = null;
        }
        #endregion

        #region Initialization Methods
        private void InitializeSingleton()
        {
            if (Instance == null)
            {
                Instance = this;
                SubscribeToEvents();
            }
            else if (Instance != this)
            {
                Destroy(this);
            }
        }

        private void InitializePlayer()
        {
            if (ReInput.isReady)
            {
                Player = ReInput.players.GetPlayer(0);
            }
        }

        private void SubscribeToEvents()
        {
            ReInput.ControllerDisconnectedEvent += OnControllerDisconnected;
            ReInput.ControllerConnectedEvent += OnControllerConnected;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void UnsubscribeFromEvents()
        {
            ReInput.ControllerDisconnectedEvent -= OnControllerDisconnected;
            ReInput.ControllerConnectedEvent -= OnControllerConnected;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Allow the glyph controller type order to be re-applied to glyph helpers
            // that were instantiated with the newly loaded scene.
            _glyphSelectorConfigured = false;
        }
        #endregion

        #region Input Handling Methods
        private void UpdateCursorPosition()
        {
            if (GameCursor == null) return;

            // Screen Space - Overlay canvases render 1:1 with screen pixels and must be converted
            // with a null camera; passing Camera.main (or any camera) here skews the result whenever
            // that camera's projection doesn't happen to match the canvas 1:1 (e.g. a distinct
            // gameplay/UI camera setup), producing an out-of-bounds RewiredMousePosition that never
            // hits anything when raycast against world colliders.
            var canvas = GameCursor.canvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RewiredMousePosition = RectTransformUtility.WorldToScreenPoint(camera, GameCursor.transform.position);
        }

        private const float MOUSE_MOVEMENT_TIME_THRESHOLD = 0.01f;
        private const float MOUSE_MOVEMENT_INPUT_THRESHOLD = 0.1f;

        private bool _lastInputWasPC = true;

        private void HandleInputSystem()
        {
            if (ReInput.touch == null) return;
            if (Player == null) return;

            _isUsingTouch = HandleTouchInput();

            if (_isUsingTouch)
            {
                lastInputWasTouch = true;
                _lastMouseOrTouchMoveTime = Time.time;
            }
            else
            {
                if (Mathf.Abs(Input.GetAxis("Mouse X")) > MOUSE_MOVEMENT_TIME_THRESHOLD || Mathf.Abs(Input.GetAxis("Mouse Y")) > MOUSE_MOVEMENT_TIME_THRESHOLD)
                    _lastMouseOrTouchMoveTime = Time.time;

                if (Mathf.Abs(Input.GetAxis("Mouse X")) > MOUSE_MOVEMENT_INPUT_THRESHOLD || Mathf.Abs(Input.GetAxis("Mouse Y")) > MOUSE_MOVEMENT_INPUT_THRESHOLD)
                    lastInputWasTouch = false;
            }

            _isUsingTouch = lastInputWasTouch;

            if (_isUsingTouch)
            {
                if (!IsConsole) LastActiveController = null;
                UpdateUIForInputType();
            }
            else
            {
                // Ensure there is always a default controller set.
                if (LastActiveController == null && ReInput.isReady)
                {
                    LastActiveController = ReInput.controllers.GetController(ControllerType.Keyboard, 0);
                }

                // Hybrid polling: if the mouse is moving/clicked while a joystick is the tracked
                // controller, switch back to Keyboard. The Rewired delegate only fires for button
                // presses, not continuous mouse movement, so we need this as a fallback.
                if (!IsConsole && LastActiveController != null && LastActiveController.type == ControllerType.Joystick)
                {
                    bool mouseMoving = Mathf.Abs(Input.GetAxis("Mouse X")) > MOUSE_MOVEMENT_INPUT_THRESHOLD ||
                                      Mathf.Abs(Input.GetAxis("Mouse Y")) > MOUSE_MOVEMENT_INPUT_THRESHOLD;
                    bool mouseClicked = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);

                    if (mouseMoving || mouseClicked)
                    {
                        _lastInputWasPC = true;
                        var kb = ReInput.controllers.GetController(ControllerType.Keyboard, 0);
                        if (kb != null) LastActiveController = kb;
                    }
                }

                _isUsingTouch = false;

                if (LastActiveController != null || _controllerWasDisconnected)
                    HandleControllerType();
                else
                    DisableAllCursors();

                UpdateUIForInputType();
            }

            if (_isUsingTouch != previousIsUsingTouch)
            {
                OnLastActiveControllerChanged();
                previousIsUsingTouch = _isUsingTouch;
            }
        }

        private bool HandleTouchInput()
        {
            if (ReInput.touch.touchCount > 0)
            {
                lastInputWasTouch = true;
                DisableAllCursors();
                return true;
            }
            return false;
        }

        private void HandleControllerType()
        {
            switch (CurrentControllerType)
            {
                case ControllerType.Joystick:
                case ControllerType.Custom:
                    HandleJoystickOrCustomController();
                    break;
                case ControllerType.Mouse:
                case ControllerType.Keyboard:
                    if (IsConsole)
                        HandleJoystickOrCustomController();
                    else
                        HandleMouseController();
                    break;
            }
        }

        private void HandleJoystickOrCustomController()
        {
            if (CurrentControllerType == ControllerType.Joystick && !alreadyShowedControllerHelp)
                ShowControllerHelpIfPossible();

            if (GameCursor != null)
            {
                bool isAndroidMobile = Application.platform == RuntimePlatform.Android;
                bool enableCursor = isAndroidMobile && CanActivateAndroidCursor(LastActiveController);
                GameCursor.enabled = CurrentControllerType == ControllerType.Joystick || enableCursor;
            }
            Cursor.visible = false;
            CanShowCustomCursor = false;
        }

        private void HandleMouseController()
        {
#if UNITY_SWITCH && !UNITY_EDITOR
            HandleJoystickOrCustomController();
#elif UNITY_STANDALONE || UNITY_EDITOR
            // UNITY_EDITOR is included so the standalone cursor path can be tested in Play Mode
            // regardless of the active Build Target (e.g. Android/iOS), where UNITY_STANDALONE
            // isn't defined and this would otherwise silently fall back to the OS default cursor.
            ConfigureStandaloneCursor();
#else
            ConfigureDefaultCursor();
#endif
        }

        private void ShowControllerHelpIfPossible()
        {
            // Always wait while the UI is blocked (scene transitions, cutscenes, UIBlockerOverlay):
            // the prompt would be invisible or land behind a blocker, so postpone instead of
            // consuming the one-shot flag.
            if (_uiBlocker.IsUiBlocked || UIBlockerOverlay.Enabled)
                return;

            // Never show in blacklisted scenes (and don't consume the one-shot flag either).
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            foreach (var blocked in ControllerHelpBlockedScenes)
            {
                if (!string.IsNullOrEmpty(blocked) &&
                    string.Equals(blocked, sceneName, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            if (_controllerHelpGate.CanShowControllerHelp)
            {
                alreadyShowedControllerHelp = true;
                OnShowControllerHelp?.Invoke();
            }
        }

        private void ConfigureStandaloneCursor()
        {
            if (GameCursor != null) GameCursor.enabled = false;
            CanShowCustomCursor = true;
            Cursor.visible = true;
            Cursor.SetCursor(CustomCursorEnabled ? CursorTexture : null, Vector2.zero, CursorMode.Auto);
        }

        private void ConfigureDefaultCursor()
        {
            if (GameCursor != null) GameCursor.enabled = false;
            Cursor.visible = true;
            CanShowCustomCursor = false;
        }

        private void DisableAllCursors()
        {
            if (GameCursor != null) GameCursor.enabled = false;
            Cursor.visible = false;
            CanShowCustomCursor = false;
        }
        #endregion

        #region Event Handlers
        private void OnControllerDisconnected(ControllerStatusChangedEventArgs args)
        {
            _controllerWasDisconnected = true;
            _controllerDisconnectedTime = Time.time;

            if (args.controller?.type == ControllerType.Joystick)
            {
                _lastKnownControllerType = args.controller.type;

                OnJoystickDisconnected(args.controller);
            }
        }

        private void OnControllerConnected(ControllerStatusChangedEventArgs args)
        {
            if (args.controller.type == ControllerType.Joystick)
            {
                _controllerWasDisconnected = false;
                ReleaseControllerDisconnectPause();
                LastActiveController = args.controller;
                _lastKnownControllerType = args.controller.type;
                OnLastActiveControllerChanged();
                UpdateUIForInputType();
            }
        }

        private void OnLastActiveControllerChanged()
        {
            OnInputTypeChanged?.Invoke(_isUsingTouch);
        }
        #endregion

        #region Utility Methods
        public bool CanActivateAndroidCursor(Controller controller)
        {
            if (controller?.tag != "AndroidRemote") return false;

            return Input.GetKey(KeyCode.UpArrow) ||
                   Input.GetKey(KeyCode.DownArrow) ||
                   Input.GetKey(KeyCode.LeftArrow) ||
                   Input.GetKey(KeyCode.RightArrow) ||
                   (GameCursor != null && GameCursor.enabled);
        }

        public void Vibrate(float motorLevel = 1f, float duration = 0.25f)
        {
            Player.SetVibration(0, motorLevel, duration);
        }

        private void UpdateUIForInputType()
        {
            var currentControllerType = CurrentControllerType;

            if (_previousInputState == _isUsingTouch && currentControllerType == _lastControllerType) return;

            // Clean up destroyed components and iterate on snapshot to avoid CollectionModifiedException
            _visibilityControllers.RemoveWhere(c => c == null);
            var snapshot = new InputVisibilityController[_visibilityControllers.Count];
            _visibilityControllers.CopyTo(snapshot);

            foreach (var controller in snapshot)
            {
                if (controller != null) controller.UpdateVisibility();
            }

            OnInputSpecializationChanged?.Invoke();

            _previousInputState = _isUsingTouch;
            _lastControllerType = currentControllerType;
        }

        public static void RegisterVisibilityController(InputVisibilityController controller)
        {
            if (controller == null) return;
            _visibilityControllers.Add(controller);
            controller.UpdateVisibility();
        }

        public static void UnregisterVisibilityController(InputVisibilityController controller)
        {
            if (controller != null) _visibilityControllers.Remove(controller);
        }
        #endregion
    }
}
