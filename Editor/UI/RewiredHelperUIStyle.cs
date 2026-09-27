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

        /// <summary>A small "label: value" pill; the value label is returned so live views can update it.</summary>
        public static VisualElement CreateChip(string key, string value, out Label valueLabel, string stateClass = null)
        {
            var chip = new VisualElement();
            chip.AddToClassList("rh-chip");
            if (!string.IsNullOrEmpty(stateClass)) chip.AddToClassList(stateClass);

            var keyLabel = new Label(key);
            keyLabel.AddToClassList("rh-chip-key");
            chip.Add(keyLabel);

            valueLabel = new Label(value);
            valueLabel.AddToClassList("rh-chip-value");
            chip.Add(valueLabel);
            return chip;
        }

        public static VisualElement CreateCodeBox(string code)
        {
            var box = new VisualElement();
            box.AddToClassList("rh-code-box");

            var label = new Label(code);
            label.AddToClassList("rh-code-text");
            box.Add(label);

            var copy = new Button { text = "Copy" };
            copy.AddToClassList("rh-toolbar-btn");
            copy.AddToClassList("rh-code-copy");
            copy.clicked += () =>
            {
                UnityEngine.GUIUtility.systemCopyBuffer = code;
                copy.text = "✓";
                copy.schedule.Execute(() => copy.text = "Copy").ExecuteLater(1200);
            };
            box.Add(copy);
            return box;
        }
    }
}
