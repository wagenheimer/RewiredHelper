using UnityEditor;
using UnityEngine;
using Wagenheimer.RewiredHelper;

namespace Wagenheimer.RewiredHelper.Editor
{
    [CustomEditor(typeof(InputVisibilityController))]
    [CanEditMultipleObjects]
    public class InputVisibilityControllerEditor : UnityEditor.Editor
    {
        private static Color ColBg => EditorGUIUtility.isProSkin
            ? new(0.16f, 0.16f, 0.18f) : new(0.82f, 0.82f, 0.84f);
        private static Color ColCard => EditorGUIUtility.isProSkin
            ? new(0.20f, 0.20f, 0.22f) : new(0.90f, 0.90f, 0.92f);
        private static Color ColGreen => EditorGUIUtility.isProSkin
            ? new(0.20f, 0.75f, 0.35f) : new(0.10f, 0.55f, 0.20f);
        private static Color ColOrange => EditorGUIUtility.isProSkin
            ? new(1.00f, 0.60f, 0.10f) : new(0.85f, 0.50f, 0.05f);
        private static readonly Color ColAccent = new(0.22f, 0.60f, 1.00f);
        private static readonly Color ColDim = new(0.55f, 0.55f, 0.60f);

        private SerializedProperty _visibilityMode;
        private SerializedProperty _targetAction;
        private SerializedProperty _targetSelectable;
        private SerializedProperty _targetCanvasGroup;
        private SerializedProperty _shouldUpdateOnStart;
        private SerializedProperty _onVisibilityChanged;

