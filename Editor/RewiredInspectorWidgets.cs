using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>Small reusable UI Toolkit pieces of the RewiredInputManager Inspector.</summary>
    internal static class RewiredInspectorWidgets
    {
        private const string SessionKeyPrefix = "RewiredHelper.Inspector.";
        private const string PassedGroupKey = SessionKeyPrefix + "passedGroup";

        /// <summary>A card containing a foldout whose open/closed state survives inspector rebuilds and domain reloads.</summary>
        public static Foldout CreateSection(VisualElement parent, string key, string title, bool defaultOpen, string hint = null)
        {
            var card = new VisualElement();
            card.AddToClassList("rh-card");

            var foldout = new Foldout { text = title, value = SessionState.GetBool(SessionKeyPrefix + key, defaultOpen) };
            foldout.AddToClassList("rh-section");
            foldout.RegisterValueChangedCallback(evt =>
            {
                // Nested foldouts bubble their own changes; only persist this one.
                if (evt.target == foldout)
                    SessionState.SetBool(SessionKeyPrefix + key, evt.newValue);
            });

            if (!string.IsNullOrEmpty(hint))
            {
                var hintLabel = new Label(hint);
                hintLabel.AddToClassList("rh-section-hint");
                foldout.Add(hintLabel);
            }

            card.Add(foldout);
            parent.Add(card);
            return foldout;
        }

        /// <param name="trailing">Extra elements (e.g. copy buttons) placed after the fix button, in order.</param>
        public static VisualElement CreateCheckRow(AuditResult result, Action onFixed, IEnumerable<VisualElement> trailing = null)
        {
            var row = new VisualElement();
            row.AddToClassList("rh-check");
            row.AddToClassList(SeverityClass(result.Severity));

            var body = new VisualElement();
            body.AddToClassList("rh-check-body");

            var title = new Label($"{SeverityIcon(result.Severity)}  {result.Title}");
            title.AddToClassList("rh-check-title");
            body.Add(title);

            AddDescription(body, result.Detail);
            if (result.Severity != AuditSeverity.Pass && result.Fix == null)
                AddDescription(body, string.IsNullOrEmpty(result.FixHint) ? null : "💡 " + result.FixHint);

            row.Add(body);

            if (result.Fix != null)
                row.Add(CreateFixButton(result, onFixed));

            if (trailing != null)
                foreach (var element in trailing)
                    row.Add(element);

            return row;
        }

        private static void AddDescription(VisualElement body, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            var label = new Label(text);
            label.AddToClassList("rh-check-desc");
            body.Add(label);
        }

        private static Button CreateFixButton(AuditResult result, Action onFixed)
        {
            var button = new Button(() =>
            {
                try { result.Fix(); }
                catch (Exception ex) { Debug.LogError($"[RewiredHelper] '{result.FixLabel}' failed: {ex.Message}"); }
                onFixed?.Invoke();
            })
            {
                tooltip = result.FixHint
            };
            RewiredHelperUIStyle.ApplyIconText(button, "🛠 " + result.FixLabel);
            button.AddToClassList("rh-toolbar-btn");
            button.AddToClassList("rh-toolbar-btn-primary");
            button.AddToClassList("rh-check-fix");
            return button;
        }

        /// <summary>Failures first, then warnings, then info; passed checks are folded into a collapsed group.</summary>
        public static void FillChecks(VisualElement container, IReadOnlyList<AuditResult> results, Action onFixed)
        {
            container.Clear();

            foreach (var result in results.Where(r => r.Severity != AuditSeverity.Pass).OrderByDescending(r => r.Severity))
                container.Add(CreateCheckRow(result, onFixed));

            var passed = results.Where(r => r.Severity == AuditSeverity.Pass).ToList();
            if (passed.Count == 0) return;

            var group = new Foldout { text = $"✓ {passed.Count} check(s) passed", value = SessionState.GetBool(PassedGroupKey, false) };
            group.style.marginTop = 4;
            group.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == group) SessionState.SetBool(PassedGroupKey, evt.newValue);
            });
            foreach (var result in passed)
                group.Add(CreateCheckRow(result, onFixed));
            container.Add(group);
        }

        /// <summary>A read-only "this is handled for you" row.</summary>
        public static VisualElement CreateAutomaticRow(string title, string detail)
        {
            var row = new VisualElement();
            row.AddToClassList("rh-check");
            row.AddToClassList("rh-check-pass");

            var body = new VisualElement();
            body.AddToClassList("rh-check-body");

            var titleLabel = new Label("⚙  " + title);
            titleLabel.AddToClassList("rh-check-title");
            body.Add(titleLabel);
            AddDescription(body, detail);

            row.Add(body);

            var chip = RewiredHelperUIStyle.CreateChip(string.Empty, "AUTO", out _, "rh-chip-on");
            chip.style.flexShrink = 0;
            chip.style.marginLeft = 8;
            row.Add(chip);
            return row;
        }

        public static string SeverityClass(AuditSeverity severity) => severity switch
        {
            AuditSeverity.Pass => "rh-check-pass",
            AuditSeverity.Warning => "rh-check-warn",
            AuditSeverity.Fail => "rh-check-fail",
            _ => "rh-check-info"
        };

        public static string SeverityIcon(AuditSeverity severity) => severity switch
        {
            AuditSeverity.Pass => "✓",
            AuditSeverity.Warning => "⚠",
            AuditSeverity.Fail => "✕",
            _ => "ℹ"
        };

        public static VisualElement CreateChipRow() => CreateRow("rh-chip-row");

        public static VisualElement CreateRow(string className)
        {
            var row = new VisualElement();
            row.AddToClassList(className);
            return row;
        }
    }
}
