using System;
using System.Collections.Generic;

using Rewired;

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
using Steamworks;
#endif

using UnityEngine;
using UnityEngine.Events;
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
    public class RewiredInputManager : MonoBehaviour
    {
        #region Singleton Pattern
        public static RewiredInputManager Instance { get; private set; }
        #endregion

        #region Public Properties
        [Tooltip("Reference to the Rewired player")]
        public Rewired.Player Player { get; private set; }

        [Tooltip("Reference to the custom game cursor")]
        public Image GameCursor;

        [Tooltip("GameObject shown/hidden when pausing the game (optional)")]
        public GameObject GamePaused;

        [Tooltip("If true, the Steam overlay pauses the game automatically")]
        public bool PauseOnSteamOverlay = true;

        /// <summary>Indicates whether the custom cursor can be displayed.</summary>
        public static bool CanShowCustomCursor { get; private set; }

        [Tooltip("Enables/disables the custom cursor for standalone builds. Can also be set at runtime by host save data.")]
        public bool CustomCursorEnabled;

        [Tooltip("Texture used by the custom cursor when Custom Cursor Enabled is checked. Can be assigned here or at runtime.")]
        public Texture2D CursorTexture;

        [SerializeField]
        private bool alreadyShowedControllerHelp;

        public bool IsSteamOverlayActive = false;

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

        public static bool anyButton => Instance != null && Instance.Player != null &&
                                        (Instance.Player.GetButtonDown("MouseLeftButton") ||
                                         Instance.Player.GetButtonDown("BackButton") ||
                                         (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began));

        public static bool anyButtonNow => Instance != null && Instance.Player != null &&
                                           (Instance.Player.GetButton("MouseLeftButton") ||
                                            Instance.Player.GetButton("BackButton") ||
                                            (Input.touchCount > 0));

        public ControllerType CurrentControllerType
        {
            get
            {
                if (Instance == null)
                    return ControllerType.Joystick;

                if (IsUsingTouch)
                    return ControllerType.Custom;

                return LastActiveController?.type ?? _lastKnownControllerType;
            }
        }
        #endregion

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
        protected Callback<GameOverlayActivated_t> m_GameOverlayActivated;
#endif

        #region Public API
        /// <summary>
        /// Wires up the optional integration hooks. Call this once after the manager exists
        /// (e.g. right after Awake/Start of your own bootstrap). Any argument left null keeps
        /// the harmless default (never blocked, no modals, help always allowed).
        /// </summary>
        public void Configure(IUiBlocker uiBlocker = null, IModalStackProvider modalStack = null,
            IControllerHelpGate controllerHelpGate = null)
        {
            _uiBlocker = uiBlocker ?? NullUiBlocker.Instance;
            _modalStack = modalStack ?? NullModalStackProvider.Instance;
            _controllerHelpGate = controllerHelpGate ?? NullControllerHelpGate.Instance;
            IsConfigured = true;
        }
        #endregion

        #region Unity Lifecycle Methods
        private void Awake()
        {
            InitializeSingleton();
        }

        private void Start()
        {
            InitializePlayer();

            if (IsConsole)
                _lastKnownControllerType = ControllerType.Joystick;

            _lastMouseOrTouchMoveTime = Time.time;

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
            if (SteamManager.Initialized) m_GameOverlayActivated = Callback<GameOverlayActivated_t>.Create(OnGameOverlayActivated);
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

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
        private void OnGameOverlayActivated(GameOverlayActivated_t pCallback)
        {
            IsSteamOverlayActive = pCallback.m_bActive != 0;
        }
#endif

        private void Update()
        {
            if (Player == null && ReInput.isReady)
            {
                InitializePlayer();
            }

            SecondsSinceLastMouseOrTouchMove = Time.time - _lastMouseOrTouchMoveTime;

            UpdateCursorPosition();
            HandleInputSystem();
            HandleEscapeButtons();

            if (_controllerWasDisconnected && Player != null && Time.time - _controllerDisconnectedTime > CONTROLLER_RECONNECT_DELAY)
                CheckForControllerReconnection();

            if (GamePaused != null && GamePaused.activeSelf && anyButton)
                PauseGame(false);

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
            if (PauseOnSteamOverlay && SteamManager.Initialized && IsSteamOverlayActive &&
                (GamePaused == null || !GamePaused.activeSelf))
                PauseGame(true);
#endif
        }

        private void CheckForControllerReconnection()
        {
            if (Player == null) return;
            var currentController = Player.controllers.GetLastActiveController();
            if (currentController != null && currentController.type == ControllerType.Joystick)
            {
                _controllerWasDisconnected = false;
                LastActiveController = currentController;
                _lastKnownControllerType = currentController.type;
            }
        }

        /// <summary>Routes Escape/Return to the host's modal stack when present, otherwise to the generic EscapeButton/ReturnEscapeEvent components.</summary>
        private void HandleEscapeButtons()
        {
            if (_uiBlocker.IsUiBlocked) return;

            _modalStack.PruneInactiveTop();

            bool escapePressed = Input.GetKeyDown(KeyCode.Escape);
            if (Player != null)
            {
                escapePressed |= Player.GetButtonDown("MenuButton") || Player.GetButtonDown("BackButton");
            }

            if (escapePressed)
            {
                if (_modalStack.ModalCount > 0 && _modalStack.TryGetTopEscapeButton(out var escapeButton) &&
                    escapeButton != null && escapeButton.interactable && escapeButton.gameObject.activeSelf)
                {
                    escapeButton.onClick.Invoke();
                }
                else if (!EscapeButton.PressedScape())
                {
                    ReturnEscapeEvent.TriggerEscape();
                }
            }

            if (Input.GetKeyDown(KeyCode.Return))
            {
                if (_modalStack.ModalCount > 0 && _modalStack.TryGetTopOkButton(out var okButton) &&
                    okButton != null && okButton.interactable && okButton.gameObject.activeSelf)
                {
                    okButton.onClick.Invoke();
                }
                else
                {
                    ReturnEscapeEvent.TriggerOk();
                }
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) PauseGame(true);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (Application.platform == RuntimePlatform.Switch && !hasFocus)
                StartCoroutine(HandleSwitchModeChange());
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

        private void PauseGame(bool pause)
        {
            if (GamePaused != null) GamePaused.SetActive(pause);
            Time.timeScale = pause ? 0 : 1;
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
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
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializePlayer()
        {
            Player = ReInput.players.GetPlayer(0);
        }

        private void SubscribeToEvents()
        {
            ReInput.ControllerDisconnectedEvent += OnControllerDisconnected;
            ReInput.ControllerConnectedEvent += OnControllerConnected;
        }

        private void UnsubscribeFromEvents()
        {
            ReInput.ControllerDisconnectedEvent -= OnControllerDisconnected;
            ReInput.ControllerConnectedEvent -= OnControllerConnected;
        }
        #endregion

        #region Input Handling Methods
        private void UpdateCursorPosition()
        {
            if (Camera.main != null && GameCursor != null)
                RewiredMousePosition = RectTransformUtility.WorldToScreenPoint(Camera.main, GameCursor.transform.position);
        }

        private const float MOUSE_MOVEMENT_TIME_THRESHOLD = 0.01f;
        private const float MOUSE_MOVEMENT_INPUT_THRESHOLD = 0.1f;

        private bool _lastInputWasPC = false;

        private bool HasJoystickAxisInput(Controller joystick)
        {
            if (joystick == null) return false;
            int axisCount = joystick.axisCount;
            for (int i = 0; i < axisCount; i++)
            {
                if (Mathf.Abs(joystick.GetAxisValue(i)) > 0.15f)
                    return true;
            }
            return false;
        }

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
                if (Mathf.Abs(Player.GetAxis("MouseX")) > MOUSE_MOVEMENT_TIME_THRESHOLD || Mathf.Abs(Player.GetAxis("MouseY")) > MOUSE_MOVEMENT_TIME_THRESHOLD)
                    _lastMouseOrTouchMoveTime = Time.time;

                if (Mathf.Abs(Player.GetAxis("MouseX")) > MOUSE_MOVEMENT_INPUT_THRESHOLD || Mathf.Abs(Player.GetAxis("MouseY")) > MOUSE_MOVEMENT_INPUT_THRESHOLD)
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
                bool mouseActive = Mathf.Abs(Player.GetAxis("MouseX")) > MOUSE_MOVEMENT_INPUT_THRESHOLD || 
                                   Mathf.Abs(Player.GetAxis("MouseY")) > MOUSE_MOVEMENT_INPUT_THRESHOLD ||
                                   Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);

                bool keyboardActive = ReInput.controllers.keyboard.GetAnyButton();

                bool joystickActive = false;
                foreach (var joystick in Player.controllers.Joysticks)
                {
                    if (joystick.GetAnyButton() || HasJoystickAxisInput(joystick))
                    {
                        joystickActive = true;
                        break;
                    }
                }

                if (mouseActive || keyboardActive)
                {
                    _lastInputWasPC = true;
                }
                else if (joystickActive)
                {
                    _lastInputWasPC = false;
                }

                Controller currentController = null;
                if (_lastInputWasPC)
                {
                    var keyboardController = ReInput.controllers.GetController(ControllerType.Keyboard, 0);
                    if (keyboardController != null)
                        currentController = keyboardController;
                }
                else
                {
                    currentController = Player.controllers.GetLastActiveController();
                    if (currentController == null || currentController.type != ControllerType.Joystick)
                    {
                        foreach (var j in Player.controllers.Joysticks)
                        {
                            currentController = j;
                            break;
                        }
                    }
                }

                if (currentController != null || !_controllerWasDisconnected)
                    LastActiveController = currentController;

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

                if (IsConsole)
                    StartCoroutine(DelayedControllerDisconnectAction());
                else
                    PauseGame(true);
            }
        }

        private System.Collections.IEnumerator DelayedControllerDisconnectAction()
        {
            yield return new WaitForSeconds(2f);

            if (_controllerWasDisconnected && Player.controllers.GetLastActiveController() == null)
                PauseGame(true);
        }

        private void OnControllerConnected(ControllerStatusChangedEventArgs args)
        {
            if (args.controller.type == ControllerType.Joystick)
            {
                _controllerWasDisconnected = false;
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

            foreach (var controller in _visibilityControllers)
                if (controller != null) controller.UpdateVisibility();

            OnInputSpecializationChanged?.Invoke();

            _previousInputState = _isUsingTouch;
            _lastControllerType = currentControllerType;
        }

        public static void RegisterVisibilityController(InputVisibilityController controller)
        {
            _visibilityControllers.Add(controller);
            controller.UpdateVisibility();
        }

        public static void UnregisterVisibilityController(InputVisibilityController controller)
        {
            _visibilityControllers.Remove(controller);
        }
        #endregion
    }
}
