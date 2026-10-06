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
            var button = new Button(onClick);
            ApplyIconText(button, text);
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

            var header = new VisualElement();
            header.AddToClassList("rh-code-header");

            var copy = new Button { text = "Copy" };
            copy.AddToClassList("rh-toolbar-btn");
            copy.AddToClassList("rh-code-copy");
            copy.clicked += () =>
            {
                UnityEngine.GUIUtility.systemCopyBuffer = code;
                copy.text = "✓ Copied";
                copy.schedule.Execute(() => copy.text = "Copy").ExecuteLater(1200);
            };
            header.Add(copy);
            box.Add(header);

            var label = new Label(code);
            label.AddToClassList("rh-code-text");
            box.Add(label);

            return box;
        }

        /// <summary>
        /// Renders <paramref name="text"/> on the button, splitting a leading icon (emoji/symbol) into its own
        /// element with a reserved width. Inline, a fallback emoji glyph draws wider than it measures, so the
        /// following text runs over it ("◻heck Updates"); a separate, min-width'd element keeps them apart.
        /// </summary>
        public static void ApplyIconText(Button button, string text)
        {
            // Idempotent: drop icon/text children from a previous call so live updates can re-apply cleanly.
            for (int i = button.childCount - 1; i >= 0; i--)
            {
                var child = button[i];
                if (child.ClassListContains("rh-btn-icon") || child.ClassListContains("rh-btn-text"))
                    child.RemoveFromHierarchy();
            }

            SplitLeadingIcon(text, out var icon, out var label);

            if (string.IsNullOrEmpty(icon))
            {
                button.text = text;
                return;
            }

            button.text = string.Empty;
            var iconElement = CreateIconElement(icon);
            if (string.IsNullOrEmpty(label)) iconElement.style.marginRight = 0;
            button.Add(iconElement);

            if (!string.IsNullOrEmpty(label))
            {
                var textLabel = new Label(label);
                textLabel.AddToClassList("rh-btn-text");
                textLabel.pickingMode = PickingMode.Ignore;
                button.Add(textLabel);
            }
        }

        private static Label CreateIconElement(string icon)
        {
            var iconLabel = new Label(icon);
            iconLabel.AddToClassList("rh-btn-icon");
            iconLabel.pickingMode = PickingMode.Ignore;
            return iconLabel;
        }

        /// <summary>
        /// Splits a leading run of icon code points from the rest of a label. Deliberately conservative: only
        /// arrows/symbols and pictographic emoji count, so ordinary words (including accented ones) stay intact.
        /// </summary>
        internal static void SplitLeadingIcon(string text, out string icon, out string label)
        {
            icon = null;
            label = text;
            if (string.IsNullOrEmpty(text)) return;

            int i = 0;
            while (i < text.Length)
            {
                int codePoint = char.IsHighSurrogate(text[i]) && i + 1 < text.Length
                    ? char.ConvertToUtf32(text[i], text[i + 1])
                    : text[i];

                if (!IsIconCodePoint(codePoint)) break;
                i += char.IsHighSurrogate(text[i]) ? 2 : 1;
            }

            if (i == 0) return;

            icon = text.Substring(0, i).TrimEnd();
            label = text.Substring(i).TrimStart();
        }

        private static bool IsIconCodePoint(int codePoint) =>
            (codePoint >= 0x2190 && codePoint <= 0x2BFF)     // arrows, geometric shapes, misc symbols (↗ ▶ ⏸ ⚡ ✔ ⚙ ✓ ✕ …)
            || (codePoint >= 0x1F000 && codePoint <= 0x1FAFF) // emoji & pictographs (🔄 🌐 📦 🔍 🛠 …)
            || codePoint == 0xFE0F                             // emoji variation selector (✉️ 🛠️ …)
            || codePoint == 0x20E3;                            // combining enclosing keycap
    }
}
