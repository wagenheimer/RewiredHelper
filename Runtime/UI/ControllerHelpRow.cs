using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// Single row in <see cref="ControllerHelpPanel"/>: an optional icon + a text label.
    /// </summary>
    public class ControllerHelpRow : MonoBehaviour
    {
        public TMP_Text Label;
        public Image Icon;

        public void SetLabel(string text)
        {
            if (Label != null)
                Label.text = text;
        }

        /// <summary>
        /// Assigns the Rewired glyph if one was found for this element (a <see cref="Sprite"/> or
        /// <see cref="Texture2D"/>, depending on the project's IGlyphProvider). Rewired only
        /// returns a glyph if a glyph provider is configured AND a glyph asset has been assigned
        /// to this specific element identifier in the Rewired Input Manager — most projects won't
        /// have this set up out of the box, so the icon is hidden (text label alone still works)
        /// until one is.
        /// </summary>
        public void SetGlyph(object glyph)
        {
            if (Icon == null)
                return;

            switch (glyph)
            {
                case Sprite sprite:
                    Icon.sprite = sprite;
                    Icon.enabled = true;
                    break;
                case Texture2D texture:
                    Icon.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    Icon.enabled = true;
                    break;
                default:
                    Icon.enabled = false;
                    break;
            }
        }
    }
}
