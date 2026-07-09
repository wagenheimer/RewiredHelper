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
        [Tooltip("Referência ao jogador do Rewired")]
        public Rewired.Player Player { get; private set; }

        [Tooltip("Referência ao cursor customizado do jogo")]
        public Image GameCursor;

        [Tooltip("GameObject exibido/ocultado ao pausar o jogo (opcional)")]
        public GameObject GamePaused;

        [Tooltip("Se verdadeiro, o overlay do Steam pausa o jogo automaticamente")]
        public bool PauseOnSteamOverlay = true;

        /// <summary>Indica se o cursor customizado pode ser exibido.</summary>
        public static bool CanShowCustomCursor { get; private set; }

        /// <summary>Habilita/desabilita o cursor customizado do jogo (definido pelo save data do host).</summary>
        public bool CustomCursorEnabled { get; set; }

        /// <summary>Textura usada pelo cursor customizado quando <see cref="CustomCursorEnabled"/> é verdadeiro.</summary>
        public Texture2D CursorTexture { get; set; }

        [SerializeField]
        private bool alreadyShowedControllerHelp;

        public bool IsSteamOverlayActive = false;

        [Tooltip("Posição atual do mouse no sistema Rewired")]
        public static Vector3 RewiredMousePosition { get; private set; }

        public static event Action<bool> OnInputTypeChanged;

        /// <summary>Disparado quando o tipo de input/controle muda, para quem precisa re-especializar UI (ex.: localização).</summary>
        public static event Action OnInputSpecializationChanged;

        [Tooltip("Segundos desde o último movimento do mouse ou toque.")]
        public static float SecondsSinceLastMouseOrTouchMove { get; private set; }

        public static bool IsUsingTouch => Instance != null && Instance._isUsingTouch;

        /// <summary>Invocado ao invés de abrir um diálogo de ajuda do controle diretamente — o jogo decide o que mostrar.</summary>
        public UnityEvent OnShowControllerHelp;
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
        [Tooltip("Controla o último dispositivo de entrada ativo")]
        public Controller UltimoControleAtivo
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
                if (IsConsole && _controllerWasDisconnected && Time.time - _controllerDisconnectedTime < 5f)
                    return _lastKnownControllerType;

                if (_isUsingTouch) return ControllerType.Custom;

                return UltimoControleAtivo?.type ?? _lastKnownControllerType;
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
        }

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
        private void OnGameOverlayActivated(GameOverlayActivated_t pCallback)
        {
            IsSteamOverlayActive = pCallback.m_bActive != 0;
        }
#endif

        private void Update()
        {
            SecondsSinceLastMouseOrTouchMove = Time.time - _lastMouseOrTouchMoveTime;

            UpdateCursorPosition();
            HandleInputSystem();
            HandleEscapeButtons();

            if (_controllerWasDisconnected && Time.time - _controllerDisconnectedTime > CONTROLLER_RECONNECT_DELAY)
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
            var currentController = Player.controllers.GetLastActiveController();
            if (currentController != null && currentController.type == ControllerType.Joystick)
            {
                _controllerWasDisconnected = false;
                UltimoControleAtivo = currentController;
                _lastKnownControllerType = currentController.type;
            }
        }

        /// <summary>Routes Escape/Return to the host's modal stack when present, otherwise to the generic EscapeButton/ReturnEscapeEvent components.</summary>
        private void HandleEscapeButtons()
        {
            if (_uiBlocker.IsUiBlocked) return;

            _modalStack.PruneInactiveTop();

            if (Input.GetKeyDown(KeyCode.Escape) ||
                Player.GetButtonDown("MenuButton") ||
                Player.GetButtonDown("BackButton"))
            {
                if (_modalStack.ModalCount > 0 && _modalStack.TryGetTopEscapeButton(out var escapeButton) &&
                    escapeButton != null && escapeButton.interactable && escapeButton.gameObject.activeSelf &&
                    escapeButton.onClick.GetPersistentEventCount() > 0)
                {
                    escapeButton.onClick.Invoke();
                }
                else if (!EscapeButton.PressedScape())
                {
                    ReturnEscapeEvent.EscapePressed = true;
                }
            }

            if (Input.GetKeyDown(KeyCode.Return))
            {
                if (_modalStack.ModalCount > 0 && _modalStack.TryGetTopOkButton(out var okButton) &&
                    okButton != null && okButton.interactable && okButton.onClick.GetPersistentEventCount() > 0)
                {
                    okButton.onClick.Invoke();
                }
                else
                {
                    ReturnEscapeEvent.OkPressed = true;
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
                if (!IsConsole) UltimoControleAtivo = null;
                UpdateUIForInputType();
            }
            else
            {
                var currentController = Player.controllers.GetLastActiveController();

                if (currentController != null || !_controllerWasDisconnected)
                    UltimoControleAtivo = currentController;

                _isUsingTouch = false;

                if (UltimoControleAtivo != null || _controllerWasDisconnected)
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
                GameCursor.enabled = CurrentControllerType == ControllerType.Joystick ||
                                     CanActivateAndroidCursor(UltimoControleAtivo);
            Cursor.visible = false;
            CanShowCustomCursor = false;
        }

        private void HandleMouseController()
        {
#if UNITY_SWITCH && !UNITY_EDITOR
            HandleJoystickOrCustomController();
#elif UNITY_STANDALONE
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
                _lastKnownControllerType = args.controller.type;
                UltimoControleAtivo = args.controller;

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
