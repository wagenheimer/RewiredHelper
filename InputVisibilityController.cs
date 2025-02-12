using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

/// <summary>
/// Componente para controlar a visibilidade de GameObjects baseado no tipo de input do usuário
/// </summary>
public class InputVisibilityController : MonoBehaviour
{
    #region Enums
    /// <summary>
    /// Modos de visibilidade disponíveis
    /// </summary>
    public enum VisibilityMode
    {
        [LabelText("🖐️ Mostrar em Touch")]
        ShowOnTouchHideOtherwise,

        [LabelText("🖱️ Mostrar em Outros")]
        HideOnTouchShowOtherwise,

        [LabelText("✅ Sempre Visível")]
        AlwaysShow,

        [LabelText("❌ Sempre Oculto")]
        AlwaysHide
    }
    #endregion

    #region Configuration
    [BoxGroup("Configuração", CenterLabel = true)]
    [Tooltip("Comportamento de visibilidade baseado no tipo de input")]
    [EnumToggleButtons]
    [SerializeField] private VisibilityMode _visibilityMode = VisibilityMode.HideOnTouchShowOtherwise;

    [BoxGroup("Configuração")]
    [LabelText("Atualizar no Start")]
    [SerializeField] private bool _shouldUpdateOnStart = true;
    #endregion

    #region Events
    [FoldoutGroup("Eventos", expanded: false)]
    [ListDrawerSettings(ShowFoldout = true, DraggableItems = false)]
    [PropertyOrder(10)]
    [SerializeField] private UnityEvent<bool> _onVisibilityChanged;
    #endregion

    #region Debug
    [BoxGroup("Estado Atual", centerLabel: true)]
    [ShowInInspector, ReadOnly]
    [LabelText("Visível")]
    private bool CurrentVisibility => gameObject.activeSelf;

    [BoxGroup("Estado Atual")]
    [ShowInInspector, ReadOnly]
    [LabelText("Input Atual")]
    private string CurrentInputType => RewiredHelper.IsUsingTouch ? "Touch" : "Outro";
    #endregion

    #region Unity Lifecycle Methods
    private void Awake() => RewiredHelper.RegisterVisibilityController(this);

    private void Start()
    {
        if (_shouldUpdateOnStart)
        {
            UpdateVisibility();
        }
    }

    private void OnDestroy()
    {
        RewiredHelper.UnregisterVisibilityController(this);
        _onVisibilityChanged.RemoveAllListeners();
    }
    #endregion

    #region Public Methods
    [BoxGroup("Ações")]
    [Button(ButtonSizes.Medium), GUIColor(0.4f, 0.8f, 1f)]
    [Tooltip("Atualiza manualmente a visibilidade")]
    public void UpdateVisibility()
    {
        bool isTouch = RewiredHelper.IsUsingTouch;
        bool shouldShow = CalculateVisibility(isTouch);

        UpdateGameObjectState(shouldShow);
        NotifyVisibilityChange(isTouch);
    }

    [BoxGroup("Ações")]
    [Button("Mudar Modo", ButtonSizes.Small)]
    [GUIColor(0.7f, 0.9f, 0.7f)]
    public void SetVisibilityMode(VisibilityMode newMode)
    {
        _visibilityMode = newMode;
        UpdateVisibility();
    }
    #endregion

    #region Private Methods
    private bool CalculateVisibility(bool isTouch) => _visibilityMode switch
    {
        VisibilityMode.ShowOnTouchHideOtherwise => isTouch,
        VisibilityMode.HideOnTouchShowOtherwise => !isTouch,
        VisibilityMode.AlwaysShow => true,
        _ => false
    };

    private void UpdateGameObjectState(bool shouldShow)
    {
        if (gameObject.activeSelf != shouldShow)
        {
            gameObject.SetActive(shouldShow);
        }
    }

    private void NotifyVisibilityChange(bool isTouch)
    {
        try
        {
            _onVisibilityChanged?.Invoke(isTouch);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in visibility notification: {e.Message}", this);
        }
    }
    #endregion
}