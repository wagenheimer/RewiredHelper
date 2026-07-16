using UnityEditor;
using UnityEngine;
using Wagenheimer.RewiredHelper.UI;

namespace Wagenheimer.RewiredHelper.Editor
{
    [CustomEditor(typeof(ControllerHelpRowBuilder))]
    public class ControllerHelpRowBuilderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var builder = (ControllerHelpRowBuilder)target;

            // Draw default inspector fields
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            GUILayout.Label("🛠️ Design Time Actions", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (builder.RebuildOnAwake)
            {
                EditorGUILayout.HelpBox("Rebuild On Awake is enabled. Any customization made to the rows at Design Time will be overwritten at runtime when the game starts.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Rebuild On Awake is disabled. You can customize the generated GameObjects below, and your changes will be preserved at runtime.", MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Rows in Editor"))
            {
                Undo.IncrementCurrentGroup();
                int groupIndex = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Generate Help Rows");
                
                builder.Rebuild();
                
                Undo.CollapseUndoOperations(groupIndex);
                EditorUtility.SetDirty(builder.gameObject);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
            }

            if (GUILayout.Button("Clear Rows"))
            {
                Undo.IncrementCurrentGroup();
                int groupIndex = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Clear Help Rows");

                for (int i = builder.transform.childCount - 1; i >= 0; i--)
                {
                    Undo.DestroyObjectImmediate(builder.transform.GetChild(i).gameObject);
                }

                Undo.CollapseUndoOperations(groupIndex);
                EditorUtility.SetDirty(builder.gameObject);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
