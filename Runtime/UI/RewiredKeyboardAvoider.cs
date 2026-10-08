using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// Lifts a form (any <see cref="RectTransform"/>) just enough that its input field stays visible above the soft keyboard.
    /// Works with both keyboards this package deals with: the operating system's keyboard on mobile
    /// (<see cref="TouchScreenKeyboard"/>) and the on-screen keyboard shown for Steam Deck
    /// (<see cref="RewiredOnScreenKeyboard"/>). Put it on the form's root, optionally assign the field; nothing else to wire.
    /// The form is only moved when the keyboard would actually cover the field, and returns to its resting position afterwards.
    /// </summary>
    [DisallowMultipleComponent]
    public class RewiredKeyboardAvoider : MonoBehaviour
    {
        [Tooltip("The form to lift. Defaults to this object's RectTransform.")]
        public RectTransform target;

        [Tooltip("The input field that must stay visible. Defaults to the first TMP_InputField under this object.")]
        public TMP_InputField field;

        [Tooltip("Extra space, in canvas units, kept between the field and the keyboard.")]
        public float padding = 24f;

        [Tooltip("Seconds the form takes to move.")]
        public float smoothTime = 0.15f;

        [Tooltip("Share of the screen height assumed when the OS keyboard does not report its size (some Android devices).")]
        [Range(0.2f, 0.6f)] public float fallbackKeyboardShare = 0.4f;

        private Canvas _canvas;
        private float _restingY;
        private bool _hasRestingY;
        private float _currentLift;
        private float _liftVelocity;

        private void OnEnable()
        {
            if (target == null) target = transform as RectTransform;
            if (field == null) field = GetComponentInChildren<TMP_InputField>(true);
            _canvas = GetComponentInParent<Canvas>();
        }

        private void OnDisable() => Restore();

        private void LateUpdate()
        {
            if (target == null) return;

            var desired = ComputeDesiredLift();
            if (!_hasRestingY && desired <= 0f) return;

            if (!_hasRestingY)
            {
                _restingY = target.anchoredPosition.y;
                _hasRestingY = true;
            }

            _currentLift = Mathf.SmoothDamp(_currentLift, desired, ref _liftVelocity, Mathf.Max(0.01f, smoothTime), Mathf.Infinity, Time.unscaledDeltaTime);

            var position = target.anchoredPosition;
            position.y = _restingY + _currentLift;
            target.anchoredPosition = position;

            if (desired <= 0f && _currentLift < 0.5f)
                Restore();
        }

        /// <summary>The lift, in canvas units, needed to keep the field above whichever keyboard is showing (0 when none or already visible).</summary>
        private float ComputeDesiredLift()
        {
            var keyboardTop = KeyboardTopScreenY();
            if (keyboardTop <= 0f || !TryGetFieldBottomScreenY(out var fieldBottom)) return 0f;

            // Part of the current lift is already included in fieldBottom, so work out the total needed from the resting position.
            var needed = keyboardTop + padding * ScaleFactor() - (fieldBottom - _currentLift * ScaleFactor());
            return Mathf.Max(0f, needed / ScaleFactor());
        }

        /// <summary>Screen y (pixels, from the bottom) of the top edge of the visible soft keyboard, or 0 when there is none.</summary>
        private float KeyboardTopScreenY()
        {
            var onScreen = RewiredInputManager.ActiveOnScreenKeyboard;
            if (onScreen != null)
            {
                var rect = onScreen.transform as RectTransform;
                if (rect != null)
                {
                    var corners = new Vector3[4];
                    rect.GetWorldCorners(corners);
                    var top = float.MinValue;
                    foreach (var corner in corners)
                        top = Mathf.Max(top, RectTransformUtility.WorldToScreenPoint(EventCamera(), corner).y);
                    return top;
                }
            }

            if (TouchScreenKeyboard.visible)
            {
                var height = TouchScreenKeyboard.area.height;
                return height > 1f ? height : Screen.height * fallbackKeyboardShare;
            }

            return 0f;
        }

        private bool TryGetFieldBottomScreenY(out float bottom)
        {
            bottom = 0f;
            var rect = (field != null ? field.transform : target) as RectTransform;
            if (rect == null) return false;

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            bottom = float.MaxValue;
            foreach (var corner in corners)
                bottom = Mathf.Min(bottom, RectTransformUtility.WorldToScreenPoint(EventCamera(), corner).y);
            return true;
        }

        private Camera EventCamera() =>
            _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        private float ScaleFactor() => _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

        private void Restore()
        {
            if (_hasRestingY && target != null)
            {
                var position = target.anchoredPosition;
                position.y = _restingY;
                target.anchoredPosition = position;
            }

            _hasRestingY = false;
            _currentLift = 0f;
            _liftVelocity = 0f;
        }
    }
}
