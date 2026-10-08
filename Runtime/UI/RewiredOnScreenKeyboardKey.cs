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
    public class RewiredOnScreenKeyboardKey : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler
    {
        public RewiredOnScreenKeyboard keyboard;

        [Tooltip("The label typed for a character key. Ignored for a special key.")]
        public TMP_Text label;

        [Tooltip("-1 = character key (types Label's text). 0+ = a special key; see RewiredOnScreenKeyboard.WriteSpecialKey.")]
        public int specialKey = -1;

        private Image _image;
        private Color _restoreColor;
        private bool _isHighlighted;

        public void Press()
        {
            if (keyboard == null) return;

            if (specialKey >= 0) keyboard.WriteSpecialKey(specialKey);
            else keyboard.WriteKey(label);
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (keyboard == null) return;

            if (_image == null) _image = GetComponent<Image>();
            if (_image == null || _isHighlighted) return;

            _restoreColor = _image.color;
            _image.color = keyboard.selectedColor;
            _isHighlighted = true;
        }

        public void OnDeselect(BaseEventData eventData) => RestoreColor();

        /// <summary>B / Circle on any key closes the keyboard and returns to the field.</summary>
        public void OnCancel(BaseEventData eventData)
        {
            if (keyboard != null) keyboard.Close();
        }

        private void OnDisable() => RestoreColor();

        private void RestoreColor()
        {
            if (!_isHighlighted || _image == null) return;

            _image.color = _restoreColor;
            _isHighlighted = false;
        }
    }
}
