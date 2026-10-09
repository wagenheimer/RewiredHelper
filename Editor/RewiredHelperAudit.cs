using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using UnityEditor;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.Editor
{
    public enum AuditSeverity
    {
        Pass,
        Info,
        Warning,
        Fail
    }

    public struct AuditResult
    {
        public string Category;
        public string Title;
        public AuditSeverity Severity;
        public string Detail;
        public string FixHint;

        /// <summary>Ready-to-paste task for an AI coding agent. Empty for Pass results.</summary>
        public string Prompt;

        /// <summary>Plain-language explanation of what the finding is and why it matters (filled in by <see cref="RewiredAuditInfo"/>).</summary>
        public string About;

        /// <summary>The code / Inspector shape that satisfies the finding, shown with a Copy button.</summary>
        public string Code;

        /// <summary>Scene object or asset the Ping button selects.</summary>
        public UnityEngine.Object Target;

        /// <summary>"Fixed with ... at 10:42" when a fix from the Audit already resolved it in this session.</summary>
        public string DoneNote;

        /// <summary>One-click in-Editor fix (label + action). Null when the finding needs manual work.</summary>
        public string FixLabel;
        public Action Fix;
    }

    /// <summary>
    /// Headless-friendly audit of the open scene and the consuming project for Rewired Helper.
    /// Run from the Dashboard, or in CI:
    /// <code>Unity -batchmode -quit -projectPath "&lt;project&gt;" -logFile - -executeMethod Wagenheimer.RewiredHelper.Editor.RewiredHelperAudit.RunHeadlessAndLog</code>
    /// Manual items that cannot be verified from source live in <c>REWIRED-CHECKLIST.md</c>.
    /// </summary>
    public static class RewiredHelperAudit
    {
        private const string CategoryMobile = "Mobile & Pause";
        private const string CategoryProject = "Project";

        [MenuItem(RewiredHelperMenu.VerifySetup, priority = RewiredHelperMenu.VerifySetupPriority)]
        public static void OpenWindow() => RewiredHelperDashboardWindow.OpenAuditTab();

        /// <summary>Writes a Markdown report to the log and exits with code 1 if any check failed (batch mode).</summary>
        public static void RunHeadlessAndLog()
        {
            var results = RunAudit();
            Debug.Log(ToMarkdown(results));

            if (results.Any(r => r.Severity == AuditSeverity.Fail))
                EditorApplication.Exit(1);
        }

        public static List<AuditResult> RunAudit()
        {
            var results = new List<AuditResult>();
            var manager = FindAll<RewiredInputManager>().FirstOrDefault();

            RewiredSetupChecks.Run(results, manager);
            AuditMobilePause(results, manager);
            AuditProject(results, manager);
            AttachPrompts(results);

            return results;
        }

        /// <summary>An override that breaks mobile best practices only matters when building for mobile.</summary>
        private static AuditSeverity OverrideSeverity => IsMobileTarget ? AuditSeverity.Warning : AuditSeverity.Info;

        internal static bool IsMobileTarget =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ||
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;


        #region Mobile & pause

        internal static void AuditMobilePause(List<AuditResult> results, RewiredInputManager manager)
        {
            if (manager == null)
            {
                results.Add(Result(CategoryMobile, "Pause policy", AuditSeverity.Info,
                    "No RewiredInputManager in the open scene, pause settings not checked."));
                return;
            }

            AuditAutoDefaults(results, manager);
            AuditPauseScreen(results, manager);
        }

        /// <summary>
        /// The pause policy is automatic per build platform, so there is nothing to "fix": the only way to end up
        /// with the "GAME PAUSED on every app switch" behavior on mobile is an explicit override, which is the
        /// user's deliberate choice and is only reported (uncheck Override Platform Defaults to go back to automatic).
        /// </summary>
        private static void AuditAutoDefaults(List<AuditResult> results, RewiredInputManager manager)
        {
            Add(results, CategoryMobile, "Pause policy is automatic for the build platform", !manager.OverridePlatformDefaults,
                "Chosen from the build platform: mobile = silent pause, no tap-to-resume; desktop/console/TV = overlay, resume on input.",
                "Override Platform Defaults is on: the values on the manager replace the automatic per-platform policy.",
                "Uncheck Override Platform Defaults on the RewiredInputManager to return to the automatic policy.",
                failSeverity: AuditSeverity.Info);

            if (!manager.OverridePlatformDefaults) return;

            Add(results, CategoryMobile, "Overridden app-background pause is safe on mobile",
                manager.PauseOnAppBackground != AppBackgroundPauseMode.Overlay,
                $"PauseOnAppBackground = {manager.PauseOnAppBackground}.",
                RewiredOverrideScanner.OverlayIssue,
                "Set PauseOnAppBackground to Auto/Silent, or uncheck Override Platform Defaults.",
                failSeverity: OverrideSeverity);

            Add(results, CategoryMobile, "Overridden controller-disconnect pause is safe on mobile",
                manager.PauseOnControllerDisconnect != AutoToggle.On,
                $"PauseOnControllerDisconnect = {manager.PauseOnControllerDisconnect}.",
                RewiredOverrideScanner.DisconnectIssue,
                "Set PauseOnControllerDisconnect to Auto/Off, or uncheck Override Platform Defaults.",
                failSeverity: OverrideSeverity);
        }

        /// <summary>
        /// Whether the game can freeze on ANY platform the project can be built for (mobile stays silent under the automatic
        /// policy, but the same scene ships to desktop/console/TV where it freezes), so the pause screen is checked regardless
        /// of the active build target.
        /// </summary>
        internal static bool CanFreezeOnSomePlatform(RewiredInputManager manager)
        {
            foreach (bool isMobile in new[] { true, false })
            {
                var policy = ResolvePolicy(manager, isMobile);
                if (policy.AppBackground == AppBackgroundPauseMode.Overlay || policy.PauseOnControllerDisconnect)
                    return true;
            }
#if WAGENHEIMER_STEAMWORKS
            return manager.PauseOnSteamOverlay;
#else
            return false;
#endif
        }

        private static PausePolicy ResolvePolicy(RewiredInputManager manager, bool isMobile) => PausePolicy.Resolve(
            manager.OverridePlatformDefaults, manager.PauseOnAppBackground, manager.PauseOnControllerDisconnect, manager.ResumeOnAnyInput, isMobile);

        private static void AuditPauseScreen(List<AuditResult> results, RewiredInputManager manager)
        {
            if (!CanFreezeOnSomePlatform(manager))
            {
                results.Add(Result(CategoryMobile, "Pause screen", AuditSeverity.Pass, "The game never freezes with the current settings, so no pause screen is needed."));
                return;
            }

            Add(results, CategoryMobile, "Pause screen assigned (Game Paused)", manager.GamePaused != null,
                "Game Paused is assigned.",
                "The game freezes on desktop/console/TV but Game Paused is empty: players would see a frozen game with no pause screen.",
                "Creates a pause screen with a Resume button and links it to Game Paused.", "Create Pause Screen",
                () => DefaultSetupGenerator.CreatePauseScreenAndWire(manager, new SerializedObject(manager)));

            if (manager.GamePaused == null) return;

            AuditResumePath(results, manager);
        }

        /// <summary>On every platform where the game can freeze there must be a way out: tap-to-resume or a Resume button.</summary>
        private static void AuditResumePath(List<AuditResult> results, RewiredInputManager manager)
        {
            bool hasResumeButton = HasResumeButton(manager);
            bool stuckSomewhere = false;
            foreach (bool isMobile in new[] { true, false })
            {
                var policy = ResolvePolicy(manager, isMobile);
                bool freezes = policy.AppBackground == AppBackgroundPauseMode.Overlay || policy.PauseOnControllerDisconnect;
                if (freezes && !policy.ResumeOnAnyInput && !hasResumeButton) stuckSomewhere = true;
            }

            Add(results, CategoryMobile, "A frozen game can always be resumed", !stuckSomewhere,
                hasResumeButton ? "The pause screen has a Resume button." : "Any input resumes.",
                "The game can freeze, tap-anywhere resume is off and the pause screen has no Resume button: the player can get stuck.",
                "Adds a Resume button (wired to RewiredInputManager.Resume) to your pause screen.", "Add Resume Button",
                () => DefaultSetupGenerator.CreatePauseScreenAndWire(manager, new SerializedObject(manager)));
        }

        internal static bool HasResumeButton(RewiredInputManager manager)
        {
            if (manager == null || manager.GamePaused == null) return false;

            foreach (var button in manager.GamePaused.GetComponentsInChildren<Button>(true))
            {
                int count = button.onClick.GetPersistentEventCount();
                for (int i = 0; i < count; i++)
                {
                    if (button.onClick.GetPersistentTarget(i) == manager && button.onClick.GetPersistentMethodName(i) == nameof(RewiredInputManager.Resume))
                        return true;
                }
            }
            return false;
        }

        #endregion

        #region Project

        private static void AuditProject(List<AuditResult> results, RewiredInputManager manager)
        {
            var scripts = SafeGetScripts();

            bool configured = scripts.Any(s => Regex.IsMatch(s.Text,
                @"RewiredInputManager\s*\.\s*(SetGlobalConfiguration|Instance\s*\.\s*Configure)|manager\s*\.\s*Configure\s*\("));
            Add(results, CategoryProject, "Configure()/SetGlobalConfiguration called from game code", configured,
                "Found in project scripts.",
                "Not found: IUiBlocker/IModalStackProvider/IPauseGate stay at their defaults (fine if you don't need them).",
                "Call RewiredInputManager.SetGlobalConfiguration(...) from your bootstrap to veto pauses during ads/IAP/loading.",
                failSeverity: AuditSeverity.Info);

            AuditBlockedScenes(results, manager);
            AuditAndroidRemote(results);
            AuditSteam(results, manager, scripts);
            AuditPackageVersion(results);
        }

        private static void AuditBlockedScenes(List<AuditResult> results, RewiredInputManager manager)
        {
            if (manager == null || manager.ControllerHelpBlockedScenes.Count == 0) return;

            var inBuild = new HashSet<string>(
                EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)), StringComparer.OrdinalIgnoreCase);
            var unknown = manager.ControllerHelpBlockedScenes.Where(n => !string.IsNullOrEmpty(n) && !inBuild.Contains(n)).ToList();

            Add(results, CategoryProject, "ControllerHelpBlockedScenes match Build Settings", unknown.Count == 0,
                "Every blocked scene name exists in Build Settings.",
                "Not in Build Settings (typo or removed scene): " + string.Join(", ", unknown),
                "Fix the names in RewiredInputManager > ControllerHelpBlockedScenes.", failSeverity: AuditSeverity.Warning);
        }

        private static void AuditAndroidRemote(List<AuditResult> results)
        {
            var inputManager = DefaultSetupGenerator.FindInputManagerInScene();
            if (inputManager == null || !AndroidRemoteGlyphSetup.HasAndroidCustomController(inputManager)) return;

            Add(results, CategoryProject, "Android Remote glyphs ready", AndroidRemoteGlyphSetup.IsAndroidRemoteGlyphSetReady(inputManager),
                "Glyph set generated.",
                "AndroidController exists but has no glyph set: TV remote users see raw names ('Left', 'Escape').",
                "Generate the glyph set.", "Ensure Glyphs", () => AndroidRemoteGlyphSetup.EnsureAndroidRemoteGlyphs(),
                AuditSeverity.Warning);
        }

        private static void AuditSteam(List<AuditResult> results, RewiredInputManager manager, List<(string Path, string Text)> scripts)
        {
#if WAGENHEIMER_STEAMWORKS
            if (manager == null || !manager.PauseOnSteamOverlay) return;

            bool isDetectable = FindType("SteamManager") != null || scripts.Any(t => t.Text.Contains("RewiredInputManager.SteamIsInitialized"));
            Add(results, CategoryProject, "Steam readiness can be detected", isDetectable,
                "SteamManager (or an explicit SteamIsInitialized assignment) found.",
                "No SteamManager type found and SteamIsInitialized is never assigned: the Steam overlay will not pause the game.",
                "Add the standard Steamworks.NET SteamManager, or set RewiredInputManager.SteamIsInitialized = true once Steam is up.",
                failSeverity: AuditSeverity.Warning);
#endif
        }

        private static void AuditPackageVersion(List<AuditResult> results)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(RewiredHelperAudit).Assembly);
            results.Add(Result(CategoryProject, "Package version", AuditSeverity.Info,
                package != null ? $"Rewired Helper v{package.version}" : "Version unknown (not installed as a package)."));
        }

        #endregion

        #region Helpers

        internal static void Add(List<AuditResult> results, string category, string title, bool pass, string passDetail,
            string failDetail, string hint, string fixLabel = null, Action fix = null, AuditSeverity failSeverity = AuditSeverity.Fail)
        {
            var result = Result(category, title, pass ? AuditSeverity.Pass : failSeverity, pass ? passDetail : failDetail);
            if (!pass)
            {
                result.FixHint = hint;
                result.FixLabel = fixLabel;
                result.Fix = fix;
            }
            results.Add(result);
        }

        internal static AuditResult Result(string category, string title, AuditSeverity severity, string detail) =>
            new AuditResult { Category = category, Title = title, Severity = severity, Detail = detail };

        /// <summary>True when the project has the standard Steamworks.NET SteamManager the pause logic auto-detects.</summary>
        internal static bool HasSteamManager() => FindType("SteamManager") != null;

        internal static List<T> FindAll<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