        private void OnEnable()
        {
            _visibilityMode = serializedObject.FindProperty("_visibilityMode");
            _targetAction = serializedObject.FindProperty("_targetAction");
            _targetSelectable = serializedObject.FindProperty("_targetSelectable");
            _targetCanvasGroup = serializedObject.FindProperty("_targetCanvasGroup");
            _shouldUpdateOnStart = serializedObject.FindProperty("_shouldUpdateOnStart");
            _onVisibilityChanged = serializedObject.FindProperty("_onVisibilityChanged");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(6);

            DrawGroup("Input Rule & Trigger", "🎮", ColAccent, () =>
            {
                EditorGUILayout.PropertyField(_visibilityMode, new GUIContent("Visibility Rule", "When should the target be shown/active?"));
                
                // Explanatory hint for the selected mode
                DrawModeDescription((InputVisibilityController.VisibilityMode)_visibilityMode.intValue);

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(_shouldUpdateOnStart, new GUIContent("Update On Start", "Evaluates and applies the visibility rule immediately on Start."));
            });

            EditorGUILayout.Space(6);

            DrawGroup("Action & Target", "🎯", ColOrange, () =>
            {
                EditorGUILayout.PropertyField(_targetAction, new GUIContent("Target Action", "How should the element react when the rule matches?"));

                var action = (InputVisibilityController.TargetAction)_targetAction.intValue;
                switch (action)
                {
                    case InputVisibilityController.TargetAction.ToggleGameObject:
                        DrawHintBox("💡 Sets gameObject.SetActive(true/false) based on the active input device.");
                        break;

                    case InputVisibilityController.TargetAction.ToggleSelectableInteractable:
                        EditorGUILayout.PropertyField(_targetSelectable, new GUIContent("Selectable Component", "Button, Toggle, Slider, etc. (Leave empty to auto-detect on this GameObject)."));
                        DrawHintBox("💡 Keeps the UI element visible on screen, but enables/disables its interactability (e.g. graying out mouse-only options when using a gamepad).");
                        break;

                    case InputVisibilityController.TargetAction.ToggleCanvasGroup:
                        EditorGUILayout.PropertyField(_targetCanvasGroup, new GUIContent("CanvasGroup", "CanvasGroup to adjust alpha and raycasts (Leave empty to auto-detect)."));
                        DrawHintBox("💡 Controls alpha (1.0 / 0.0) and blocksRaycasts/interactable on the CanvasGroup.");
                        break;
                }
            });

            EditorGUILayout.Space(6);

            DrawGroup("Events", "⚡", ColGreen, () =>
            {
                EditorGUILayout.PropertyField(_onVisibilityChanged, new GUIContent("On Visibility Evaluated", "Fired whenever the active input device changes, passing whether the rule matched."));
            });

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(6);
                DrawLiveStatus();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = ColAccent }
            };
            EditorGUILayout.LabelField("🎛️ Dynamic Input Visibility Controller", headerStyle);
            var subStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColDim } };
            EditorGUILayout.LabelField("Adapts UI state (active, interactable, canvas group) based on Mouse, Gamepad, or Touch.", subStyle);
        }

        private void DrawModeDescription(InputVisibilityController.VisibilityMode mode)
        {
            string description = mode switch
            {
                InputVisibilityController.VisibilityMode.ShowOnMouseOrKeyboardHideOnJoystickOrTouch =>
                    "🖥️ Visible ONLY with Mouse/Keyboard. Hidden/Disabled when playing with Gamepad or Touch (ideal for Custom Cursor / Mouse Sensitivity settings).",
                
                InputVisibilityController.VisibilityMode.ShowOnJoystickOnly =>
                    "🎮 Visible ONLY with Gamepad/Joystick. Hidden/Disabled on Mouse/Keyboard and Touch.",
                
                InputVisibilityController.VisibilityMode.HideOnJoystickOnly =>
                    "🚫 Hidden/Disabled ONLY when using Gamepad/Joystick.",

                InputVisibilityController.VisibilityMode.ShowOnTouchHideOtherwise =>
                    "📱 Visible ONLY when using Touch screen (ideal for virtual On-Screen D-Pads and touch buttons).",

                InputVisibilityController.VisibilityMode.HideOnTouchShowOtherwise =>
                    "🖥️🎮 Hidden ONLY on Touch screens. Visible for both PC Mouse/Keyboard and Gamepad.",

                InputVisibilityController.VisibilityMode.ShowOnJoystickOrTouchHideOnMouse =>
                    "🎮📱 Visible for Gamepad and Touch. Hidden on PC Mouse/Keyboard.",

                InputVisibilityController.VisibilityMode.ShowOnJoystickHideOnMouseOrTouch =>
                    "🎮 Visible strictly for Gamepad/Joystick.",

                InputVisibilityController.VisibilityMode.ShowOnMouseOnly =>
                    "🖱️ Visible ONLY when active controller is Mouse/Keyboard.",

                InputVisibilityController.VisibilityMode.HideOnMouseOnly =>
                    "🚫 Hidden ONLY when active controller is Mouse/Keyboard.",

                InputVisibilityController.VisibilityMode.AlwaysShow =>
                    "✅ Always visible/interactable regardless of input device.",

                InputVisibilityController.VisibilityMode.AlwaysHide =>
                    "❌ Always hidden/disabled.",

                _ => "Custom visibility evaluation."
            };

            DrawHintBox(description);
        }

        private void DrawLiveStatus()
        {
            var r = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), ColCard);
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, 3, r.height + 4), ColGreen);
            GUILayout.Space(4);

            string currentInput = RewiredInputManager.IsUsingTouch
                ? "Touch Screen (Custom)"
                : (RewiredInputManager.Instance != null ? RewiredInputManager.Instance.CurrentControllerType.ToString() : "Unknown");

            EditorGUILayout.LabelField("Live Input State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Active Device: {currentInput}", EditorStyles.miniLabel);

            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        private void DrawGroup(string title, string icon, Color accent, System.Action content)
        {
            var r = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), ColCard);
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, 3, r.height + 4), accent);
            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = accent } };
            EditorGUILayout.LabelField($"{icon}  {title}", titleStyle);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical();

            content?.Invoke();

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        private void DrawHintBox(string text)
        {
            var style = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                wordWrap = true
            };
            EditorGUILayout.LabelField(text, style);
        }
    }
}
