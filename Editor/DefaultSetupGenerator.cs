using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wagenheimer.RewiredHelper;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Scene-building helpers under <b>Tools → Wagenheimer → Rewired Helper</b> that create a
    /// standard, ready-to-use <see cref="RewiredInputManager"/> setup and a bare-bones controller
    /// help UI, wired together. These build plain GameObjects directly in the open scene (rather
    /// than shipping a hand-authored .prefab file, which this package's CI can't validate compiles
    /// or instantiates correctly) — save the result as a prefab in your own project once you're
    /// happy with it.
    ///
    /// The generated controller help UI is intentionally unstyled: Rewired glyph icons (Xbox/
    /// PlayStation/Switch button art) are project-specific data configured in each project's own
    /// Rewired Input Manager asset, and can't be shipped inside an open-source package for
    /// licensing reasons. Without glyphs configured, rows fall back to text-only labels.
    /// </summary>
    internal static class DefaultSetupGenerator
    {
        [MenuItem("Tools/Wagenheimer/Rewired Helper/Create Rewired Input Manager", priority = 11)]
        static void CreateRewiredInputManager()
        {
            var go = new GameObject("RewiredInputManager");
            Undo.RegisterCreatedObjectUndo(go, "Create Rewired Input Manager");
            go.AddComponent<RewiredInputManager>();

            Selection.activeGameObject = go;
            MarkSceneDirty();
        }

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Create Default Controller Help UI", priority = 12)]
        static void CreateControllerHelpUI()
        {
            var canvas = FindOrCreateCanvas();

            var panel = CreatePanel(canvas.transform);
            CreateScrollArea(panel.transform, out var content);
            var rowTemplate = CreateRowTemplate(content);

            var panelGo = panel.gameObject;
            var help = panelGo.AddComponent<Wagenheimer.RewiredHelper.UI.ControllerHelpPanel>();
            help.RowTemplate = rowTemplate;
            help.RowContainer = content;

            panelGo.SetActive(false);

            Undo.RegisterCreatedObjectUndo(panelGo, "Create Default Controller Help UI");
            Selection.activeGameObject = panelGo;
            MarkSceneDirty();

            Debug.Log("[RewiredHelper] Created a Controller Help panel (inactive by default). Wire " +
                "RewiredInputManager.OnShowControllerHelp to call SetActive(true) + ControllerHelpPanel.Populate() " +
                "on it, then style RowTemplate/panel background to match your game.");
        }

        static Canvas FindOrCreateCanvas()
        {
            var existing = Object.FindObjectOfType<Canvas>();
            if (existing != null)
                return existing;

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            return canvas;
        }

        static RectTransform CreatePanel(Transform parent)
        {
            var go = new GameObject("ControllerHelpPanel", typeof(RectTransform), typeof(Image));
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

        static ScrollRect CreateScrollArea(Transform parent, out RectTransform content)
        {
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.SetParent(parent, false);
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(20, 20);
            scrollRect.offsetMax = new Vector2(-20, -20);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewport = (RectTransform)viewportGo.transform;
            viewport.SetParent(scrollRect, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content = (RectTransform)contentGo.transform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;

            return scroll;
        }

        static Wagenheimer.RewiredHelper.UI.ControllerHelpRow CreateRowTemplate(Transform parent)
        {
            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var rowRect = (RectTransform)rowGo.transform;
            rowRect.SetParent(parent, false);

            var rowLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = false;

            var layoutElement = rowGo.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 36;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(rowRect, false);
            iconRect.sizeDelta = new Vector2(32, 32);
            var icon = iconGo.GetComponent<Image>();
            icon.enabled = false;
            icon.preserveAspect = true;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(rowRect, false);
            var labelLayoutElement = labelGo.AddComponent<LayoutElement>();
            labelLayoutElement.flexibleWidth = 1;
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 22;
            label.color = Color.white;
            label.text = "Element — Action";

            var row = rowGo.AddComponent<Wagenheimer.RewiredHelper.UI.ControllerHelpRow>();
            row.Icon = icon;
            row.Label = label;

            rowGo.SetActive(false);
            return row;
        }

        static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
