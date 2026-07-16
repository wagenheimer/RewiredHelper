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
                serializedObject.FindProperty("GameCursor"),
                serializedObject.FindProperty("CustomCursorEnabled"),
                serializedObject.FindProperty("CursorTexture")
            }, new[] {
                new GUIContent("Game Cursor", "UI Image used to render the custom in-game cursor."),
                new GUIContent("Custom Cursor Enabled", "Enables the standalone OS cursor (Cursor.SetCursor) using Cursor Texture below. Can also be toggled at runtime from your save data."),
                new GUIContent("Cursor Texture", "Texture used by the standalone custom cursor when Custom Cursor Enabled is checked.")
            }, ColAccent);

            // 2. Pause & Steam Overlay
            DrawSettingsGroup("Pause & Steam Overlay", "🎮", new[] {
                serializedObject.FindProperty("GamePaused"),
                serializedObject.FindProperty("PauseOnSteamOverlay")
            }, new[] {
                new GUIContent("Game Paused", "GameObject toggled when the game pauses."),
                new GUIContent("Pause On Steam Overlay", "Automatically pauses the game when Steam overlay opens.")
            }, ColAccent);

            // 3. Controller Help Events
            DrawSettingsGroup("Controller Help", "❔", new[] {
                serializedObject.FindProperty("OnShowControllerHelp")
            }, new[] {
                new GUIContent("On Show Controller Help", "Event triggered once the first time physical controller input is detected.")
            }, ColAccent);

            // 4. Runtime Status & State
            DrawSettingsGroup("Runtime Status & State", "⚙️", new[] {
                serializedObject.FindProperty("alreadyShowedControllerHelp"),
                serializedObject.FindProperty("IsSteamOverlayActive")
            }, new[] {
                new GUIContent("Already Showed Help", "Tracks if the player was already shown the controller help form."),
                new GUIContent("Is Steam Overlay Active", "Indicates if the Steam overlay is currently active.")
            }, ColDim);

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

            // 4. Verify Game Cursor (Required)
            var hasCursor = manager.GameCursor != null;
            DrawCheckResult("Game Cursor", hasCursor,
                "A UI Image is required to act as the visual cursor on screen.",
                "Create Cursor", () => DefaultSetupGenerator.CreateGameCursorAndWire(manager, serializedObject));

            // Standalone Custom Cursor warnings
            if (manager.CustomCursorEnabled && manager.CursorTexture == null)
            {
                DrawWarningBox("Standalone Custom Cursor is enabled, but no Cursor Texture has been assigned!");
            }

            // 4b. Verify Player Mouse (drives the cursor from joystick/controller input)
            DrawPlayerMouseCheck(manager);

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

                var glyphProviderType = DefaultSetupGenerator.FindGlyphProviderType();
                var hasGlyphProvider = glyphProviderType != null && manager.GetComponent(glyphProviderType) != null;
                DrawCheckResult("Glyph Provider", hasGlyphProvider,
                    "Without a Glyph Provider on the Rewired Input Manager, ReInput.glyphs.glyphProvider is never set, so every <rewiredElement> glyph tag falls back to plain text instead of an icon.",
                    "Add Glyph Provider", () => DefaultSetupGenerator.EnsureGlyphProvider(manager.gameObject));
            }

            // 7. Verify I2 Localization Integration
            var i2ManagerType = FindTypeByName("I2.Loc.LocalizationManager");
            if (i2ManagerType != null)
            {
                bool isPartial = false;
                string specPath = "Assets/I2/Localization/Scripts/Configurables/SpecializationManager.cs";
                if (System.IO.File.Exists(specPath))
                {
                    string content = System.IO.File.ReadAllText(specPath);
                    isPartial = content.Contains("partial class SpecializationManager");
                }
                else
                {
                    isPartial = true; // File not found at expected path, assume partial to avoid false alarm
                }

                if (!isPartial)
                {
                    DrawErrorBox("I2 Localization: SpecializationManager.cs is not declared as a 'partial' class. Open '" + specPath + "' and add the 'partial' keyword (e.g. 'public partial class SpecializationManager') so that the Rewired Helper integration can compile.");
                }
                else
                {
                    var hasI2Integration = FindTypeByName("Wagenheimer.RewiredHelper.Integration.I2SpecializationImportedMarker") != null;
                    DrawCheckResult("I2 Localization Integration", hasI2Integration,
                        "I2 Localization detected, but the integration specialization helper is not imported. Import it to enable automatic controller glyphs in localized texts.",
                        "Import Integration", () => ImportI2IntegrationSample());
                }
            }

            EditorGUILayout.Space(10);
        }

        private static void ImportI2IntegrationSample()
        {
            string sourcePath = "Packages/com.wagenheimer.rewiredhelper/Samples~/I2LocalizationIntegration/SpecializationManager.cs";
            string destDir = "Assets/Samples/Rewired Helper/I2 Localization Integration";
            string destPath = System.IO.Path.Combine(destDir, "SpecializationManager.cs");

            try
            {
                if (!System.IO.Directory.Exists(destDir))
                {
                    System.IO.Directory.CreateDirectory(destDir);
                }

                if (System.IO.File.Exists(sourcePath))
                {
                    System.IO.File.Copy(sourcePath, destPath, true);
                    AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);
                    AssetDatabase.Refresh();
                    Debug.Log("[RewiredHelper] Imported I2 Localization Integration successfully!");
                }
                else
                {
                    Debug.LogError($"[RewiredHelper] Integration source file not found at: {sourcePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RewiredHelper] Failed to import I2 Localization Integration: {ex.Message}");
            }
        }

        private static Type FindTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }

        /// <summary>
        /// Checks for Rewired's own Rewired.Components.PlayerMouse — the component that actually
        /// drives cursor/pointer movement from controller input. Only offers the specific action
        /// that's actually missing (create it, or bind its Movement axes) instead of re-offering
        /// to recreate something that already exists.
        /// </summary>
        private void DrawPlayerMouseCheck(RewiredInputManager manager)
        {
            var playerMouseType = DefaultSetupGenerator.FindPlayerMouseType();
            var playerMouseComp = DefaultSetupGenerator.FindPlayerMouseInScene(playerMouseType);

            if (playerMouseComp == null)
            {
                DrawCheckResult("Player Mouse (Joystick Cursor Movement)", false,
                    "Rewired's Player Mouse component drives the OS/UI pointer from controller input. Without it, the cursor shows for a joystick but never moves.",
                    "Create Player Mouse", () => DefaultSetupGenerator.CreatePlayerMouseAndWire(manager));
                return;
            }

            var pmSerialized = new SerializedObject(playerMouseComp);
            var configuredElements = DefaultSetupGenerator.CountConfiguredMouseElements(pmSerialized);

            if (configuredElements == 0)
            {
                DrawCheckResult("Player Mouse — Movement Elements", false,
                    "Player Mouse exists but its Elements list has no axis bound to it, so controller input never reaches it and the cursor stays fixed.",
                    "Auto-Configure Elements", () => DefaultSetupGenerator.ConfigureMouseMovementElements(playerMouseComp));
            }
            else if (configuredElements > 0)
            {
                DrawCheckResult("Player Mouse (Joystick Cursor Movement)", true, null, null, null);

                // Verify Player Mouse Events are wired to GameCursor
                if (manager.GameCursor != null)
                {
                    var positioner = manager.GameCursor.GetComponent<Wagenheimer.RewiredHelper.UI.GameCursorPositioner>();
                    DrawCheckResult("Game Cursor Positioner", positioner != null,
                        "GameCursorPositioner converts the Player Mouse screen position into the Canvas's local space. Without it, the cursor only lines up correctly on a Screen Space - Overlay Canvas with a 1:1 Canvas Scaler.",
                        "Add Positioner", () => manager.GameCursor.gameObject.AddComponent<Wagenheimer.RewiredHelper.UI.GameCursorPositioner>());

                    var onScreenPos = pmSerialized.FindProperty("_onScreenPositionChanged");
                    var onEnabled = pmSerialized.FindProperty("_onEnabledStateChanged");
                    bool posWiredToPositioner = IsEventWiredTo(onScreenPos, positioner, "SetScreenPosition");
                    bool enabledWired = IsEventWired(onEnabled);

                    if (!posWiredToPositioner || !enabledWired)
                    {
                        var reason = !posWiredToPositioner
                            ? "On Screen Position Changed isn't wired to GameCursorPositioner.SetScreenPosition (it may still be bound directly to RectTransform.anchoredPosition, which only works on a Screen Space - Overlay Canvas with a 1:1 Canvas Scaler — otherwise the cursor drifts off-screen)."
                            : "On Enabled State Changed is not wired to control the Game Cursor's visibility.";

                        DrawCheckResult("Player Mouse Events — Wiring", false, reason,
                            "Fix Wiring", () => DefaultSetupGenerator.WirePlayerMouseEvents(manager, playerMouseComp));
                    }
                    else
                    {
                        DrawCheckResult("Player Mouse Events — Wiring", true, null, null, null);
                    }
                }
            }
            else
            {
                // -1: couldn't read the Elements field (different Rewired version) — don't claim a false failure.
                DrawHintBox("Player Mouse detected, but its Elements list couldn't be inspected automatically — verify manually that a horizontal/vertical axis is bound.");
            }
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
            DrawStatusBox(msg, ColOrange, "⚠️", "Warning");
        }

        private void DrawHintBox(string msg)
        {
            DrawStatusBox(msg, ColDim, "💡", "Info");
        }

        private void DrawErrorBox(string msg)
        {
            DrawStatusBox(msg, ColRed, "❌", "Error");
        }

        private void DrawSuccessBox(string msg)
        {
            DrawStatusBox(msg, ColGreen, "✅", "Success");
        }

        private void DrawStatusBox(string msg, Color statusColor, string icon, string tag)
        {
            var r = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), ColCard);
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, 3, r.height + 4), statusColor);
            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11, normal = { textColor = statusColor } };
            GUILayout.Label($"{icon}  {tag}", titleStyle, GUILayout.ExpandWidth(false));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            var msgStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColDim }, wordWrap = true };
            EditorGUILayout.LabelField(msg, msgStyle);

            GUILayout.Space(5);
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
                    // Disable editing for runtime status variables
                    if (properties[i].name == "IsSteamOverlayActive" || properties[i].name == "alreadyShowedControllerHelp")
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
            if (title == "Cursor & Visuals" && properties[0].objectReferenceValue == null)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(15);
                if (GUILayout.Button("🛠️  Generate Game Cursor & Link", GUILayout.Height(20)))
                {
                    DefaultSetupGenerator.CreateGameCursorAndWire((RewiredInputManager)serializedObject.targetObject, serializedObject);
                }
                GUILayout.Space(5);
                EditorGUILayout.EndHorizontal();
            }
            else if (title == "Pause & Steam Overlay" && properties[0].objectReferenceValue == null)
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
                var eventProp = properties[0]; // OnShowControllerHelp is the first property in this group now
                var callsProp = eventProp?.FindPropertyRelative("m_PersistentCalls.m_Calls");
                bool hasListeners = callsProp != null && callsProp.arraySize > 0;
                
                if (!hasListeners)
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

        /// <summary>
        /// Unlike <see cref="IsEventWired"/> (any non-null listener), this checks the event is
        /// wired to a *specific* target/method — used to tell a correct GameCursorPositioner
        /// binding apart from a stale direct-to-anchoredPosition one left by an older setup.
        /// </summary>
        private static bool IsEventWiredTo(SerializedProperty eventProp, UnityEngine.Object target, string methodName)
        {
            if (eventProp == null || target == null) return false;
            var calls = eventProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls == null) return false;

            for (int i = 0; i < calls.arraySize; i++)
            {
                var call = calls.GetArrayElementAtIndex(i);
                var callTarget = call.FindPropertyRelative("m_Target").objectReferenceValue;
                var callMethod = call.FindPropertyRelative("m_MethodName").stringValue;
                if (callTarget == target && callMethod == methodName)
                    return true;
            }
            return false;
        }

        private static bool IsEventWired(SerializedProperty eventProp)
        {
            if (eventProp == null) return false;
            var calls = eventProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls == null || calls.arraySize == 0) return false;

            for (int i = 0; i < calls.arraySize; i++)
            {
                var call = calls.GetArrayElementAtIndex(i);
                var target = call.FindPropertyRelative("m_Target").objectReferenceValue;
                var methodName = call.FindPropertyRelative("m_MethodName").stringValue;
                if (target != null && !string.IsNullOrEmpty(methodName))
                    return true;
            }
            return false;
        }
    }
}
