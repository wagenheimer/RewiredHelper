using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.Editor
{
    [CustomEditor(typeof(RewiredInputManager))]
    public class RewiredInputManagerEditor : UnityEditor.Editor
    {
        private static bool showHelpFoldout = true;
        private static bool showBootstrapHelp = false;
        private static bool showRoutingHelp = false;
        private static bool showApisHelp = false;

        private static readonly string GlyphHelperTypeName = "Rewired.Glyphs.UnityUI.UnityUITextMeshProGlyphHelper";

        private static Color ColBg => EditorGUIUtility.isProSkin
            ? new(0.16f, 0.16f, 0.18f) : new(0.82f, 0.82f, 0.84f);
        private static Color ColCard => EditorGUIUtility.isProSkin
            ? new(0.20f, 0.20f, 0.22f) : new(0.90f, 0.90f, 0.92f);
        private static Color ColGreen => EditorGUIUtility.isProSkin
            ? new(0.20f, 0.75f, 0.35f) : new(0.10f, 0.55f, 0.20f);
        private static Color ColRed => EditorGUIUtility.isProSkin
            ? new(0.85f, 0.25f, 0.20f) : new(0.70f, 0.15f, 0.10f);
        private static Color ColOrange => EditorGUIUtility.isProSkin
            ? new(1.00f, 0.60f, 0.10f) : new(0.85f, 0.50f, 0.05f);
        private static readonly Color ColAccent = new(0.22f, 0.60f, 1.00f);
        private static readonly Color ColDim = new(0.55f, 0.55f, 0.60f);

        public override void OnInspectorGUI()
        {
            var manager = (RewiredInputManager)target;

            serializedObject.Update();

            // Draw Script field (read-only)
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target), typeof(MonoScript), false);
            GUI.enabled = true;

            EditorGUILayout.Space(5);

            // ==========================================
            // SETTINGS GROUPS (MANUAL DRAWING)
            // ==========================================
            
            // 0. Initialization & Boot
            DrawSettingsGroup("Initialization & Boot", "🚀", new[] {
                serializedObject.FindProperty("AutoConfigureOnStart"),
                serializedObject.FindProperty("UseDefaultModalStack")
            }, new[] {
                new GUIContent("Auto Configure On Start", "If checked, the manager will automatically configure itself on Start, removing the need to call Configure() from code."),
                new GUIContent("Use Default Modal Stack", "Automatically uses the built-in ModalDialogStack provider for UI modals navigation (Escape/Return keys).")
            }, ColAccent);

            // 1. Cursor & Visuals Settings
            DrawSettingsGroup("Cursor & Visuals", "🖱️", new[] {
                serializedObject.FindProperty("GameCursor")
            }, new[] {
                new GUIContent("Game Cursor", "UI Image used to render the custom in-game cursor.")
            }, ColAccent);

            // 2. Pause & Steam Overlay
            var steamProp = serializedObject.FindProperty("IsSteamOverlayActive");
            DrawSettingsGroup("Pause & Steam Overlay", "🎮", new[] {
                serializedObject.FindProperty("GamePaused"),
                serializedObject.FindProperty("PauseOnSteamOverlay"),
                steamProp
            }, new[] {
                new GUIContent("Game Paused", "GameObject toggled when the game pauses."),
                new GUIContent("Pause On Steam Overlay", "Automatically pauses the game when Steam overlay opens."),
                new GUIContent("Is Steam Overlay Active", "Indicates if the Steam overlay is currently active.")
            }, ColAccent);

            // 3. Controller Help Events
            DrawSettingsGroup("Controller Help", "❔", new[] {
                serializedObject.FindProperty("alreadyShowedControllerHelp"),
                serializedObject.FindProperty("OnShowControllerHelp")
            }, new[] {
                new GUIContent("Already Showed Help", "Tracks if the player was already shown the controller help form."),
                new GUIContent("On Show Controller Help", "Event triggered once the first time physical controller input is detected.")
            }, ColAccent);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            DrawSeparator();
            EditorGUILayout.Space(5);

            // ==========================================
            // SECTION 1: QUICK HELP GUIDE & DOCUMENTATION
            // ==========================================
            showHelpFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(showHelpFoldout, "📖 QUICK HELP GUIDE");
            if (showHelpFoldout)
            {
                EditorGUI.indentLevel++;

                showBootstrapHelp = EditorGUILayout.Foldout(showBootstrapHelp, "How to Initialize (Bootstrap)");
                if (showBootstrapHelp)
                {
                    EditorGUILayout.HelpBox(
                        "To initialize the manager, call Configure() in your bootstrap script (e.g. in Awake):\n\n" +
                        "RewiredInputManager.Instance.Configure(\n" +
                        "    uiBlocker: yourUiBlocker,\n" +
                        "    modalStack: yourModalStack,\n" +
                        "    controllerHelpGate: yourHelpGate\n" +
                        ");\n\n" +
                        "All arguments are optional and will assume safe default behaviors if omitted.",
                        MessageType.Info
                    );
                }

                showRoutingHelp = EditorGUILayout.Foldout(showRoutingHelp, "Escape & Return Routing");
                if (showRoutingHelp)
                {
                    EditorGUILayout.HelpBox(
                        "• EscapeButton: Attach to any UI button. The active button with the highest priority will respond to the Escape key (or Back/Menu buttons on controllers).\n\n" +
                        "• ReturnEscapeEvent: Fires generic events when Escape or Return are pressed and no button or modal dialog claims the key first.\n\n" +
                        "• IModalStackProvider: Register your own modal stack so that its top-most modal automatically gains routing priority.",
                        MessageType.Info
                    );
                }

                showApisHelp = EditorGUILayout.Foldout(showApisHelp, "Core APIs for your Code");
                if (showApisHelp)
                {
                    EditorGUILayout.HelpBox(
                        "• RewiredInputManager.IsUsingTouch: true if the player touched the screen recently.\n\n" +
                        "• manager.CurrentControllerType: Returns whether the player is using Mouse/Keyboard, Joystick (Controller), or Custom (Touch).\n\n" +
                        "• RewiredInputManager.OnInputTypeChanged: Static event fired when the active input source changes (e.g. to swap controller UI glyphs).\n\n" +
                        "• manager.Vibrate(): Fires controller rumble on the active controller for Player 0.",
                        MessageType.Info
                    );
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(15);
            DrawSeparator();
            EditorGUILayout.Space(5);

            // ==========================================
            // SECTION 2: SETUP DIAGNOSTIC & STATUS CHECKER
            // ==========================================
            var sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = ColAccent } };
            GUILayout.Label("🛠️ SETUP DIAGNOSTIC & STATUS CHECKER", sectionHeaderStyle);
            EditorGUILayout.Space(5);

            // 1. Verify Native Manager in Scene
            var hasRewired = DefaultSetupGenerator.FindInputManagerInScene() != null;
            DrawCheckResult("Rewired Input Manager (Native)", hasRewired, 
                "Instantiate the configured Rewired prefab to manage bindings and controls.",
                "Create Manager", () => DefaultSetupGenerator.CreateRewiredInputManager());

            // 2. Verify Event System
            var hasEventSystem = UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null;
            DrawCheckResult("Rewired Event System", hasEventSystem,
                "An Event System with Rewired support is required for controller UI navigation.",
                "Create Event System", () => DefaultSetupGenerator.CreateRewiredInputManager());

            // 3. Verify Canvas
            var hasCanvas = UnityEngine.Object.FindObjectOfType<Canvas>() != null;
            DrawCheckResult("UI Canvas", hasCanvas,
                "The scene requires a Canvas to render custom cursors and modal dialogs.",
                "Create Canvas", () => {
                    var go = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
                    var canvas = go.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
                });

            // 4. Standalone Custom Cursor warnings
            if (manager.CustomCursorEnabled && manager.CursorTexture == null)
            {
                DrawWarningBox("Standalone Custom Cursor is enabled, but no Cursor Texture has been assigned!");
            }

            if (manager.GameCursor == null)
            {
                DrawHintBox("Hint: Assign a UI Image to the 'Game Cursor' field if you use a screen-space custom cursor.");
            }

            // 5. Verify Runtime Initialization
            if (EditorApplication.isPlaying)
            {
                if (!manager.IsConfigured)
                {
                    DrawErrorBox("Runtime: The Configure() method has not been called in this session. Initialize it in your game's Awake/Start.");
                }
                else
                {
                    DrawSuccessBox("Runtime: Initialized and configured successfully!");
                }
            }

            // 6. Verify Rewired Glyphs Addon
            var glyphHelperType = FindGlyphHelperType();
            if (glyphHelperType == null)
            {
                DrawHintBox("Note: The Rewired Official Glyphs Addon was not detected in the project. If you wish to use dynamic controller icons in help texts, install it via:\nWindow > Rewired > Extras > Glyphs > Install");
            }
            else
            {
                DrawSuccessBox("Rewired Glyphs Addon detected and active!");
            }

            EditorGUILayout.Space(10);
        }

        private void DrawCheckResult(string title, bool pass, string missingDesc, string fixBtnText, Action fixAction)
        {
            var color = pass ? ColGreen : ColRed;
            var r = EditorGUILayout.BeginVertical();
            
            // Render Card with left colored sidebar
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), ColCard);
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, 3, r.height + 4), color);
            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);
            
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11, normal = { textColor = color } };
            var statusIcon = pass ? "✅  " : "❌  ";
            GUILayout.Label($"{statusIcon}{title}", style);

            GUILayout.FlexibleSpace();

            if (!pass && !string.IsNullOrEmpty(fixBtnText) && fixAction != null)
            {
                if (GUILayout.Button(fixBtnText, GUILayout.Width(120), GUILayout.Height(18)))
                {
                    fixAction.Invoke();
                }
            }
            else
            {
                var greenStyle = new GUIStyle(EditorStyles.label) { richText = true };
                GUILayout.Label("<color=green>Ready</color>", greenStyle);
            }
            GUILayout.Space(5);
            EditorGUILayout.EndHorizontal();

            if (!pass && !string.IsNullOrEmpty(missingDesc))
            {
                EditorGUI.indentLevel++;
                var descStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColDim }, wordWrap = true };
                EditorGUILayout.LabelField(missingDesc, descStyle);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }

            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawWarningBox(string msg)
        {
            DrawStatusBox(msg, ColOrange, "⚠️  Warning");
        }

        private void DrawHintBox(string msg)
        {
            DrawStatusBox(msg, ColDim, "💡  Info");
        }

        private void DrawErrorBox(string msg)
        {
            DrawStatusBox(msg, ColRed, "❌  Error");
        }

        private void DrawSuccessBox(string msg)
        {
            DrawStatusBox(msg, ColGreen, "✅  Success");
        }

        private void DrawStatusBox(string msg, Color statusColor, string tag)
        {
            var r = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), ColCard);
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, 3, r.height + 4), statusColor);
            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);
            
            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColDim }, wordWrap = true };
            GUILayout.Label($"<b>{tag}:</b> {msg}", style);
            
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawSettingsGroup(string title, string icon, SerializedProperty[] properties, GUIContent[] labels, Color accentColor)
        {
            GUILayout.Label($"{icon}  {title.ToUpper()}", EditorStyles.boldLabel);
            var r = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), ColCard);
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, 3, r.height + 4), accentColor);
            GUILayout.Space(5);

            EditorGUI.indentLevel++;
            for (int i = 0; i < properties.Length; i++)
            {
                if (properties[i] != null)
                {
                    // Disable editing for IsSteamOverlayActive
                    if (properties[i].name == "IsSteamOverlayActive")
                    {
                        GUI.enabled = false;
                        EditorGUILayout.PropertyField(properties[i], labels[i]);
                        GUI.enabled = true;
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(properties[i], labels[i]);
                    }
                }
            }
            EditorGUI.indentLevel--;

            // Draw custom creation shortcuts
            if (title == "Pause & Steam Overlay" && properties[0].objectReferenceValue == null)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(15);
                if (GUILayout.Button("🛠️  Generate Pause Screen & Link", GUILayout.Height(20)))
                {
                    DefaultSetupGenerator.CreatePauseScreenAndWire((RewiredInputManager)serializedObject.targetObject, serializedObject);
                }
                GUILayout.Space(5);
                EditorGUILayout.EndHorizontal();
            }
            else if (title == "Controller Help")
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(15);
                if (GUILayout.Button("🛠️  Generate Help Form & Wire Event", GUILayout.Height(20)))
                {
                    DefaultSetupGenerator.CreateControllerHelpFormAndWire((RewiredInputManager)serializedObject.targetObject);
                }
                GUILayout.Space(5);
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void DrawSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
        }

        private Type FindGlyphHelperType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(GlyphHelperTypeName);
                if (type != null)
                    return type;
            }
            return null;
        }
    }
}
