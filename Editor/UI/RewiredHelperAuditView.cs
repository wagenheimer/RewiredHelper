using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.RewiredHelper.Editor
{
    internal sealed class RewiredHelperAuditView
    {
        private const long CopiedFeedbackMs = 1500;
        private const float IconButtonWidth = 30f;

        public VisualElement Root { get; }

        // The audit walks the whole scene, so switching Dashboard tabs reuses the last result while nothing changed.
        // Any undoable edit, a scene switch or a build-target change invalidates it; "Run Audit Now" always re-runs.
        private static List<AuditResult> _cachedResults;
        private static string _cachedKey;

        private List<AuditResult> _results;
        private AuditSeverity? _severityFilter;
        private VisualElement _resultsContainer;
        private Label _summaryLabel;

        public RewiredHelperAuditView()
        {
            Root = new VisualElement();
            RewiredHelperUIStyle.Apply(Root);
            BuildUI();

            if (_cachedResults != null && _cachedKey == CurrentCacheKey())
            {
                _results = _cachedResults;
                RefreshResults();
            }
            else
            {
                RunAudit();
            }
        }

        private static string CurrentCacheKey() =>
            $"{UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}|{UnityEditor.Undo.GetCurrentGroup()}|{UnityEditor.EditorUserBuildSettings.activeBuildTarget}";

        private void BuildUI()
        {
            var headerCard = RewiredHelperUIStyle.CreateCard("🔍 Project Setup Verification Audit",
                "Automated scan of the open scene, pause policy and project wiring. Most findings have a one-click fix.");

            var actionsRow = Row();
            actionsRow.Add(RewiredHelperUIStyle.CreateButton("▶ Run Audit Now", RunAudit, primary: true));
            actionsRow.Add(RewiredHelperUIStyle.CreateButton("🛠 Fix All Safe Issues", () =>
            {
                RewiredSetupAll.Run();
                RunAudit();
            }));
            actionsRow.Add(RewiredHelperUIStyle.CreateButton("All", () => SetFilter(null)));
            actionsRow.Add(RewiredHelperUIStyle.CreateButton("✕ Fails Only", () => SetFilter(AuditSeverity.Fail)));
            actionsRow.Add(RewiredHelperUIStyle.CreateButton("⚠ Warnings Only", () => SetFilter(AuditSeverity.Warning)));
            headerCard.Add(actionsRow);

            var copyRow = Row();
            copyRow.Add(CreateCopyButton("📋 Copy Report", "Copy the full audit as Markdown.",
                () => _results != null ? RewiredHelperAudit.ToMarkdown(_results) : null));
            var promptBtn = CreateCopyButton("🤖 Copy AI Fix Prompt (all)",
                "Copy one ready-to-paste prompt that makes an AI agent fix every warning and failure.",
                () => _results != null ? RewiredHelperAudit.ToPromptMarkdown(_results) : null);
            promptBtn.AddToClassList("rh-toolbar-btn-primary");
            copyRow.Add(promptBtn);
            headerCard.Add(copyRow);

            _summaryLabel = new Label("Running audit...");
            _summaryLabel.style.fontSize = 11;
            _summaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerCard.Add(_summaryLabel);

            Root.Add(headerCard);

            _resultsContainer = new VisualElement();
            Root.Add(_resultsContainer);
        }

        public void RunAudit()
        {
            _results = RewiredHelperAudit.RunAudit();
            _cachedResults = _results;
            _cachedKey = CurrentCacheKey();
            RefreshResults();
        }

        private void SetFilter(AuditSeverity? filter)
        {
            _severityFilter = filter;
            RefreshResults();
        }

        private void RefreshResults()
        {
            _resultsContainer.Clear();
            if (_results == null || _results.Count == 0)
            {
                _resultsContainer.Add(RewiredHelperUIStyle.CreateCallout("No audit results to display."));
                return;
            }

            UpdateSummary();
            AddHistoryCard();

            var filtered = _results.Where(r => !_severityFilter.HasValue || r.Severity == _severityFilter.Value);
            foreach (var group in filtered.GroupBy(r => r.Category))
            {
                var card = RewiredHelperUIStyle.CreateCard(group.Key);
                AddCategoryPromptButton(card, group.Key);
                foreach (var item in group)
                    card.Add(CreateRow(item));
                _resultsContainer.Add(card);
            }
        }

        private void UpdateSummary()
        {
            int fails = _results.Count(r => r.Severity == AuditSeverity.Fail);
            int warnings = _results.Count(r => r.Severity == AuditSeverity.Warning);
            int passes = _results.Count(r => r.Severity == AuditSeverity.Pass);

            if (fails > 0)
            {
                _summaryLabel.text = $"❌ {fails} critical failure(s) found: resolve before publishing.";
                SetSummaryClass("rh-summary-fail");
            }
            else if (warnings > 0)
            {
                _summaryLabel.text = $"⚠️ {warnings} warning(s) found: review recommended.";
                SetSummaryClass("rh-summary-warn");
            }
            else
            {
                _summaryLabel.text = $"✓ All {passes} automated checks passed cleanly!";
                SetSummaryClass("rh-summary-pass");
            }
        }

        /// <summary>The shared check row (same look as the Inspector), plus per-finding copy buttons.</summary>
        /// <summary>What was already fixed from the Audit in this editor session, newest first.</summary>
        private void AddHistoryCard()
        {
            var entries = RewiredAuditHistory.Entries();
            if (entries.Count == 0) return;

            var card = RewiredHelperUIStyle.CreateCard("✔ Done so far", "Fixes applied from the Audit in this editor session.");
            foreach (var entry in entries.Take(8))
            {
                var line = new Label($"{entry.Time:HH:mm:ss}   {entry.FixLabel}   ({entry.Title})");
                line.AddToClassList("rh-check-desc");
                card.Add(line);
            }

            var clear = RewiredHelperUIStyle.CreateButton("Clear", () =>
            {
                RewiredAuditHistory.Clear();
                RefreshResults();
            });
            clear.style.alignSelf = Align.FlexStart;
            card.Add(clear);
            _resultsContainer.Add(card);
        }

        /// <summary>A per-category "copy AI prompt" button in the card header, for fixing one area at a time.</summary>
        private void AddCategoryPromptButton(VisualElement card, string category)
        {
            var header = card.Q(className: "rh-card-header");
            if (header == null) return;

            var button = CreateCopyButton("🤖 Copy prompt", "Copy an AI prompt that fixes only the findings in this category.",
                () => _results != null ? RewiredHelperAudit.ToPromptMarkdown(_results, category) : null);
            button.style.marginLeft = 8;
            header.Add(button);
        }

        private void SetSummaryClass(string className)
        {
            _summaryLabel.RemoveFromClassList("rh-summary-fail");
            _summaryLabel.RemoveFromClassList("rh-summary-warn");
            _summaryLabel.RemoveFromClassList("rh-summary-pass");
            _summaryLabel.AddToClassList(className);
        }

        private VisualElement CreateRow(AuditResult item)
        {
            var trailing = new List<VisualElement> { CreateIconButton("📋", "Copy this finding.", () => FormatFinding(item)) };
            if (!string.IsNullOrEmpty(item.Prompt))
                trailing.Add(CreateIconButton("🤖", "Copy an AI prompt that fixes this finding.", () => item.Prompt));

            return RewiredInspectorWidgets.CreateCheckRow(item, RunAudit, trailing);
        }

        private static VisualElement Row() =>
            new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8, flexWrap = Wrap.Wrap } };

        private static string FormatFinding(AuditResult r)
        {
            var text = $"[{r.Severity}] {r.Category} - {r.Title}";
            if (!string.IsNullOrEmpty(r.Detail)) text += "\n" + r.Detail;
            if (!string.IsNullOrEmpty(r.FixHint)) text += "\nHow to fix: " + r.FixHint;
            return text;
        }

        private static Button CreateIconButton(string icon, string tooltip, Func<string> getText)
        {
            var button = CreateCopyButton(icon, tooltip, getText, "✓");
            button.style.width = IconButtonWidth;
            button.style.marginLeft = 4;
            button.style.marginRight = 0;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            return button;
        }

        private static Button CreateCopyButton(string label, string tooltip, Func<string> getText, string copiedLabel = "✓ Copied")
        {
            var button = new Button { tooltip = tooltip };
            RewiredHelperUIStyle.ApplyIconText(button, label);
            button.AddToClassList("rh-toolbar-btn");
            button.clicked += () =>
            {
                var text = getText();
                RewiredHelperUIStyle.ApplyIconText(button, string.IsNullOrEmpty(text) ? "Run the audit first" : copiedLabel);
                if (!string.IsNullOrEmpty(text))
                    GUIUtility.systemCopyBuffer = text;

                button.schedule.Execute(() => RewiredHelperUIStyle.ApplyIconText(button, label)).ExecuteLater(CopiedFeedbackMs);
            };
            return button;
        }
    }
}
