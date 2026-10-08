using System;
using System.IO;

using Rewired;

using TMPro;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// One reusable "On-Screen Keyboard" panel shared by the RewiredInputManager Inspector and the Dashboard, so the
    /// state, the create action and the Play Mode simulation live in a single place. It reports the keyboard status
    /// even when the open scene has no <see cref="TMP_InputField"/> (common with a persistent Main/bootstrap scene whose
    /// forms live in prefabs), which is exactly the case that used to show nothing.
    /// </summary>
    internal sealed class RewiredOnScreenKeyboardPanel
    {
        private static int? _cachedPrefabInputFields;

        private readonly Action _onChanged;

        public VisualElement Root { get; } = new VisualElement();

        public RewiredOnScreenKeyboardPanel(Action onChanged = null)
        {
            _onChanged = onChanged;
            Refresh();
        }

        /// <summary>Short state for a section title: "ready", "missing" or "no input fields".</summary>
        public static string StateLabel()
        {
            var state = Evaluate();
            if (state.HasKeyboard) return "ready";
            return state.NeedsKeyboard ? "missing" : "no input fields";
        }

        /// <summary>True when a gamepad / Steam Deck player could not type in this project (open scene or scanned prefabs).</summary>
        public static bool IsMissing()
        {
            var state = Evaluate();
            return state.NeedsKeyboard && !state.HasKeyboard;
        }

        public void Refresh()
        {
            Root.Clear();

            var state = Evaluate();
            Root.Add(BuildStatus(state));

            var actions = RewiredInspectorWidgets.CreateChipRow();
            if (!state.HasKeyboard)
            {
                actions.Add(CreateButton("⌨ Create On-Screen Keyboard", CreateKeyboard,
                    "Builds a plain QWERTY + digits keyboard from code and registers it; shown automatically for any TMP_InputField selected with a gamepad."));
            }
            else
            {
                actions.Add(CreateButton("Select keyboard", () => SelectKeyboard(state.Keyboard), "Ping the keyboard in the Hierarchy."));
            }

            actions.Add(CreateButton("🔍 Scan project prefabs", ScanPrefabs,
                "Counts TMP_InputField components in project prefabs, for forms that are not in the open scene."));
            Root.Add(actions);

            if (EditorApplication.isPlaying)
                Root.Add(BuildSimulation());
        }

        private static VisualElement BuildStatus(State state)
        {
            string message;
            AuditSeverity severity;

            if (state.HasKeyboard)
            {
                severity = AuditSeverity.Pass;
                message = state.SceneInputFields > 0
                    ? $"On-screen keyboard found: covers all {state.SceneInputFields} TMP_InputField(s) in this scene."
                    : "On-screen keyboard found in this scene. It covers any TMP_InputField selected with a gamepad.";
            }
            else if (state.SceneInputFields > 0)
            {
                severity = AuditSeverity.Warning;
                message = $"{state.SceneInputFields} TMP_InputField(s) in this scene but no RewiredOnScreenKeyboard: a gamepad / Steam Deck player has no way to type.";
            }
            else if (state.PrefabInputFields > 0)
            {
                severity = AuditSeverity.Warning;
                message = $"No keyboard in this scene, but {state.PrefabInputFields} prefab(s) in the project contain a TMP_InputField. " +
                          "Put the keyboard in the scene that shows them (e.g. the persistent Main/bootstrap scene).";
            }
            else
            {
                severity = AuditSeverity.Info;
                message = state.PrefabInputFields.HasValue
                    ? "No TMP_InputField found in this scene or in project prefabs: nothing to type into."
                    : "No TMP_InputField in this scene. If your forms live in prefabs or other scenes, press \"Scan project prefabs\" to check.";
            }

            return RewiredHelperUIStyle.CreateCallout(message, severity);
        }

        private VisualElement BuildSimulation()
        {
            var row = RewiredInspectorWidgets.CreateChipRow();
            var current = RewiredInputManager.DebugForceControllerType;
            row.Add(new Label(current.HasValue ? $"🧪 Simulating: {current.Value}" : "🧪 Simulate text-input device:")
                { style = { marginRight = 6, unityTextAlign = TextAnchor.MiddleLeft } });
            row.Add(CreateButton("Gamepad (shows keyboard)", () => Simulate(ControllerType.Joystick)));
            row.Add(CreateButton("Touch", () => Simulate(ControllerType.Custom)));
            row.Add(CreateButton("Stop simulating", () => Simulate(null)));
            return row;
        }

        private void Simulate(ControllerType? type)
        {
            RewiredInputManager.DebugForceControllerType = type;
            Changed();
        }

        private void CreateKeyboard()
        {
            DefaultSetupGenerator.CreateOnScreenKeyboardAndWire();
            Changed();
        }

        private static void SelectKeyboard(Wagenheimer.RewiredHelper.UI.RewiredOnScreenKeyboard keyboard)
        {
            if (keyboard == null) return;

            Selection.activeObject = keyboard.gameObject;
            EditorGUIUtility.PingObject(keyboard.gameObject);
        }

        private void ScanPrefabs()
        {
            _cachedPrefabInputFields = CountPrefabsWithInputField();
            Changed();
        }

        private void Changed()
        {
            Refresh();
            _onChanged?.Invoke();
        }

        private static Button CreateButton(string text, Action onClick, string tooltip = null)
        {
            var button = RewiredHelperUIStyle.CreateButton(text, onClick);
            button.style.marginLeft = 0;
            button.style.marginRight = 6;
            button.tooltip = tooltip;
            return button;
        }

        #region State

        private readonly struct State
        {
            public readonly Wagenheimer.RewiredHelper.UI.RewiredOnScreenKeyboard Keyboard;
            public readonly int SceneInputFields;
            public readonly int? PrefabInputFields;

            public State(Wagenheimer.RewiredHelper.UI.RewiredOnScreenKeyboard keyboard, int sceneInputFields, int? prefabInputFields)
            {
                Keyboard = keyboard;
                SceneInputFields = sceneInputFields;
                PrefabInputFields = prefabInputFields;
            }

            public bool HasKeyboard => Keyboard != null;
            public bool NeedsKeyboard => SceneInputFields > 0 || PrefabInputFields > 0;
        }

        private static State Evaluate() => new State(
            DefaultSetupGenerator.FindOnScreenKeyboardInScene(),
            RewiredHelperAudit.FindAll<TMP_InputField>().Count,
            _cachedPrefabInputFields);

        /// <summary>Text scan for the TMP_InputField script GUID in project prefabs: far cheaper than loading every prefab.</summary>
        private static int CountPrefabsWithInputField()
        {
            var scriptGuid = FindInputFieldScriptGuid();
            if (string.IsNullOrEmpty(scriptGuid)) return 0;

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            var count = 0;
            try
            {
                for (var i = 0; i < prefabGuids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar("Scanning prefabs for TMP_InputField", path, (float)i / prefabGuids.Length))
                        break;

                    if (File.ReadAllText(path).Contains(scriptGuid))
                        count++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return count;
        }

        private static string FindInputFieldScriptGuid()
        {
            foreach (var guid in AssetDatabase.FindAssets("TMP_InputField t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == typeof(TMP_InputField))
                    return guid;
            }

            return null;
        }

        #endregion
    }
}
