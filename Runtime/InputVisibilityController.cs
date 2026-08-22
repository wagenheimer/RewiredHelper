using Rewired;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Controls a GameObject's active state, Selectable interactability, or CanvasGroup
    /// based on the active Rewired input type (Mouse/Keyboard, Joystick/Gamepad, or Touch).
    /// </summary>
    [MovedFrom(true, sourceClassName: "InputVisibilityController")]
    public class InputVisibilityController : MonoBehaviour
    {
        public enum VisibilityMode
        {
            // Backwards compatibility values
            ShowOnTouchHideOtherwise = 0,
            HideOnTouchShowOtherwise = 1,
            AlwaysShow = 2,
            AlwaysHide = 3,

            // Granular controller type options
            ShowOnMouseOnly = 10,
            HideOnMouseOnly = 11,
            ShowOnJoystickOnly = 12,
            HideOnJoystickOnly = 13,
            ShowOnTouchOnly = 14,
            HideOnTouchOnly = 15,
            
            // Common combinations
            ShowOnMouseOrKeyboardHideOnJoystickOrTouch = 20,
            ShowOnJoystickOrTouchHideOnMouse = 21,
            ShowOnJoystickHideOnMouseOrTouch = 22
        }

        public enum TargetAction
        {
            [Tooltip("Activates or deactivates the GameObject.")]
            ToggleGameObject = 0,

            [Tooltip("Enables or disables interactability on a Selectable (Button, Toggle, Slider, etc.).")]
            ToggleSelectableInteractable = 1,

            [Tooltip("Adjusts alpha and raycast blocking on a CanvasGroup.")]
            ToggleCanvasGroup = 2
        }

        [Header("Configuration")]
        [Tooltip("Visibility behavior based on the current active input device")]
        [SerializeField] private VisibilityMode _visibilityMode = VisibilityMode.HideOnTouchShowOtherwise;

        [Tooltip("What action to apply when visibility condition changes")]
        [SerializeField] private TargetAction _targetAction = TargetAction.ToggleGameObject;

        [Tooltip("Optional target Selectable (Toggle, Button, etc.). If not assigned, will search on this GameObject.")]
        [SerializeField] private Selectable _targetSelectable;

        [Tooltip("Optional target CanvasGroup. If not assigned, will search on this GameObject.")]
        [SerializeField] private CanvasGroup _targetCanvasGroup;

        [Tooltip("Update immediately on Start")]
        [SerializeField] private bool _shouldUpdateOnStart = true;

        [Header("Events")]
        [Tooltip("Event fired when visibility state is recalculated. Passes the calculated shouldShow boolean.")]
        [SerializeField] private UnityEvent<bool> _onVisibilityChanged;

        private void Awake()
        {
            if (_targetAction == TargetAction.ToggleSelectableInteractable && _targetSelectable == null)
                _targetSelectable = GetComponent<Selectable>();

            if (_targetAction == TargetAction.ToggleCanvasGroup && _targetCanvasGroup == null)
                _targetCanvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            RewiredInputManager.RegisterVisibilityController(this);
            UpdateVisibility();
        }

        private void Start()
        {
            if (_shouldUpdateOnStart) UpdateVisibility();
        }

        private void OnDisable()
        {
            RewiredInputManager.UnregisterVisibilityController(this);
        }

        private void OnDestroy()
        {
            RewiredInputManager.UnregisterVisibilityController(this);
            _onVisibilityChanged.RemoveAllListeners();
        }

        public void UpdateVisibility()
        {
            bool isTouch = RewiredInputManager.IsUsingTouch;
            ControllerType currentType = RewiredInputManager.Instance != null
                ? RewiredInputManager.Instance.CurrentControllerType
                : (isTouch ? ControllerType.Custom : ControllerType.Keyboard);

            bool shouldShow = CalculateVisibility(isTouch, currentType);

            ApplyTargetAction(shouldShow);
            _onVisibilityChanged?.Invoke(shouldShow);
        }

        public void SetVisibilityMode(VisibilityMode newMode)
        {
            _visibilityMode = newMode;
            UpdateVisibility();
        }

        public void SetTargetAction(TargetAction newAction)
        {
            _targetAction = newAction;
            UpdateVisibility();
        }

        private void ApplyTargetAction(bool shouldShow)
        {
            switch (_targetAction)
            {
                case TargetAction.ToggleGameObject:
                    if (gameObject.activeSelf != shouldShow)
                        gameObject.SetActive(shouldShow);
                    break;

                case TargetAction.ToggleSelectableInteractable:
                    if (_targetSelectable == null) _targetSelectable = GetComponent<Selectable>();
                    if (_targetSelectable != null)
                        _targetSelectable.interactable = shouldShow;
                    break;

                case TargetAction.ToggleCanvasGroup:
                    if (_targetCanvasGroup == null) _targetCanvasGroup = GetComponent<CanvasGroup>();
                    if (_targetCanvasGroup != null)
                    {
                        _targetCanvasGroup.alpha = shouldShow ? 1f : 0f;
                        _targetCanvasGroup.interactable = shouldShow;
                        _targetCanvasGroup.blocksRaycasts = shouldShow;
                    }
                    break;
            }
        }

        private bool CalculateVisibility(bool isTouch, ControllerType currentType)
        {
            bool isJoystick = currentType == ControllerType.Joystick;
            bool isMouseOrKeyboard = !isTouch && (currentType == ControllerType.Mouse || currentType == ControllerType.Keyboard);

            return _visibilityMode switch
            {
                VisibilityMode.ShowOnTouchHideOtherwise => isTouch,
                VisibilityMode.HideOnTouchShowOtherwise => !isTouch,
                VisibilityMode.AlwaysShow => true,
                VisibilityMode.AlwaysHide => false,

                VisibilityMode.ShowOnMouseOnly => isMouseOrKeyboard,
                VisibilityMode.HideOnMouseOnly => !isMouseOrKeyboard,

                VisibilityMode.ShowOnJoystickOnly => isJoystick,
                VisibilityMode.HideOnJoystickOnly => !isJoystick,

                VisibilityMode.ShowOnTouchOnly => isTouch,
                VisibilityMode.HideOnTouchOnly => !isTouch,

                VisibilityMode.ShowOnMouseOrKeyboardHideOnJoystickOrTouch => isMouseOrKeyboard,
                VisibilityMode.ShowOnJoystickOrTouchHideOnMouse => isJoystick || isTouch,
                VisibilityMode.ShowOnJoystickHideOnMouseOrTouch => isJoystick,

                _ => false
            };
        }
    }
}
