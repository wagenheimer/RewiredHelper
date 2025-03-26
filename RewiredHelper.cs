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

    [ShowInInspector]
    public static bool IsUsingTouch => instance != null && instance._isUsingTouch;
    public static RewiredHelper Instance => instance;
    #endregion

    #region Private Fields
    private Controller lastActiveController;
    private bool _isUsingTouch;
    private bool _previousInputState;
    private bool lastInputWasTouch = false;
    private bool previousIsUsingTouch;
    private ControllerType _lastControllerType;

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
                lastActiveController = value;
                OnLastActiveControllerChanged();
            }
        }
    }

    /// <summary>
    /// Verifica se algum botão foi pressionado
    /// </summary>
    public static bool anyButton => instance != null && (instance.Player.GetButtonDown("MouseLeftButton") ||
                                                         instance.Player.GetButtonDown("BackButton") ||
                                                         (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began));
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
        UpdateCursorPosition();
        HandleInputSystem();
        HandleScapeButtons();

        //Se Está Pausado e Apertou Botão
        if (GamePaused.gameObject.activeSelf && anyButton)
            PauseGame(false);

        //Se Steam e Overlay está Ativo, pausa o Jogo
        if (Main.main.Config.Publisher == Publisher.Steam && SteamManager.Initialized && IsSteamOverlayActive && !GamePaused.gameObject.activeSelf)
            PauseGame(true);
    }

    /// <summary>
    /// Método responsável por gerenciar a lógica de botões de escape e retorno
    /// </summary>
    private void HandleScapeButtons()
    {
        if (Main.main == null) return;

        // Verifica se a UI não está bloqueada para processamento de inputs
        if (Main.main.IsUiBlocked) return;


        // Limpa modais nulos ou inativos para evitar problemas de referência
        if (Dialogs.Modals.Count > 0 &&
            (Dialogs.Modals[0] == null || (!Dialogs.Modals[0].gameObject.activeSelf)))
        {
            Dialogs.Modals.Clear();
        }

        // Verifica pressionamento de botões de escape (Escape, Menu, Voltar)
        if (Input.GetKeyDown(KeyCode.Escape) ||
            Player.GetButtonDown("MenuButton") ||
            Player.GetButtonDown("BackButton"))
        {
            // Prioriza o botão de escape do último modal ativo
            if (Dialogs.Modals.Count > 0 &&
                Dialogs.Modals[^1].EscapeButton != null &&
                Dialogs.Modals[^1].EscapeButton.interactable &&
                Dialogs.Modals[^1].EscapeButton.gameObject.activeSelf &&
                Dialogs.Modals[^1].EscapeButton.onClick.GetPersistentEventCount() > 0)
            {
                // Dispara o evento de escape do modal
                Dialogs.Modals[^1].EscapeButton.onClick.Invoke();
            }
            else
            {
                // Caso não haja modal ativo, tenta acionar botões de escape personalizados
                if (!EscapeButton.PressedScape())
                {
                    // Se nenhum botão de escape foi acionado, define evento de escape global
                    ReturnEscapeEvent.EscapePressed = true;
                }
            }
        }

        // Verifica pressionamento da tecla Enter/Return
        if (Input.GetKeyDown(KeyCode.Return))
        {
            // Prioriza o botão OK do último modal ativo
            if (Dialogs.Modals.Count > 0 &&
                Dialogs.Modals[^1].OkButton != null &&
                Dialogs.Modals[^1].OkButton.interactable &&
                Dialogs.Modals[^1].OkButton.onClick.GetPersistentEventCount() > 0)
            {
                // Dispara o evento de confirmação do modal
                Dialogs.Modals[^1].OkButton.onClick.Invoke();
            }
            else
            {
                // Caso não haja modal ativo, define evento de OK global
                ReturnEscapeEvent.OkPressed = true;
            }
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) PauseGame(true);
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
    }

    private void UnsubscribeFromEvents()
    {
        ReInput.ControllerDisconnectedEvent -= OnControllerDisconnected;
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

    private void HandleInputSystem()
    {
        if (ReInput.touch == null) return;
        if (Player == null) return;

        _isUsingTouch = HandleTouchInput();

        if (_isUsingTouch)
        {
            lastInputWasTouch = true;
        }
        else
        {
            // Verificar movimento do mouse
            if (Mathf.Abs(Player.GetAxis("MouseX")) > 0.1f || Mathf.Abs(Player.GetAxis("MouseY")) > 0.1f)
            {
                lastInputWasTouch = false;
            }
            // Verificar movimento do stick
            else if (Player.controllers.GetLastActiveController()?.type == ControllerType.Joystick)
            {
                if (Mathf.Abs(Player.GetAxis("Horizontal")) > 0.1f || Mathf.Abs(Player.GetAxis("Vertical")) > 0.1f)
                {
                    lastInputWasTouch = false;
                }
            }
        }

        _isUsingTouch = lastInputWasTouch;

        if (_isUsingTouch)
        {
            UltimoControleAtivo = null;
            UpdateUIForInputType();
        }
        else
        {
            UltimoControleAtivo = Player.controllers.GetLastActiveController();
            _isUsingTouch = false;

            if (UltimoControleAtivo != null)
            {
                HandleControllerType();
            }
            else
            {
                DisableAllCursors();
            }

            UpdateUIForInputType();
        }

        // Verificação explícita da mudança de _isUsingTouch
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
        switch (UltimoControleAtivo.type)
        {
            case ControllerType.Joystick:
            case ControllerType.Custom:
                HandleJoystickOrCustomController();
                break;
            case ControllerType.Mouse:
                HandleMouseController();
                break;
        }
    }

    private void HandleJoystickOrCustomController()
    {
        if (UltimoControleAtivo.type == ControllerType.Joystick && !alreadyShowedControllerHelp)
        {
            ShowControllerHelpIfPossible();
        }

        GameCursor.enabled = UltimoControleAtivo.type == ControllerType.Joystick ||
                            CanActivateAndroidCursor(UltimoControleAtivo);
        Cursor.visible = false;
        CanShowCustomCursor = false;
    }

    private void HandleMouseController()
    {
#if UNITY_SWITCH && !UNITY_EDITOR
        DisableAllCursors();
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
    /// Manipula o evento de desconexão do controle
    /// </summary>
    private void OnControllerDisconnected(ControllerStatusChangedEventArgs args)
    {
        if (Player.controllers.GetLastActiveController() == args.controller)
        {
            PauseGame(true);
        }
    }

    /// <summary>
    /// Atualiza as traduções quando o controlador ativo muda
    /// </summary>
    private void OnLastActiveControllerChanged()
    {
        LocalizationManager.LocalizeAll(true);
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// Verifica se o cursor pode ser ativado para um controle AndroidRemote
    /// </summary>
    /// <param name="controller">O controle a ser verificado</param>
    /// <returns>True se o cursor puder ser ativado, false caso contrário</returns>
    public bool CanActivateAndroidCursor(Controller controller)
    {
        if (controller.tag != "AndroidRemote") return false;

        return Input.GetKey(KeyCode.UpArrow) ||
               Input.GetKey(KeyCode.DownArrow) ||
               Input.GetKey(KeyCode.LeftArrow) ||
               Input.GetKey(KeyCode.RightArrow) ||
               GameCursor.enabled;
    }
    #endregion

    public void VibraControle(float motorLevel = 1f, float duration = 0.25f)
    {
        // Set vibration in all Joysticks assigned to the Player
        Player.SetVibration(0, motorLevel, duration);
        //Player.SetVibration(1, motorLevel, duration);
    }

    private void UpdateUIForInputType()
    {
        if (_previousInputState == _isUsingTouch && UltimoControleAtivo?.type == _lastControllerType) return;

        foreach (var controller in _visibilityControllers)
        {
            if (controller != null)
            {
                controller.UpdateVisibility();
            }
        }

        _previousInputState = _isUsingTouch;
        _lastControllerType = UltimoControleAtivo?.type ?? ControllerType.Mouse;
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


}