#else
            return UnityEngine.Object.FindObjectsOfType<T>(true).ToList();
#endif
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try { type = assembly.GetType(fullName, false); }
                catch { continue; }
                if (type != null) return type;
            }
            return null;
        }

        private static List<(string Path, string Text)> SafeGetScripts()
        {
            var list = new List<(string Path, string Text)>();
            try
            {
                foreach (var path in Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
                    list.Add((path, File.ReadAllText(path)));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RewiredHelperAudit] Failed to scan .cs files: {ex.Message}");
            }
            return list;
        }

        #endregion

        #region Export

        internal static void AttachPrompts(List<AuditResult> results)
        {
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                bool isActionable = r.Severity == AuditSeverity.Fail || r.Severity == AuditSeverity.Warning ||
                                    (r.Severity == AuditSeverity.Info && !string.IsNullOrEmpty(r.FixHint));
                if (!isActionable) continue;

                r.Prompt = $"In the Unity project, resolve this Rewired Helper audit finding.\nCategory: {r.Category}\nFinding: {r.Title}\n" +
                           $"Evidence: {r.Detail}\nExpected fix: {r.FixHint}\n" +
                           "Keep the change minimal and re-run Tools > Wagenheimer > Rewired Helper > Verify Setup to confirm.";
                results[i] = r;
            }
        }

        public static string ToMarkdown(List<AuditResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Rewired Helper - Audit Report").AppendLine();
            sb.AppendLine($"Fails: {results.Count(r => r.Severity == AuditSeverity.Fail)}  |  " +
                          $"Warnings: {results.Count(r => r.Severity == AuditSeverity.Warning)}  |  " +
                          $"Passed: {results.Count(r => r.Severity == AuditSeverity.Pass)}");

            foreach (var group in results.GroupBy(r => r.Category))
            {
                sb.AppendLine().AppendLine($"## {group.Key}");
                foreach (var r in group)
                {
                    sb.AppendLine($"- **[{r.Severity}]** {r.Title}");
                    if (!string.IsNullOrEmpty(r.Detail)) sb.AppendLine($"  - {r.Detail}");
                    if (!string.IsNullOrEmpty(r.FixHint)) sb.AppendLine($"  - Fix: {r.FixHint}");
                }
            }
            return sb.ToString();
        }

        /// <param name="category">When set, only the findings of that category are included.</param>
        public static string ToPromptMarkdown(List<AuditResult> results, string category = null)
        {
            var pending = results
                .Where(r => !string.IsNullOrEmpty(r.Prompt) && (category == null || r.Category == category))
                .ToList();
            if (pending.Count == 0) return "No actionable Rewired Helper findings.";

            var sb = new StringBuilder("Fix every Rewired Helper finding below, then re-run the audit.\n");
            for (int i = 0; i < pending.Count; i++)
                sb.AppendLine().AppendLine($"### {i + 1}. {pending[i].Title}").AppendLine(pending[i].Prompt);
            return sb.ToString();
        }

        #endregion
    }
}
