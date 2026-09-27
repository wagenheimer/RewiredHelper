using UnityEditor;
using UnityEngine.UIElements;

namespace Wagenheimer.RewiredHelper.Editor
{
    internal static class RewiredHelperUIStyle
    {
        private const string PackageUssPath = "Packages/com.wagenheimer.rewiredhelper/Editor/UI/RewiredHelperCommon.uss";
        private const string LocalUssPath = "Assets/Editor/UI/RewiredHelperCommon.uss";

        public static void Apply(VisualElement element)
        {
            if (element == null) return;

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(PackageUssPath)
                        ?? AssetDatabase.LoadAssetAtPath<StyleSheet>(LocalUssPath);
            if (sheet != null)
                element.styleSheets.Add(sheet);
        }

        public static VisualElement CreateCard(string title, string subtitle = null)
        {
            var card = new VisualElement();
            card.AddToClassList("rh-card");

            var header = new VisualElement();
            header.AddToClassList("rh-card-header");

            var titleCol = new VisualElement();
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("rh-card-title");
            titleCol.Add(titleLabel);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var subLabel = new Label(subtitle);
                subLabel.AddToClassList("rh-card-subtitle");
                titleCol.Add(subLabel);
            }

            header.Add(titleCol);
            card.Add(header);
            return card;
        }

        public static Label CreateBadge(string text, AuditSeverity severity)
        {
            var badge = new Label(text);
            badge.AddToClassList("rh-badge");
            badge.AddToClassList(severity switch
            {
                AuditSeverity.Pass => "rh-badge-pass",
                AuditSeverity.Warning => "rh-badge-warn",
                AuditSeverity.Fail => "rh-badge-fail",
                _ => "rh-badge-info"
            });
            return badge;
        }

        public static VisualElement CreateCallout(string message, AuditSeverity level = AuditSeverity.Info)
        {
            var box = new VisualElement();
            box.AddToClassList("rh-callout");
            box.AddToClassList(level switch
            {
                AuditSeverity.Pass => "rh-callout-pass",
                AuditSeverity.Warning => "rh-callout-warn",
                _ => "rh-callout-info"
            });

            box.Add(new Label(message) { style = { whiteSpace = WhiteSpace.Normal } });
            return box;
        }

        public static Button CreateButton(string text, System.Action onClick, bool primary = false)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("rh-toolbar-btn");
            if (primary) button.AddToClassList("rh-toolbar-btn-primary");
            return button;
        }
    }
}
