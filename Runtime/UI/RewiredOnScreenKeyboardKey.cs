using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// Optional helper for a single key <see cref="Button"/> so its <c>OnClick</c> can call a parameterless
    /// method, which is what Unity's serialized persistent-listener system needs — <see cref="RewiredOnScreenKeyboard.WriteKey"/>
    /// takes a <c>TMP_Text</c> argument that cannot be wired as a persistent listener directly. Not required if
    /// you call <see cref="RewiredOnScreenKeyboard.WriteKey"/>/<see cref="RewiredOnScreenKeyboard.WriteSpecialKey"/>
    /// yourself from your own key script.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RewiredOnScreenKeyboardKey : MonoBehaviour
    {
        public RewiredOnScreenKeyboard keyboard;

        [Tooltip("The label typed for a character key. Ignored for a special key.")]
        public TMP_Text label;

        [Tooltip("-1 = character key (types Label's text). 0+ = a special key; see RewiredOnScreenKeyboard.WriteSpecialKey.")]
        public int specialKey = -1;

        public void Press()
        {
            if (keyboard == null) return;

            if (specialKey >= 0) keyboard.WriteSpecialKey(specialKey);
            else keyboard.WriteKey(label);
        }
    }
}
