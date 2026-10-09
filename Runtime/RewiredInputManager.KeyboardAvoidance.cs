using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Zero-setup keyboard avoidance: whenever an input field is selected, the form that contains it gets a
    /// <see cref="UI.RewiredKeyboardAvoider"/> (if it does not have one yet), so any form lifts itself above the soft keyboard
    /// (the OS keyboard on mobile, the on-screen keyboard on Steam Deck) without per-form wiring.
    /// </summary>
    public partial class RewiredInputManager
    {
        [Tooltip("Automatically lift the form that contains the selected input field above the soft keyboard (OS keyboard on mobile, on-screen keyboard on Steam Deck). Add a RewiredKeyboardAvoider by hand to choose the object that moves.")]
        public bool AutoLiftFormsAboveKeyboard = true;

        private const string FormComponentTypeName = "Dialog";

        private GameObject _lastAvoiderSelection;

        private void EnsureKeyboardAvoiderForSelection()
        {
            if (!AutoLiftFormsAboveKeyboard) return;

            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected == _lastAvoiderSelection) return;

            _lastAvoiderSelection = selected;
            var field = selected != null ? selected.GetComponent<TMP_InputField>() : null;
            if (field == null) return;

            var root = FindFormRoot(field.transform);
            if (root == null) return;

            var avoider = root.GetComponent<UI.RewiredKeyboardAvoider>();
            if (avoider == null) avoider = root.gameObject.AddComponent<UI.RewiredKeyboardAvoider>();

            // A form with several fields keeps the one being edited visible.
            avoider.field = field;
        }

        /// <summary>The nearest ancestor that is a dialog/form; otherwise the top-level panel under the Canvas; otherwise the field itself.</summary>
        public static RectTransform FindFormRoot(Transform fieldTransform)
        {
            for (var t = fieldTransform; t != null; t = t.parent)
            {
                foreach (var component in t.GetComponents<MonoBehaviour>())
                {
                    if (component != null && component.GetType().Name == FormComponentTypeName)
                        return t as RectTransform;
                }
            }

            for (var t = fieldTransform; t != null && t.parent != null; t = t.parent)
            {
                if (t.parent.GetComponent<Canvas>() != null)
                    return t as RectTransform;
            }

            return fieldTransform as RectTransform;
        }
    }
}
