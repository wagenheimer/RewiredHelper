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
        private const string CategoryScene = "Scene Setup";
        private const string CategoryMobile = "Mobile & Pause";
        private const string CategoryProject = "Project";

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Verify Setup...", priority = 139)]
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

            AuditSceneSetup(results, manager);
            AuditMobilePause(results, manager);
            AuditProject(results, manager);
            AttachPrompts(results);

            return results;
        }

        internal static bool IsMobileTarget =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ||
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;

        #region Scene setup

        private static void AuditSceneSetup(List<AuditResult> results, RewiredInputManager manager)
        {
            var inputManager = DefaultSetupGenerator.FindInputManagerInScene();
            Add(results, CategoryScene, "Rewired Input Manager in the scene", inputManager != null,
                "Found in the open scene.", "Missing: no Rewired input will be processed.",
                "Create it with the Rewired Helper generator.", "Create Manager", DefaultSetupGenerator.CreateRewiredInputManager);

            Add(results, CategoryScene, "RewiredInputManager component", manager != null,
                "Component present.", "Missing: input-type tracking, cursor and Escape routing will not work.",
                "Add RewiredInputManager next to the Rewired Input Manager.", "Configure", DefaultSetupGenerator.CreateRewiredInputManager);

            Add(results, CategoryScene, "Event System uses Rewired's input module", DefaultSetupGenerator.HasRewiredEventSystemInScene(),
                "RewiredStandaloneInputModule active.",
                "Missing: controller UI navigation and Player Mouse will not work (Unity's default module ignores Rewired).",
                "Create/repair the Event System.", "Create Event System", DefaultSetupGenerator.EnsureRewiredEventSystem);

            AuditDuplicateEventSystems(results);
            AuditCanvasAndCursor(results, manager);
            AuditOptionalAddons(results);
        }

        private static void AuditDuplicateEventSystems(List<AuditResult> results)
        {
            int count = FindAll<EventSystem>().Count;
            Add(results, CategoryScene, "Single Event System", count <= 1,
                "No duplicate Event Systems in the open scene.",
                $"{count} Event Systems in the open scene: Unity logs 'There can be only one active Event System'.",
                "Keep only the one running Rewired's input module.", "Remove Duplicates (All Scenes)",
                DefaultSetupGenerator.RemoveDuplicateEventSystemsInAllScenes, AuditSeverity.Warning);
        }

        private static void AuditCanvasAndCursor(List<AuditResult> results, RewiredInputManager manager)
        {
            Add(results, CategoryScene, "UI Canvas", FindAll<Canvas>().Count > 0,
                "Canvas found.", "No Canvas in the open scene (needed for the game cursor and modal dialogs).",
                "Add a Screen Space - Overlay Canvas.", failSeverity: AuditSeverity.Warning);

            if (manager == null) return;

            // Touch-only titles never draw a game cursor, so a missing one is only a hint on mobile.
            var missing = IsMobileTarget ? AuditSeverity.Info : AuditSeverity.Warning;
            var so = new SerializedObject(manager);
            Add(results, CategoryScene, "Game Cursor (controller/remote cursor image)", manager.GameCursor != null,
                "Game Cursor image assigned.", "Not assigned: gamepad/remote users get no on-screen cursor.",
                "Generate and link a Game Cursor.", "Create Game Cursor",
                () => DefaultSetupGenerator.CreateGameCursorAndWire(manager, so), missing);
        }

        private static void AuditOptionalAddons(List<AuditResult> results)
        {
            bool hasGlyphs = FindType("Rewired.Glyphs.UnityUI.UnityUITextMeshProGlyphHelper") != null;
            Add(results, CategoryScene, "Rewired Glyphs add-on", hasGlyphs,
                "Detected in the project.", "Not installed: UI labels cannot show dynamic controller icons.",
                "Extract Assets/Rewired/Internal/Assets/Extras/GlyphsUnityUITMProAddonV2.zip (optional).", failSeverity: AuditSeverity.Info);

            if (FindType("I2.Loc.LocalizationManager") == null) return;

            bool hasIntegration = FindType("Wagenheimer.RewiredHelper.Integration.I2SpecializationImportedMarker") != null;
            Add(results, CategoryScene, "I2 Localization integration imported", hasIntegration,
                "Integration sample is imported.", "I2 Localization detected, but the specialization helper sample is not imported.",
                "Import the I2 Localization Integration sample.", "Import Integration", I2IntegrationImporter.Import,
                AuditSeverity.Warning);

            bool termsOk = DefaultSetupGenerator.AllI2TermsExist(out int missingCount);
            Add(results, CategoryScene, "I2 Localization terms", termsOk,
                "All Rewired Helper terms exist.", $"{missingCount} term(s) missing or without an English translation.",
                "Add the missing terms to the I2 Language Source.", "Verify/Add Terms", DefaultSetupGenerator.EnsureI2Terms,
                AuditSeverity.Warning);
        }

        #endregion

        #region Mobile & pause

        private static void AuditMobilePause(List<AuditResult> results, RewiredInputManager manager)
        {
            if (manager == null)
            {
                results.Add(Result(CategoryMobile, "Pause policy", AuditSeverity.Info,
                    "No RewiredInputManager in the open scene, pause settings not checked."));
                return;
            }

            AuditAutoDefaults(results, manager);
            AuditResumePath(results, manager);
        }

        /// <summary>
        /// Explicit Overlay/On values are saved in the scene and win over the per-platform Auto defaults, so
        /// they are the way a mobile build ends up with the "GAME PAUSED on every app switch" behavior.
        /// </summary>
        private static void AuditAutoDefaults(List<AuditResult> results, RewiredInputManager manager)
        {
            Add(results, CategoryMobile, "App background pause follows the platform default",
                manager.PauseOnAppBackground != AppBackgroundPauseMode.Overlay,
                $"PauseOnAppBackground = {manager.PauseOnAppBackground}.",
                "PauseOnAppBackground is forced to Overlay: on Android/iOS every notification shade, ad, IAP sheet or app switch freezes the game and shows 'GAME PAUSED'.",
                "Set it to Auto (Silent on mobile, Overlay on desktop/console).", "Set to Auto",
                () => ApplyAutoPreset(manager), AuditSeverity.Warning);

            Add(results, CategoryMobile, "Controller-disconnect pause follows the platform default",
                manager.PauseOnControllerDisconnect != AutoToggle.On,
                $"PauseOnControllerDisconnect = {manager.PauseOnControllerDisconnect}.",
                "PauseOnControllerDisconnect is forced On: Bluetooth pads/remotes connecting and disconnecting will pause a touch game on Android/iOS.",
                "Set it to Auto (off on mobile, on elsewhere).", "Set to Auto",
                () => ApplyAutoPreset(manager), AuditSeverity.Warning);
        }

        private static void AuditResumePath(List<AuditResult> results, RewiredInputManager manager)
        {
            var mode = manager.EffectiveAppBackgroundMode;
            bool canFreeze = mode == AppBackgroundPauseMode.Overlay || manager.ShouldPauseOnControllerDisconnect;
#if WAGENHEIMER_STEAMWORKS
            canFreeze |= manager.PauseOnSteamOverlay;
#endif
            if (!canFreeze) return;

            bool hasResumeButton = HasResumeButton(manager);
            var so = new SerializedObject(manager);

            Add(results, CategoryMobile, "A frozen game can always be resumed", manager.ShouldResumeOnAnyInput || hasResumeButton,
                manager.ShouldResumeOnAnyInput ? "Any input resumes." : "The pause screen has a Resume button wired to Resume().",
                "The game can freeze, tap-anywhere resume is off and the pause screen has no Resume button: the player can get stuck.",
                "Add a Resume button (wired to RewiredInputManager.Resume) to the pause screen.", "Add Resume Button",
                () => DefaultSetupGenerator.CreatePauseScreenAndWire(manager, so));
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

        /// <summary>Resets the pause policy to the per-platform Auto defaults (mobile-safe on Android/iOS, classic elsewhere).</summary>
        internal static void ApplyAutoPreset(RewiredInputManager manager)
        {
            if (manager == null) return;

            Undo.RecordObject(manager, "Apply Rewired Helper auto pause preset");
            manager.PauseOnAppBackground = AppBackgroundPauseMode.Auto;
            manager.PauseOnControllerDisconnect = AutoToggle.Auto;
            manager.ResumeOnAnyInput = AutoToggle.Auto;
            EditorUtility.SetDirty(manager);
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

            bool isAssigned = scripts.Any(s => s.Text.Contains("RewiredInputManager.SteamIsInitialized"));
            Add(results, CategoryProject, "SteamIsInitialized is assigned", isAssigned,
                "Assigned in project scripts.", "Never assigned: the Steam overlay will not pause the game.",
                "Set RewiredInputManager.SteamIsInitialized = SteamManager.Initialized once Steam is up.",
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

        private static void Add(List<AuditResult> results, string category, string title, bool pass, string passDetail,
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

        private static AuditResult Result(string category, string title, AuditSeverity severity, string detail) =>
            new AuditResult { Category = category, Title = title, Severity = severity, Detail = detail };

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

        private static void AttachPrompts(List<AuditResult> results)
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

        public static string ToPromptMarkdown(List<AuditResult> results)
        {
            var pending = results.Where(r => !string.IsNullOrEmpty(r.Prompt)).ToList();
            if (pending.Count == 0) return "No actionable Rewired Helper findings.";

            var sb = new StringBuilder("Fix every Rewired Helper finding below, then re-run the audit.\n");
            for (int i = 0; i < pending.Count; i++)
                sb.AppendLine().AppendLine($"### {i + 1}. {pending[i].Title}").AppendLine(pending[i].Prompt);
            return sb.ToString();
        }

        #endregion
    }
}
