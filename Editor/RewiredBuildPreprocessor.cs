using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

using UnityEngine;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Runs before every player build (Unity Build Pipeline, the Build Settings window or the CLI), like
    /// RateControl's preprocessor. The pause policy itself needs no build-time work: it is resolved from the
    /// build platform when the game runs. This step makes that visible and catches the one way to defeat it:
    /// an explicit "Override Platform Defaults" saved in a scene or prefab that breaks mobile best practices.
    /// It only logs; it never modifies assets and never fails the build.
    /// </summary>
    public sealed class RewiredBuildPreprocessor : IPreprocessBuildWithReport
    {
        private const long MaxScannedFileBytes = 8L * 1024 * 1024;

        private static readonly Regex OverrideOn = new Regex(@"^\s*OverridePlatformDefaults:\s*1\s*$", RegexOptions.Multiline);
        private static readonly Regex ForcedOverlay = new Regex(@"^\s*PauseOnAppBackground:\s*3\s*$", RegexOptions.Multiline);
        private static readonly Regex ForcedDisconnectPause = new Regex(@"^\s*PauseOnControllerDisconnect:\s*1\s*$", RegexOptions.Multiline);

        public int callbackOrder => -40;

        public void OnPreprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            bool isMobile = IsMobile(target);
            var policy = PausePolicy.ForPlatform(isMobile);

            var guid = FindManagerScriptGuid();
            var users = guid == null ? new List<string>() : FindAssetsUsingManager(guid);

            if (users.Count == 0)
            {
                Debug.Log($"[RewiredHelper] Build target {target}: no scene/prefab with a RewiredInputManager found (package not used in this build).");
                return;
            }

            Debug.Log($"[RewiredHelper] Build target {target}: automatic pause policy = app background {policy.AppBackground}, " +
                      $"pause on controller disconnect {OnOff(policy.PauseOnControllerDisconnect)}, resume on any input {OnOff(policy.ResumeOnAnyInput)}.");

            if (isMobile)
                WarnAboutBreakingOverrides(users);
        }

        internal static bool IsMobile(BuildTarget target) => target == BuildTarget.Android || target == BuildTarget.iOS;

        private static string OnOff(bool value) => value ? "on" : "off";

        private static string FindManagerScriptGuid()
        {
            foreach (var guid in AssetDatabase.FindAssets("RewiredInputManager t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == "RewiredInputManager.cs")
                    return guid;
            }
            return null;
        }

        /// <summary>Text-scans the scenes in Build Settings and all prefabs for the manager's script GUID (no scene is opened).</summary>
        private static List<string> FindAssetsUsingManager(string scriptGuid)
        {
            var candidates = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
                .Concat(AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath))
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal));

            var users = new List<string>();
            foreach (var path in candidates)
            {
                if (ReadYaml(path, out var text) && text.Contains(scriptGuid))
                    users.Add(path);
            }
            return users;
        }

        private static void WarnAboutBreakingOverrides(List<string> users)
        {
            foreach (var path in users)
            {
                if (!ReadYaml(path, out var text) || !OverrideOn.IsMatch(text)) continue;

                if (ForcedOverlay.IsMatch(text))
                    Debug.LogWarning($"[RewiredHelper] {path}: Override Platform Defaults forces PauseOnAppBackground = Overlay. " +
                                     "On this mobile build every notification shade, ad, IAP sheet or app switch will freeze the game and show the pause screen. " +
                                     "Turn Override Platform Defaults off (Tools > Wagenheimer > Rewired Helper > Dashboard).", AssetDatabase.LoadMainAssetAtPath(path));

                if (ForcedDisconnectPause.IsMatch(text))
                    Debug.LogWarning($"[RewiredHelper] {path}: Override Platform Defaults forces PauseOnControllerDisconnect = On. " +
                                     "Bluetooth pads/remotes connecting and disconnecting will pause a touch game on this mobile build.",
                        AssetDatabase.LoadMainAssetAtPath(path));
            }
        }

        private static bool ReadYaml(string assetPath, out string text)
        {
            text = null;
            try
            {
                var info = new FileInfo(assetPath);
                if (!info.Exists || info.Length > MaxScannedFileBytes) return false;

                text = File.ReadAllText(assetPath);
                return text.StartsWith("%YAML", StringComparison.Ordinal); // binary-serialized assets are skipped
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[RewiredHelper] Could not scan '{assetPath}': {ex.Message}");
                return false;
            }
        }
    }
}
