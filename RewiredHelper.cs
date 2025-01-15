using I2.Loc;

using Rewired;
using Rewired.Demos;

using Sirenix.OdinInspector;

using UnityEngine;
using UnityEngine.UI;

public class RewiredHelper : MonoBehaviour
{
    public Rewired.Player Player;
    public Image GameCursor;
    public UIPointer CursorUIPointer;

    public static bool CanShowCustomCursor = false;

    public static RewiredHelper instance;

    public bool AlreadyShowedControllerHelp = false;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            ReInput.ControllerDisconnectedEvent += OnControllerDisconnected;
        }

    }

    void OnDestroy()
    {
        // Unsubscribe from events
        ReInput.ControllerDisconnectedEvent -= OnControllerDisconnected;
    }

    private void Start()
    {
        // Get the Player for a particular playerId
        Player = ReInput.players.GetPlayer(0);
    }

    private Controller _ultimoControleAtivo;
    public Controller UltimoControleAtivo
    {
        get => _ultimoControleAtivo;
        set
        {
            if (_ultimoControleAtivo != value)
            {
                _ultimoControleAtivo = value;
                OnUltimoControleAtivoChanged();
            }
        }
    }

    private void Update()
    {
        RewiredMousePosition = RectTransformUtility.WorldToScreenPoint(Camera.main, GameCursor.transform.position);
        if (ReInput.touch == null) return;

        //Se Usou Touch, Some com Mouse
        if (ReInput.touch.touchCount > 0)
        {
            GameCursor.enabled = false;
            Cursor.visible = false;
            CanShowCustomCursor = false;

            return;
        }

        UltimoControleAtivo = Player.controllers.GetLastActiveController();

        if (UltimoControleAtivo != null)
        {
            switch (UltimoControleAtivo.type)
            {
                case ControllerType.Joystick:
                case ControllerType.Custom:
                    {
                        if (UltimoControleAtivo.type == ControllerType.Joystick && !AlreadyShowedControllerHelp && !Main.main.IsUiBlocked && !LoadingScreen.LoadingNow && LoadingScreen.AlreadyShowedMainMenu)
                        {
                            AlreadyShowedControllerHelp = true;
                            Dialogs.ShowDialog(Main.main.formController);
                        }

                        GameCursor.enabled = UltimoControleAtivo.type == ControllerType.Joystick || PodeAtivarCursorAndroid(UltimoControleAtivo); ;
                        Cursor.visible = false;
                        CanShowCustomCursor = false;

                        break;
                    }

                case ControllerType.Mouse:
                    {
#if UNITY_SWITCH && !UNITY_EDITOR
                        GameCursor.enabled = false;
                        CanShowCustomCursor = false;
                        Cursor.visible = false;
#elif UNITY_STANDALONE
                        GameCursor.enabled = false;
                        CanShowCustomCursor = true;
                        Cursor.visible = true;
                        Cursor.SetCursor(Main.main.SaveData.CustomCursor ? Main.main.Config.cursorTexture : null, Vector2.zero, CursorMode.Auto);
#else
                        GameCursor.enabled = false;
                        Cursor.visible = true;
                        CanShowCustomCursor = false;
#endif
                        break;
                    }

            }
        }
        else
        {
            GameCursor.enabled = false;
            Cursor.visible = false;
            CanShowCustomCursor = false;

        }
    }

    // This function will be called when a controller is fully disconnected
    // You can get information about the controller that was disconnected via the args parameter
    void OnControllerDisconnected(ControllerStatusChangedEventArgs args)
    {
        //Se Desconectou Controle, Abre Menu
        if (Player.controllers.GetLastActiveController() == args.controller)
            ReturnEscapeEvent.EscapePressed = true;
    }

    /// <summary>
    /// Verifica se o cursor pode ser ativado para um controle AndroidRemote.
    /// O cursor pode ser ativado se uma das teclas de seta for pressionada ou se o cursor do jogo já estiver ativado.
    /// </summary>
    /// <param name="controller">O controle a ser verificado.</param>
    /// <returns>Retorna verdadeiro se o cursor puder ser ativado, falso caso contrário.</returns>
    public bool PodeAtivarCursorAndroid(Controller controller)
    {
        return controller.tag == "AndroidRemote" &&
        (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) || GameCursor.enabled);
    }

    public static bool anyButton => instance == null ? false :
                                    instance.Player.GetButtonDown("MouseLeftButton") ||
                                    instance.Player.GetButtonDown("BackButton")
                                    || (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began);


    [ShowInInspector]
    public static Vector3 RewiredMousePosition;


    //Se mudar o ultimo controle ativo, atualiza todas traduções ativas
    private void OnUltimoControleAtivoChanged()
    {
        LocalizationManager.LocalizeAll(true);
    }
}


