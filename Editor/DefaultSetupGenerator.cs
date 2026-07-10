using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Rewired;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Scene-building helpers under <b>Tools → Wagenheimer → Rewired Helper</b> that create a
    /// standard <see cref="RewiredInputManager"/> setup and a controller-help form wired to it.
    /// These build plain GameObjects directly in the open scene (rather than shipping a
    /// hand-authored .prefab file, which this package's CI can't validate compiles or instantiates
    /// correctly) — save the result as a prefab in your own project once you're happy with it.
    ///
    /// The controller-help form is populated using Rewired's own official glyph system
    /// (Window > Rewired > Extras > Glyphs > Install → installs the icon sets and
    /// Rewired.Glyphs.UnityUI.UnityUITextMeshProGlyphHelper) if the consumer has installed it —
    /// this package cannot bundle that addon itself since it ships under Rewired's own commercial
    /// license, not this package's MIT one. Referenced via reflection so this package compiles
    /// whether or not the addon is present. If it isn't found, the form is still created with
    /// placeholder text pointing at that install menu.
    /// </summary>
    internal static class DefaultSetupGenerator
    {
        const string GlyphHelperTypeName = "Rewired.Glyphs.UnityUI.UnityUITextMeshProGlyphHelper";

        const string InputManagerPrefabPath = "Packages/com.wagenheimer.rewiredhelper/Runtime/Prefabs/Rewired Input Manager.prefab";
        const string EventSystemPrefabPath = "Packages/com.wagenheimer.rewiredhelper/Runtime/Prefabs/Rewired Event System.prefab";
        const string FormControllerPrefabPath = "Packages/com.wagenheimer.rewiredhelper/Runtime/Prefabs/formController.prefab";

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Create Rewired Input Manager", priority = 11)]
        internal static void CreateRewiredInputManager()
        {
            // 1. Instantiate Rewired Input Manager if not present
            var rewiredManager = FindInputManagerInScene();
            if (rewiredManager == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InputManagerPrefabPath);
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    Undo.RegisterCreatedObjectUndo(instance, "Create Rewired Input Manager");
                    rewiredManager = instance.GetComponent("InputManager");
                }
                else
                {
                    Debug.LogWarning($"[RewiredHelper] Prefab not found at {InputManagerPrefabPath}");
                }
            }

            // If still null (e.g. prefab missing), create a fallback empty gameobject
            GameObject managerGo = rewiredManager != null ? rewiredManager.gameObject : null;
            if (managerGo == null)
            {
                managerGo = new GameObject("Rewired Input Manager");
                Undo.RegisterCreatedObjectUndo(managerGo, "Create Rewired Input Manager");
            }

            // 2. Add our RewiredInputManager component if not present
            var helper = managerGo.GetComponent<RewiredInputManager>();
            if (helper == null)
            {
                helper = managerGo.AddComponent<RewiredInputManager>();
                Undo.RegisterCompleteObjectUndo(managerGo, "Add RewiredInputManager Component");
            }

            // 3. Instantiate Rewired Event System if not present
            var eventSystem = UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EventSystemPrefabPath);
                if (eventSystemPrefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(eventSystemPrefab);
                    Undo.RegisterCreatedObjectUndo(instance, "Create Rewired Event System");
                }
                else
                {
                    // Fallback to standard EventSystem
                    var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                        typeof(UnityEngine.EventSystems.StandaloneInputModule));
                    Undo.RegisterCreatedObjectUndo(go, "Create Event System");
                }
            }

            Selection.activeGameObject = managerGo;
            MarkSceneDirty();
        }

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Create Controller Help Form", priority = 12)]
        internal static void CreateControllerHelpForm()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FormControllerPrefabPath);
            GameObject formGo = null;

            if (prefab != null)
            {
                var canvas = FindOrCreateCanvas();
                formGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                Undo.RegisterCreatedObjectUndo(formGo, "Create Controller Help Form");
                
                // If the glyph helper addon is installed, apply glyph text dynamically via reflection
                var glyphHelperType = FindGlyphHelperType();
                if (glyphHelperType != null)
                {
                    var helper = formGo.GetComponentInChildren(glyphHelperType);
                    if (helper != null)
                    {
                        ApplyGlyphText(helper, glyphHelperType);
                    }
                }
            }
            else
            {
                // Fallback to procedural generation if prefab not found
                var canvas = FindOrCreateCanvas();
                var panel = CreatePanel(canvas.transform);
                var label = CreateLabel(panel);
                formGo = panel.gameObject;

                var glyphHelperType = FindGlyphHelperType();
                if (glyphHelperType != null)
                {
                    var helper = panel.gameObject.AddComponent(glyphHelperType);
                    ApplyGlyphText(helper, glyphHelperType);
                }
                else
                {
                    label.text = "Install Rewired's Glyphs addon first: Window > Rewired > Extras > Glyphs > " +
                        "Install (installs both the icon sets and the TextMeshPro UI helper), then " +
                        "regenerate this form to show real controller glyphs.";
                    Debug.LogWarning($"[RewiredHelper] {GlyphHelperTypeName} not found — install it via " +
                        "Window > Rewired > Extras > Glyphs > Install, then regenerate this form. See README.md > Controller Help Form.");
                }
            }

            if (formGo != null)
            {
                formGo.SetActive(false);
                Selection.activeGameObject = formGo;
                MarkSceneDirty();

                Debug.Log("[RewiredHelper] Created a Controller Help form (inactive by default). Wire " +
                    "RewiredInputManager.OnShowControllerHelp to SetActive(true) it — it already only " +
                    "fires once, the first time a joystick/gamepad is detected.");
            }
        }

        static Type FindGlyphHelperType()
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
        /// Builds one "&lt;rewiredElement&gt; &lt;rewiredAction&gt;" line per Action defined in
        /// this project's Rewired Input Manager and assigns it to the glyph helper's `text`
        /// property (both accessed via reflection — see <see cref="GlyphHelperTypeName"/>). The
        /// glyph helper re-parses this at runtime and swaps in the icon for whichever controller
        /// is currently active, so this is generated once at edit time and stays correct forever.
        /// </summary>
        static void ApplyGlyphText(Component helper, Type helperType)
        {
            if (ReInput.mapping == null)
            {
                Debug.LogWarning("[RewiredHelper] ReInput.mapping is null. ReInput is not initialized. Make sure to set up actions in your Rewired Input Manager configuration. A placeholder has been set.");
                var textProp = helperType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                textProp?.SetValue(helper, "<!-- ReInput.mapping was null at generation time. Ensure Rewired is initialized or run in Play Mode to populate this list. -->");
                return;
            }

            var sb = new StringBuilder();
            foreach (var action in ReInput.mapping.Actions.OrderBy(a => a.categoryId).ThenBy(a => a.name))
            {
                if (action.type != InputActionType.Button)
                    continue; // Axis actions need firstPole/range attributes to make sense in a flat list; skip here.

                sb.Append("<rewiredElement playerId=0 actionName=\"").Append(action.name).Append("\">  ");
                sb.Append("<rewiredAction name=\"").Append(action.name).Append("\">\n");
            }

            var textProperty = helperType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            textProperty?.SetValue(helper, sb.ToString());
        }

        static Canvas FindOrCreateCanvas()
        {
            var existing = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (existing != null)
                return existing;

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            return canvas;
        }

        static RectTransform CreatePanel(Transform parent)
        {
            var go = new GameObject("ControllerHelpForm", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(700, 500);
            rect.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.85f);

            return rect;
        }

        static TMP_Text CreateLabel(Transform parent)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24, 24);
            rect.offsetMax = new Vector2(-24, -24);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = 24;
            label.color = Color.white;
            label.enableWordWrapping = true;
            label.alignment = TextAlignmentOptions.TopLeft;

            return label;
        }

        static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        internal static Component FindInputManagerInScene()
        {
            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                var comp = go.GetComponent("InputManager");
                if (comp != null && comp.GetType().FullName == "Rewired.InputManager")
                    return comp;
            }
            return null;
        }
    }
}
