using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// One click to bring a scene to a working Rewired Helper setup: runs the Setup Audit and applies every one-click fix, repeating
    /// because later checks depend on earlier ones (the Event System needs the manager, the UI actions need the Event System, ...).
    /// Fixes that rewrite other scenes or migrate legacy components are left to the Setup Audit, where they can be reviewed first.
    /// </summary>
    internal static class RewiredSetupAll
    {
        private const int MaxPasses = 6;

        [MenuItem(RewiredHelperMenu.SetUpEverything, priority = RewiredHelperMenu.SetUpEverythingPriority)]
        internal static void SetUpEverythingMenu()
        {
            var applied = Run();
            var remaining = RewiredHelperAudit.RunAudit()
                .Count(r => r.Severity == AuditSeverity.Fail || r.Severity == AuditSeverity.Warning);

            var summary = applied.Count == 0
                ? "Nothing to set up: every automatic fix is already applied."
                : "Applied:\n- " + string.Join("\n- ", applied);

            if (remaining > 0)
                summary += $"\n\n{remaining} finding(s) still need attention (manual or cross-scene). Open the Dashboard > Setup Audit.";

            EditorUtility.DisplayDialog("Rewired Helper: Set Up Everything", summary, "OK");
        }

        /// <summary>Applies the safe one-click fixes until the audit has nothing left to fix. Returns the labels of what was applied.</summary>
        internal static List<string> Run()
        {
            var applied = new List<string>();
            var attempted = new HashSet<string>();

            for (var pass = 0; pass < MaxPasses; pass++)
            {
                var fixes = RewiredHelperAudit.RunAudit().Where(IsSafeFix).ToList();
                var progressed = false;

                foreach (var result in fixes)
                {
                    // A fix that did not clear its finding must not be retried forever.
                    if (!attempted.Add($"{result.Category}|{result.Title}")) continue;

                    progressed = true;
                    try
                    {
                        result.Fix();
                        applied.Add(result.FixLabel);
                        RewiredAuditHistory.Record(result.Title, result.FixLabel);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[RewiredHelper] '{result.FixLabel}' failed: {ex.Message}");
                    }
                }

                if (!progressed) break;
            }

            EnsureOnScreenKeyboard(applied);
            return applied;
        }

        /// <summary>
        /// The keyboard check stays quiet when the open scene has no input field (forms often live in prefabs), so the audit never offers
        /// it there. A persistent scene with the manager is exactly where it belongs, so Set Up Everything adds it.
        /// </summary>
        private static void EnsureOnScreenKeyboard(List<string> applied)
        {
            if (DefaultSetupGenerator.FindInputManagerInScene() == null) return;
            if (DefaultSetupGenerator.FindOnScreenKeyboardInScene() != null) return;

            DefaultSetupGenerator.CreateOnScreenKeyboardAndWire();
            applied.Add("Create On-Screen Keyboard");
            RewiredAuditHistory.Record("On-Screen Keyboard for gamepad text input", "Create On-Screen Keyboard");
        }

        private static bool IsSafeFix(AuditResult result)
        {
            if (result.Fix == null) return false;
            if (result.Severity != AuditSeverity.Fail && result.Severity != AuditSeverity.Warning) return false;

            var label = result.FixLabel ?? string.Empty;
            return !label.Contains("All Scenes") && !label.StartsWith("Migrate", StringComparison.OrdinalIgnoreCase);
        }
    }
}
