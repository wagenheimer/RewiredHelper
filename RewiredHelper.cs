using I2.Loc;

using Rewired;
using Rewired.Demos;

using Sirenix.OdinInspector;

using Steamworks;

using System;
using System.Collections;

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

    /// <summary>
    /// Posição atual do mouse no sistema Rewired
    /// </summary>
    [ShowInInspector]
    public static Vector3 RewiredMousePosition { get; private set; }
    #endregion

    #region Private Fields
    private Controller lastActiveController;
    #endregion

    #region Properties
    /// <summary>
    /// Controla o último dispositivo de entrada ativo
    /// </summary>
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
    public static bool anyButton => instance == null ? false :
        instance.Player.GetButtonDown("MouseLeftButton") ||
        instance.Player.GetButtonDown("BackButton") ||
        (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began);
    #endregion

    #region Unity Lifecycle Methods
    private void Awake()
    {
        InitializeSingleton();
    }

    private void Start()
    {
        InitializePlayer();
    }

    private void Update()
    {
        UpdateCursorPosition();
        HandleInputSystem();

        //Se Está Pausado e Apertou Botão
        if (GamePaused.gameObject.activeSelf && anyButton)
            PauseGame(false);

        //Se Steam e Overlay está Ativo, pausa o Jogo
        if (Main.main.Config.Publisher == Publisher.Steam && SteamUtils.IsOverlayEnabled() && !GamePaused.gameObject.activeSelf)
            PauseGame(true);
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

        if (HandleTouchInput()) return;

        UltimoControleAtivo = Player.controllers.GetLastActiveController();

        if (UltimoControleAtivo != null)
        {
            HandleControllerType();
        }
        else
        {
            DisableAllCursors();
        }
    }

    private bool HandleTouchInput()
    {
        if (ReInput.touch.touchCount > 0)
        {
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

    public void VibraControle(float motorLevel = 1f, float duration = 0.5f)
    {
        // Set vibration in all Joysticks assigned to the Player
        Player.SetVibration(0, motorLevel, duration);
        Player.SetVibration(1, motorLevel, duration);
    }
}