using I2.Loc;

using Rewired;
using Rewired.Demos;

using Sirenix.OdinInspector;

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
using Steamworks;
#endif

using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gerenciador de input que utiliza o sistema Rewired para controlar diferentes tipos de entrada (teclado, mouse, controle, touch).
/// Responsável por gerenciar o cursor do jogo e diferentes estados de input.
/// </summary>
public class RewiredHelper : MonoBehaviour
{
    #region Singleton Pattern
    public static RewiredHelper instance { get; private set; }
    #endregion

    #region Public Properties
    [Tooltip("Referência ao jogador do Rewired")]
    public Rewired.Player Player { get; private set; }

    [Tooltip("Referência ao cursor customizado do jogo")]
    public Image GameCursor;

    [Tooltip("Referência ao pointer da UI")]
    public UIPointer CursorUIPointer;

    [TabGroup("Editor Canvas")] public GameObject GamePaused;

    /// <summary>
    /// Indica se o cursor customizado pode ser exibido
    /// </summary>
    public static bool CanShowCustomCursor { get; private set; }

    /// <summary>
    /// Indica se o tutorial do controle já foi exibido
    /// </summary>
    [SerializeField]
    private bool alreadyShowedControllerHelp;

    public bool IsSteamOverlayActive = false;

    /// <summary>
    /// Posição atual do mouse no sistema Rewired
    /// </summary>
    [ShowInInspector]
    public static Vector3 RewiredMousePosition { get; private set; }

    [ShowInInspector]
    public static event System.Action<bool> OnInputTypeChanged;

    [ShowInInspector, Tooltip("Segundos desde o último movimento do mouse ou toque.")]
    public static float SecondsSinceLastMouseOrTouchMove { get; private set; }

    [ShowInInspector]
    public static bool IsUsingTouch => instance != null && instance._isUsingTouch;
    public static RewiredHelper Instance => instance;
    #endregion

    #region Private Fields
    private float _lastMouseOrTouchMoveTime;
    private Controller lastActiveController;
    private bool _isUsingTouch;
    private bool _previousInputState;
    private bool lastInputWasTouch = false;
    private bool previousIsUsingTouch;
    private ControllerType _lastControllerType;

    // CORREÇÃO: Adicionar campos para gerenciar estado de desconexão
    private bool _controllerWasDisconnected = false;
    private ControllerType _lastKnownControllerType = ControllerType.Joystick; // Default para console
    private float _controllerDisconnectedTime = 0f;
    private const float CONTROLLER_RECONNECT_DELAY = 0.5f; // Delay para evitar mudanças rápidas

    // CORREÇÃO: Detectar plataforma para comportamento específico
    private bool IsConsole => Application.platform == RuntimePlatform.Switch ||
                             Application.platform == RuntimePlatform.PS4 ||
                             Application.platform == RuntimePlatform.PS5 ||
                             Application.platform == RuntimePlatform.XboxOne;

    private static readonly HashSet<InputVisibilityController> _visibilityControllers = new();
    #endregion

