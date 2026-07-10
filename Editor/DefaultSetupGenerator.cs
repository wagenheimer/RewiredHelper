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
            var canvas = FindOrCreateCanvas();
            var formGo = GenerateRowBasedHelpForm(canvas.transform);

            Undo.RegisterCreatedObjectUndo(formGo, "Create Controller Help Form");
            formGo.SetActive(false);

            Selection.activeGameObject = formGo;
            MarkSceneDirty();

            Debug.Log("[RewiredHelper] Created a custom Row-Based Controller Help form (inactive by default). " +
                "Wire RewiredInputManager.OnShowControllerHelp to SetActive(true) it.");
        }

        static GameObject GenerateRowBasedHelpForm(Transform parent)
        {
            // 1. Create Main Panel (600x450)
            var formGo = new GameObject("ControllerHelpForm", typeof(RectTransform), typeof(Image));
            var formRect = (RectTransform)formGo.transform;
            formRect.SetParent(parent, false);
            formRect.anchorMin = new Vector2(0.5f, 0.5f);
            formRect.anchorMax = new Vector2(0.5f, 0.5f);
            formRect.sizeDelta = new Vector2(600, 450);
            formRect.anchoredPosition = Vector2.zero;

            var bgImage = formGo.GetComponent<Image>();
            bgImage.color = new Color(0.08f, 0.08f, 0.10f, 0.98f); // Sleek dark blue-black background

            // Top Color Highlight Bar (Accent)
            var topBarGo = new GameObject("TopAccentBar", typeof(RectTransform), typeof(Image));
            var topBarRect = (RectTransform)topBarGo.transform;
            topBarRect.SetParent(formRect, false);
            topBarRect.anchorMin = new Vector2(0, 1);
            topBarRect.anchorMax = new Vector2(1, 1);
            topBarRect.pivot = new Vector2(0.5f, 1);
            topBarRect.sizeDelta = new Vector2(0, 5);
            topBarRect.anchoredPosition = Vector2.zero;
            topBarGo.GetComponent<Image>().color = new Color(0.22f, 0.60f, 1.00f); // Accent Blue

            // 2. Create Header Title
            var headerGo = new GameObject("HeaderTitle", typeof(RectTransform));
            var headerRect = (RectTransform)headerGo.transform;
            headerRect.SetParent(formRect, false);
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 45);
            headerRect.anchoredPosition = new Vector2(0, -15);

            var headerText = headerGo.AddComponent<TextMeshProUGUI>();
            headerText.text = "GAMEPAD CONTROLS";
            headerText.fontSize = 20;
            headerText.color = Color.white;
            headerText.fontStyle = FontStyles.Bold;
            headerText.alignment = TextAlignmentOptions.Center;

            // Separator Underline
            var sepGo = new GameObject("HeaderSeparator", typeof(RectTransform), typeof(Image));
            var sepRect = (RectTransform)sepGo.transform;
            sepRect.SetParent(formRect, false);
            sepRect.anchorMin = new Vector2(0, 1);
            sepRect.anchorMax = new Vector2(1, 1);
            sepRect.pivot = new Vector2(0.5f, 1);
            sepRect.sizeDelta = new Vector2(-40, 1);
            sepRect.anchoredPosition = new Vector2(0, -60);
            sepGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.22f, 1f);

            // 3. Create Scroll View
            var scrollViewGo = new GameObject("Scroll View", typeof(RectTransform), typeof(ScrollRect));
            var scrollRect = scrollViewGo.GetComponent<ScrollRect>();
            var scrollRectTransform = (RectTransform)scrollViewGo.transform;
            scrollRectTransform.SetParent(formRect, false);
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(20, 50); // Leave room for footer
            scrollRectTransform.offsetMax = new Vector2(-20, -75); // Offset top for header

            // Viewport
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRect = (RectTransform)viewportGo.transform;
            viewportRect.SetParent(scrollRectTransform, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0, 0, 0, 0); // Transparent mask
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            // Content Container
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var contentRect = (RectTransform)contentGo.transform;
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(10, 10, 10, 10);

            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Footer Prompt
            var footerGo = new GameObject("FooterPrompt", typeof(RectTransform));
            var footerRect = (RectTransform)footerGo.transform;
            footerRect.SetParent(formRect, false);
            footerRect.anchorMin = new Vector2(0, 0);
            footerRect.anchorMax = new Vector2(1, 0);
            footerRect.pivot = new Vector2(0.5f, 0);
            footerRect.sizeDelta = new Vector2(0, 30);
            footerRect.anchoredPosition = new Vector2(0, 12);

            var footerText = footerGo.AddComponent<TextMeshProUGUI>();
            footerText.text = "PRESS ANY BUTTON TO RESUME";
            footerText.fontSize = 11;
            footerText.color = new Color(0.45f, 0.45f, 0.50f);
            footerText.fontStyle = FontStyles.Bold;
            footerText.alignment = TextAlignmentOptions.Center;

            // 4. Generate Rows
            var glyphHelperType = FindGlyphHelperType();
            var actions = ReInput.mapping != null 
                ? ReInput.mapping.Actions.Where(a => a.type == InputActionType.Button).OrderBy(a => a.categoryId).ThenBy(a => a.name).ToList()
                : new System.Collections.Generic.List<InputAction>();

            if (actions.Count == 0)
            {
                // Fallback template rows if mapping is empty (e.g. edit mode)
                CreateHelpRow(contentRect, "UIHorizontal", "Move Selection (Horizontal)", glyphHelperType, false);
                CreateHelpRow(contentRect, "UIVertical", "Move Selection (Vertical)", glyphHelperType, true);
                CreateHelpRow(contentRect, "UISubmit", "Confirm / Select", glyphHelperType, false);
                CreateHelpRow(contentRect, "UICancel", "Back / Cancel", glyphHelperType, true);
            }
            else
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    CreateHelpRow(contentRect, actions[i].name, actions[i].descriptiveName, glyphHelperType, i % 2 == 1);
                }
            }

            return formGo;
        }

        static void CreateHelpRow(Transform parent, string actionName, string actionDesc, Type glyphHelperType, bool isAlt)
        {
            var rowGo = new GameObject($"Row_{actionName}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            var rowRect = (RectTransform)rowGo.transform;
            rowRect.SetParent(parent, false);
            rowRect.sizeDelta = new Vector2(0, 38);

            var bgImage = rowGo.GetComponent<Image>();
            // Alternating dark backgrounds
            bgImage.color = isAlt 
                ? new Color(0.12f, 0.12f, 0.14f, 0.7f) 
                : new Color(0.15f, 0.15f, 0.17f, 0.8f);

            // Left accent vertical bar (like a tag)
            var accentBarGo = new GameObject("AccentTag", typeof(RectTransform), typeof(Image));
            var accentBarRect = (RectTransform)accentBarGo.transform;
            accentBarRect.SetParent(rowRect, false);
            accentBarRect.anchorMin = new Vector2(0, 0.5f);
            accentBarRect.anchorMax = new Vector2(0, 0.5f);
            accentBarRect.pivot = new Vector2(0, 0.5f);
            accentBarRect.sizeDelta = new Vector2(3, 26);
            accentBarRect.anchoredPosition = new Vector2(4, 0);
            accentBarGo.GetComponent<Image>().color = new Color(0.22f, 0.60f, 1.00f); // Blue accent

            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 15f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(20, 20, 3, 3);

            // Icon Label (Left side) - Right aligned for crisp button layout
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(rowRect, false);
            iconRect.sizeDelta = new Vector2(140, 32);

            var iconText = iconGo.AddComponent<TextMeshProUGUI>();
            iconText.text = $"<rewiredElement playerId=0 actionName=\"{actionName}\">";
            iconText.fontSize = 18;
            iconText.color = Color.white;
            iconText.alignment = TextAlignmentOptions.Right;

            if (glyphHelperType != null)
            {
                iconGo.AddComponent(glyphHelperType);
            }

            // Separator dash in row
            var divGo = new GameObject("Divider", typeof(RectTransform));
            var divRect = (RectTransform)divGo.transform;
            divRect.SetParent(rowRect, false);
            divRect.sizeDelta = new Vector2(15, 32);
            var divText = divGo.AddComponent<TextMeshProUGUI>();
            divText.text = "—";
            divText.fontSize = 14;
            divText.color = new Color(0.4f, 0.4f, 0.45f);
            divText.alignment = TextAlignmentOptions.Center;

            // Description Label (Right side)
            var descGo = new GameObject("Description", typeof(RectTransform));
            var descRect = (RectTransform)descGo.transform;
            descRect.SetParent(rowRect, false);
            descRect.sizeDelta = new Vector2(320, 32);

            var descText = descGo.AddComponent<TextMeshProUGUI>();
            descText.text = !string.IsNullOrEmpty(actionDesc) ? actionDesc.ToUpper() : $"<rewiredAction name=\"{actionName}\">";
            descText.fontSize = 13;
            descText.fontStyle = FontStyles.Bold;
            descText.color = new Color(0.75f, 0.75f, 0.8f);
            descText.alignment = TextAlignmentOptions.Left;
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
