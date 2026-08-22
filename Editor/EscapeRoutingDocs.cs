using UnityEditor;
using UnityEngine;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Draws a HelpBox on EscapeButton and ReturnEscapeEvent inspectors explaining the two
    /// components, how they differ and the full Escape/Back resolution order, so the system
    /// is self-documenting directly in the Inspector.
    /// </summary>
    [CustomEditor(typeof(EscapeButton))]
    internal class EscapeButtonInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "EscapeButton: fires a Button (or a custom UnityEvent) when Escape/Back is pressed.\n\n" +
                "Resolution order for a single press:\n" +
                "1. Top modal's EscapeButton (via your IModalStackProvider)\n" +
                "2. Highest-Priority active EscapeButton (this component)\n" +
                "3. Highest-Priority active ReturnEscapeEvent\n\n" +
                "Use this component when the Back press should 'click' something (close a panel, " +
                "toggle a menu). Only the highest-Priority active instance fires.",
                MessageType.Info);
            base.OnInspectorGUI();
        }
    }

    [CustomEditor(typeof(ReturnEscapeEvent))]
    internal class ReturnEscapeEventInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "ReturnEscapeEvent: generic UnityEvents (ReturnEvent / EscapeEvent) fired when " +
                "Return/OK or Escape/Back is pressed and nothing above it in the resolution order " +
                "consumed the press.\n\n" +
                "Resolution order for a single press:\n" +
                "1. Top modal's EscapeButton (via your IModalStackProvider)\n" +
                "2. Highest-Priority active EscapeButton\n" +
                "3. Highest-Priority active ReturnEscapeEvent (this component)\n\n" +
                "Priority: when several ReturnEscapeEvents are active at once, ONLY the one with " +
                "the highest Priority fires. Registration is driven by OnEnable/OnDisable, so put " +
                "this component on the panel's own GameObject: it competes only while the panel is " +
                "open, and once the panel closes itself the next press falls through to the " +
                "lower-priority handler (e.g. the main menu).",
                MessageType.Info);
            base.OnInspectorGUI();
        }
    }
}
