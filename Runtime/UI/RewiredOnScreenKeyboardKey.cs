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
        private Color _baseColor = Color.white;
        private bool _hasBaseColor;
        private bool _isSelected;

        public void Press()
        {
            if (keyboard == null) return;

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
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _isSelected = false;
            ApplyColor();
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