    #region Properties
    /// <summary>
    /// Controla o último dispositivo de entrada ativo
    /// </summary>
    [ShowInInspector]
    public Controller UltimoControleAtivo
    {
        get => lastActiveController;
        private set
        {
            if (lastActiveController != value)
            {
                Controller previous = lastActiveController;
                lastActiveController = value;
                Debug.Log($"<color=#FFA500>Controller changed:</color> <color=#FF6347>{(previous != null ? previous.name : "none")}</color> <color=#FFFFFF>-></color> <color=#4CAF50>{(value != null ? value.name : "none")}</color>");
                OnLastActiveControllerChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether any input button has been pressed.
    /// </summary>
    public static bool anyButton => instance != null && instance.Player != null &&
                                    (instance.Player.GetButtonDown("MouseLeftButton") ||
                                     instance.Player.GetButtonDown("BackButton") ||
                                     (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began));

    /// <summary>
    /// Gets a value indicating whether any button is currently being pressed.
    /// </summary>
    public static bool anyButtonNow => instance != null && instance.Player != null &&
                                       (instance.Player.GetButton("MouseLeftButton") ||
                                        instance.Player.GetButton("BackButton") ||
                                        (Input.touchCount > 0));

    // CORREÇÃO: Propriedade para obter o tipo de controle adequado
    public ControllerType CurrentControllerType
    {
        get
        {
            // Se estamos em console e um controle foi desconectado recentemente, manter o tipo de controle
            if (IsConsole && _controllerWasDisconnected && Time.time - _controllerDisconnectedTime < 5f)
            {
                return _lastKnownControllerType;
            }

            if (_isUsingTouch) return ControllerType.Custom; // Representa touch

            return UltimoControleAtivo?.type ?? _lastKnownControllerType;
        }
    }
    #endregion

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
    protected Callback<GameOverlayActivated_t> m_GameOverlayActivated;
#endif

    #region Unity Lifecycle Methods
    private void Awake()
    {
        InitializeSingleton();
    }

    private void Start()
    {
        InitializePlayer();

        // CORREÇÃO: Definir tipo padrão baseado na plataforma
        if (IsConsole)
        {
            _lastKnownControllerType = ControllerType.Joystick;
        }

        _lastMouseOrTouchMoveTime = Time.time;

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
        if (SteamManager.Initialized) m_GameOverlayActivated = Callback<GameOverlayActivated_t>.Create(OnGameOverlayActivated);
#endif
    }

#if STEAMWORKS_NET && !DISABLESTEAMWORKS
    private void OnGameOverlayActivated(GameOverlayActivated_t pCallback)
    {
        if (pCallback.m_bActive != 0)
        {
            IsSteamOverlayActive = true;
            Debug.Log("Steam Overlay has been activated");
        }
        else
        {
            IsSteamOverlayActive = false;
            Debug.Log("Steam Overlay has been closed");
        }
    }
#endif

    private void Update()
    {
        SecondsSinceLastMouseOrTouchMove = Time.time - _lastMouseOrTouchMoveTime;

        UpdateCursorPosition();
        HandleInputSystem();
        HandleScapeButtons();

        // CORREÇÃO: Gerenciar timeout de desconexão
        if (_controllerWasDisconnected && Time.time - _controllerDisconnectedTime > CONTROLLER_RECONNECT_DELAY)
        {
            CheckForControllerReconnection();
        }

        //Se Está Pausado e Apertou Botão
        if (GamePaused.gameObject.activeSelf && anyButton)
            PauseGame(false);

        //Se Steam e Overlay está Ativo, pausa o Jogo
#if STEAMWORKS_NET && !DISABLESTEAMWORKS
        if (Main.main.Config.Publisher == Publisher.Steam && SteamManager.Initialized && IsSteamOverlayActive && !GamePaused.gameObject.activeSelf)
            PauseGame(true);
#endif
    }

    // CORREÇÃO: Método para verificar reconexão do controle
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

    /// <summary>
    /// Método responsável por gerenciar a lógica de botões de escape e retorno
    /// </summary>
    private void HandleScapeButtons()
    {
        if (Main.main == null) return;
        if (Main.main.IsUiBlocked) return;

        if (Dialogs.Modals.Count > 0 &&
            (Dialogs.Modals[0] == null || (!Dialogs.Modals[0].gameObject.activeSelf)))
        {
            Dialogs.Modals.Clear();
        }

        if (Input.GetKeyDown(KeyCode.Escape) ||
            Player.GetButtonDown("MenuButton") ||
            Player.GetButtonDown("BackButton"))
        {
            if (Dialogs.Modals.Count > 0 &&
                Dialogs.Modals[^1].EscapeButton != null &&
                Dialogs.Modals[^1].EscapeButton.interactable &&
                Dialogs.Modals[^1].EscapeButton.gameObject.activeSelf &&
                Dialogs.Modals[^1].EscapeButton.onClick.GetPersistentEventCount() > 0)
            {
                Dialogs.Modals[^1].EscapeButton.onClick.Invoke();
            }
            else
            {
                if (!EscapeButton.PressedScape())
                {
                    ReturnEscapeEvent.EscapePressed = true;
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (Dialogs.Modals.Count > 0 &&
                Dialogs.Modals[^1].OkButton != null &&
                Dialogs.Modals[^1].OkButton.interactable &&
                Dialogs.Modals[^1].OkButton.onClick.GetPersistentEventCount() > 0)
            {
                Dialogs.Modals[^1].OkButton.onClick.Invoke();
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

    // CORREÇÃO: Detectar mudança de modo handheld no Switch
    void OnApplicationFocus(bool hasFocus)
    {
        if (Application.platform == RuntimePlatform.Switch && !hasFocus)
        {
            // Switch mudando para modo handheld ou perdendo foco
            StartCoroutine(HandleSwitchModeChange());
        }
    }

    private System.Collections.IEnumerator HandleSwitchModeChange()
    {
        yield return new WaitForSeconds(0.5f); // Aguardar estabilização

        // Forçar atualização do estado de input
        var currentController = Player.controllers.GetLastActiveController();
        if (currentController != null)
        {
            _lastKnownControllerType = currentController.type;
        }

        OnLastActiveControllerChanged();
        UpdateUIForInputType();
    }

    private void PauseGame(bool pause)
    {
        GamePaused.gameObject.SetActive(pause);
        Time.timeScale = pause ? 0 : 1;
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
    #endregion

    #region Initialization Methods
    private void InitializeSingleton()
    {
        if (instance == null)
        {
            instance = this;
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
        ReInput.ControllerConnectedEvent += OnControllerConnected; // CORREÇÃO: Adicionar evento de conexão
    }

    private void UnsubscribeFromEvents()
    {
        ReInput.ControllerDisconnectedEvent -= OnControllerDisconnected;
        ReInput.ControllerConnectedEvent -= OnControllerConnected; // CORREÇÃO: Remover evento de conexão
    }
    #endregion

    #region Input Handling Methods
    private void UpdateCursorPosition()
    {
        if (Camera.main != null && GameCursor != null)
        {
            RewiredMousePosition = RectTransformUtility.WorldToScreenPoint(Camera.main, GameCursor.transform.position);
        }
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
            // Verifca se houve movimento do mouse 
            if (Mathf.Abs(Player.GetAxis("MouseX")) > MOUSE_MOVEMENT_TIME_THRESHOLD || Mathf.Abs(Player.GetAxis("MouseY")) > MOUSE_MOVEMENT_TIME_THRESHOLD)
                _lastMouseOrTouchMoveTime = Time.time;

            // Verificar movimento do mouse
            if (Mathf.Abs(Player.GetAxis("MouseX")) > MOUSE_MOVEMENT_INPUT_THRESHOLD || Mathf.Abs(Player.GetAxis("MouseY")) > MOUSE_MOVEMENT_INPUT_THRESHOLD)
            {
                lastInputWasTouch = false;
            }
        }

        _isUsingTouch = lastInputWasTouch;

        if (_isUsingTouch)
        {
            // CORREÇÃO: Não zerar UltimoControleAtivo se estiver em console
            if (!IsConsole)
            {
                UltimoControleAtivo = null;
            }
            UpdateUIForInputType();
        }
        else
        {
            var currentController = Player.controllers.GetLastActiveController();

            // CORREÇÃO: Manter último controle conhecido se nenhum estiver ativo
            if (currentController != null || !_controllerWasDisconnected)
            {
                UltimoControleAtivo = currentController;
            }

            _isUsingTouch = false;

            if (UltimoControleAtivo != null || _controllerWasDisconnected)
            {
                HandleControllerType();
            }
            else
            {
                DisableAllCursors();
            }

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
        var controllerType = CurrentControllerType; // CORREÇÃO: Usar propriedade corrigida

        switch (controllerType)
        {
            case ControllerType.Joystick:
            case ControllerType.Custom:
                HandleJoystickOrCustomController();
                break;
            case ControllerType.Mouse:
                // CORREÇÃO: Em console, tratar mouse como joystick
                if (IsConsole)
                {
                    HandleJoystickOrCustomController();
                }
                else
                {
                    HandleMouseController();
                }
                break;
        }
    }

    private void HandleJoystickOrCustomController()
    {
        if (CurrentControllerType == ControllerType.Joystick && !alreadyShowedControllerHelp)
        {
            ShowControllerHelpIfPossible();
        }

        GameCursor.enabled = CurrentControllerType == ControllerType.Joystick ||
                            CanActivateAndroidCursor(UltimoControleAtivo);
        Cursor.visible = false;
        CanShowCustomCursor = false;
    }

    private void HandleMouseController()
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        // CORREÇÃO: No Switch, sempre tratar como controle
        HandleJoystickOrCustomController();
#elif UNITY_STANDALONE
        ConfigureStandaloneCursor();
#else
        ConfigureDefaultCursor();
#endif
    }

    private void ShowControllerHelpIfPossible()
    {
        if (!Main.main.IsUiBlocked && !LoadingScreen.LoadingNow && LoadingScreen.AlreadyShowedMainMenu)
        {
            alreadyShowedControllerHelp = true;
            Dialogs.ShowDialog(Main.main.formController);
        }
    }

    private void ConfigureStandaloneCursor()
    {
        GameCursor.enabled = false;
        CanShowCustomCursor = true;
        Cursor.visible = true;
        Cursor.SetCursor(
            Main.main.SaveData.CustomCursor ? Main.main.Config.cursorTexture : null,
            Vector2.zero,
            CursorMode.Auto
        );
    }

    private void ConfigureDefaultCursor()
    {
        GameCursor.enabled = false;
        Cursor.visible = true;
        CanShowCustomCursor = false;
    }

    private void DisableAllCursors()
    {
        GameCursor.enabled = false;
        Cursor.visible = false;
        CanShowCustomCursor = false;
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// CORREÇÃO: Manipula o evento de desconexão do controle sem mudar para PC
    /// </summary>
    private void OnControllerDisconnected(ControllerStatusChangedEventArgs args)
    {
        _controllerWasDisconnected = true;
        _controllerDisconnectedTime = Time.time;

        if (args.controller?.type == ControllerType.Joystick)
        {
            _lastKnownControllerType = args.controller.type;

            // CORREÇÃO: Não pausar imediatamente, dar tempo para reconexão
            if (IsConsole)
            {
                StartCoroutine(DelayedControllerDisconnectAction());
            }
            else
            {
                PauseGame(true);
            }
        }
    }

    // CORREÇÃO: Delay para pausar o jogo em caso de desconexão
    private System.Collections.IEnumerator DelayedControllerDisconnectAction()
    {
        yield return new WaitForSeconds(2f); // Aguardar 2 segundos

        // Se ainda estiver desconectado após o delay, pausar
        if (_controllerWasDisconnected && Player.controllers.GetLastActiveController() == null)
        {
            PauseGame(true);
        }
    }

    /// <summary>
    /// CORREÇÃO: Manipula o evento de conexão do controle
    /// </summary>
    private void OnControllerConnected(ControllerStatusChangedEventArgs args)
    {
        if (args.controller.type == ControllerType.Joystick)
        {
            _controllerWasDisconnected = false;
            _lastKnownControllerType = args.controller.type;
            UltimoControleAtivo = args.controller;

            // Atualizar UI imediatamente
            OnLastActiveControllerChanged();
            UpdateUIForInputType();
        }
    }

    /// <summary>
    /// Atualiza as traduções quando o controlador ativo muda
    /// </summary>
    private void OnLastActiveControllerChanged()
    {
        // LocalizationManager.LocalizeAll(true); // REMOVIDO: Agora é chamado apenas quando o tipo muda em UpdateUIForInputType
        OnInputTypeChanged?.Invoke(_isUsingTouch); // CORREÇÃO: Disparar evento
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// Verifica se o cursor pode ser ativado para um controle AndroidRemote
    /// </summary>
    public bool CanActivateAndroidCursor(Controller controller)
    {
        if (controller?.tag != "AndroidRemote") return false;

        return Input.GetKey(KeyCode.UpArrow) ||
               Input.GetKey(KeyCode.DownArrow) ||
               Input.GetKey(KeyCode.LeftArrow) ||
               Input.GetKey(KeyCode.RightArrow) ||
               GameCursor.enabled;
    }

    public void VibraControle(float motorLevel = 1f, float duration = 0.25f)
    {
        Player.SetVibration(0, motorLevel, duration);
    }

    private void UpdateUIForInputType()
    {
        var currentControllerType = CurrentControllerType; // CORREÇÃO: Usar propriedade corrigida

        if (_previousInputState == _isUsingTouch && currentControllerType == _lastControllerType) return;

        foreach (var controller in _visibilityControllers)
        {
            if (controller != null)
            {
                controller.UpdateVisibility();
            }
        }

        // OTIMIZAÇÃO: Forçar atualização de especialização apenas quando o tipo de controle muda
        SpecializationManager.Singleton.ForceUpdateSpecialization();

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