using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Toggles this GameObject's active state based on the current input type
    /// (<see cref="RewiredInputManager.IsUsingTouch"/>), e.g. to show touch-only controls.
    /// </summary>
    [MovedFrom(true, sourceClassName: "InputVisibilityController")]
    public class InputVisibilityController : MonoBehaviour
    {
        public enum VisibilityMode
        {
            ShowOnTouchHideOtherwise,
            HideOnTouchShowOtherwise,
            AlwaysShow,
            AlwaysHide
        }

        [Header("Configuration")]
        [Tooltip("Visibility behavior based on the input type")]
        [SerializeField] private VisibilityMode _visibilityMode = VisibilityMode.HideOnTouchShowOtherwise;

        [Tooltip("Update on Start")]
        [SerializeField] private bool _shouldUpdateOnStart = true;

        [Header("Events")]
        [SerializeField] private UnityEvent<bool> _onVisibilityChanged;

        private void Awake() => RewiredInputManager.RegisterVisibilityController(this);

        private void Start()
        {
            if (_shouldUpdateOnStart) UpdateVisibility();
        }

        private void OnDestroy()
        {
            RewiredInputManager.UnregisterVisibilityController(this);
            _onVisibilityChanged.RemoveAllListeners();
        }

        public void UpdateVisibility()
        {
            bool isTouch = RewiredInputManager.IsUsingTouch;
            bool shouldShow = CalculateVisibility(isTouch);

            if (gameObject.activeSelf != shouldShow)
                gameObject.SetActive(shouldShow);

            _onVisibilityChanged?.Invoke(isTouch);
        }

        public void SetVisibilityMode(VisibilityMode newMode)
        {
            _visibilityMode = newMode;
            UpdateVisibility();
        }

        private bool CalculateVisibility(bool isTouch) => _visibilityMode switch
        {
            VisibilityMode.ShowOnTouchHideOtherwise => isTouch,
            VisibilityMode.HideOnTouchShowOtherwise => !isTouch,
            VisibilityMode.AlwaysShow => true,
            _ => false
        };
    }
}
