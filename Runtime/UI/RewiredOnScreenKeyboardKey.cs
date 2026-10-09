using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// Optional helper for a single key <see cref="Button"/> so its <c>OnClick</c> can call a parameterless
    /// method, which is what Unity's serialized persistent-listener system needs — <see cref="RewiredOnScreenKeyboard.WriteKey"/>
    /// takes a <c>TMP_Text</c> argument that cannot be wired as a persistent listener directly. Not required if
    /// you call <see cref="RewiredOnScreenKeyboard.WriteKey"/>/<see cref="RewiredOnScreenKeyboard.WriteSpecialKey"/>
    /// yourself from your own key script.
    ///
    /// It also gives the key its gamepad feedback: the selected key is tinted with
    /// <see cref="RewiredOnScreenKeyboard.selectedColor"/> (so the player can see where the D-pad / stick is), and the
    /// Cancel button (B / Circle) closes the keyboard.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RewiredOnScreenKeyboardKey : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ISubmitHandler
    {
        public RewiredOnScreenKeyboard keyboard;

        [Tooltip("The label typed for a character key. Ignored for a special key.")]
        public TMP_Text label;

        [Tooltip("-1 = character key (types Label's text). 0+ = a special key; see RewiredOnScreenKeyboard.WriteSpecialKey.")]
        public int specialKey = -1;

        // One physical press can reach the key more than once: the virtual cursor's click, the module's Submit and the
        // RewiredInputManager submit bridge all activate the selected button. A real player cannot press a key twice this fast.
        private const float DuplicatePressWindow = 0.12f;

        private float _lastPressTime = float.NegativeInfinity;
        private Image _image;
        private Color _baseColor = Color.white;
        private bool _hasBaseColor;
        private bool _isSelected;

        public void Press()
        {
            if (keyboard == null) return;

            var now = Time.unscaledTime;
            var sinceLast = now - _lastPressTime;
            if (sinceLast < DuplicatePressWindow)
            {
                keyboard.DebugLog($"key '{name}' Press IGNORED (duplicate {sinceLast * 1000f:F0} ms after the previous one)");
                return;
            }

            _lastPressTime = now;
            keyboard.DebugLog($"key '{name}' Press ACCEPTED");

            if (specialKey >= 0) keyboard.WriteSpecialKey(specialKey);
            else keyboard.WriteKey(label);
        }

        /// <summary>The resting colour of the key. The selection tint is layered on top and removed when the key is deselected.</summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            _hasBaseColor = true;
            ApplyColor();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _isSelected = true;
            ApplyColor();
            Trace("Select");
        }

        // The four handlers below only trace which input path reaches the key (Button.onClick still does the work):
        // a mouse/cursor click gives PointerDown + PointerUp + PointerClick, a gamepad Submit gives Submit.
        public void OnPointerDown(PointerEventData eventData) => Trace($"PointerDown {Describe(eventData)}");

        public void OnPointerUp(PointerEventData eventData) => Trace($"PointerUp {Describe(eventData)}");

        public void OnPointerClick(PointerEventData eventData) => Trace($"PointerClick {Describe(eventData)}");

        public void OnSubmit(BaseEventData eventData) => Trace("Submit");

        private void Trace(string what)
        {
            if (keyboard != null) keyboard.DebugLog($"key '{name}' {what}");
        }

        private static string Describe(PointerEventData eventData) =>
            eventData == null ? "-" : $"[{eventData.GetType().Name} button={eventData.button} id={eventData.pointerId} pos={eventData.position}]";

        public void OnDeselect(BaseEventData eventData)
        {
            _isSelected = false;
            ApplyColor();
            Trace("Deselect");
        }

        /// <summary>B / Circle on any key closes the keyboard and returns to the field.</summary>
        public void OnCancel(BaseEventData eventData)
        {
            if (keyboard != null) keyboard.Close();
        }

        private void OnDisable()
        {
            _isSelected = false;
            ApplyColor();
        }

        private void ApplyColor()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image == null) return;

            // Remember whatever colour the key had the first time we touch it, so a keyboard themed by hand keeps its look.
            if (!_hasBaseColor)
            {
                _baseColor = _image.color;
                _hasBaseColor = true;
            }

            _image.color = _isSelected && keyboard != null ? (Color)keyboard.selectedColor : _baseColor;
        }
    }
}
