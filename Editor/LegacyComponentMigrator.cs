using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Finds project-local copies of components that this package now ships (same class name, global
    /// namespace, Assembly-CSharp) and swaps them for the package versions:
    /// <list type="number">
    /// <item>repoints every serialized reference (scenes, prefabs, assets) from the old script GUID to the
    /// package script GUID, so nothing turns into "Missing Script";</item>
    /// <item>deletes the old script and its <c>.meta</c>;</item>
    /// <item>adds <c>using Wagenheimer.RewiredHelper;</c> to project scripts that name the type.</item>
    /// </list>
    /// The covered types are the ones the package marks with <c>[MovedFrom]</c>, so field names match.
    /// Project code that used static members of the old copy (for example
    /// <c>ReturnEscapeEvent.EscapePressed</c>) cannot be swapped automatically: <see cref="FindStaticMemberUses"/>
    /// lists those lines so they can be removed by hand.
    /// </summary>
    public static class LegacyComponentMigrator
    {
        private const string PackageUsing = "using Wagenheimer.RewiredHelper;";

        /// <summary>Component names the package owns and a project may still carry its own copy of.</summary>
        public static readonly string[] ComponentNames =
        {
            "ReturnEscapeEvent",
            "EscapeButton",
            "InputVisibilityController"
        };

        private static readonly string[] SerializedExtensions = { ".prefab", ".unity", ".asset", ".controller", ".playable" };

        /// <summary>One project-local script that duplicates a package component.</summary>
        public readonly struct LegacyScript
        {
            public readonly string ClassName;
            public readonly string Path;
            public readonly string Guid;
            public readonly string PackageGuid;

            public LegacyScript(string className, string path, string guid, string packageGuid)
            {
                ClassName = className;
                Path = path;
                Guid = guid;
                PackageGuid = packageGuid;
            }
        }

        [MenuItem(RewiredHelperMenu.MigrateLegacyComponents, priority = RewiredHelperMenu.MigrateLegacyComponentsPriority)]
        public static void Migrate() => MigrateInternal(interactive: true);

        /// <summary>Same migration without prompts, for automation.</summary>
        public static void MigrateSilent() => MigrateInternal(interactive: false);

        private static void MigrateInternal(bool interactive)
        {
            var legacy = FindLegacyScripts();
            if (legacy.Count == 0)
            {
                if (interactive)
                    EditorUtility.DisplayDialog("Rewired Helper", "No project-local copy of a package component was found.", "OK");
                return;
            }

            var staticUses = FindStaticMemberUses();
            if (interactive && !EditorUtility.DisplayDialog("Migrate legacy components",
                    "Found " + legacy.Count + " script(s) that duplicate the package:\n\n" +
                    string.Join("\n", legacy.Select(l => l.Path)) +
                    "\n\nThey will be deleted and every scene/prefab reference repointed to the package component." +
                    (staticUses.Count > 0
                        ? "\n\n" + staticUses.Count + " code line(s) use static members of the old copy and must be removed by hand."
                        : "") +
                    "\n\nCommit (or back up) first: this rewrites scenes and prefabs.",
                    "Migrate", "Cancel"))
                return;

            var repointed = RepointSerializedReferences(legacy);
            foreach (var l in legacy)
                AssetDatabase.DeleteAsset(l.Path);
            AssetDatabase.Refresh();

            var usingAdded = AddNamespaceUsings(legacy);
            AssetDatabase.Refresh();

            Debug.Log("[RewiredHelper] Legacy component migration complete. Deleted " + legacy.Count +
                      " script(s), repointed " + repointed + " asset(s), added the using to " + usingAdded + " script(s).");

            foreach (var use in staticUses)
                Debug.LogWarning("[RewiredHelper] Remove this use of the old copy's static member: " + use);
        }

        // ── discovery ──────────────────────────────────────────────────────────────

        /// <summary>Project-local scripts that duplicate a package component and have a package counterpart.</summary>
        public static List<LegacyScript> FindLegacyScripts()
        {
            var result = new List<LegacyScript>();

            foreach (var name in ComponentNames)
            {
                var packageGuid = FindPackageGuid(name);
                if (string.IsNullOrEmpty(packageGuid)) continue;

                foreach (var guid in AssetDatabase.FindAssets(name + " t:MonoScript"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                    if (!path.StartsWith("Assets/") || !path.EndsWith("/" + name + ".cs")) continue;
                    if (path.IndexOf("RewiredHelper", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                    string text;
                    try { text = File.ReadAllText(ToAbsolute(path)); }
                    catch { continue; }

                    if (!IsLegacyComponentSource(text, name)) continue;
                    result.Add(new LegacyScript(name, path, guid, packageGuid));
                }
            }

            return result;
        }

        /// <summary>
        /// True for a global-namespace <c>class &lt;name&gt; : MonoBehaviour</c>. Content based, so it keeps
        /// working while the project does not compile.
        /// </summary>
        public static bool IsLegacyComponentSource(string text, string className)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (!Regex.IsMatch(text, @"(^|\s)class\s+" + Regex.Escape(className) + @"\s*:\s*MonoBehaviour")) return false;
            return !Regex.IsMatch(text, @"\bnamespace\s+[\w\.]+\s*(\{|;|\r|\n)");
        }

        private static string FindPackageGuid(string className)
        {
            foreach (var guid in AssetDatabase.FindAssets(className + " t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (path.StartsWith("Assets/")) continue;
                if (path.IndexOf("rewiredhelper", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (path.EndsWith("/Runtime/" + className + ".cs")) return guid;
            }

            return null;
        }

        /// <summary>
        /// Project code (outside the legacy scripts) that reads static members the package component does
        /// not have, e.g. <c>ReturnEscapeEvent.EscapePressed = true;</c>. These must be removed by hand.
        /// </summary>
        public static List<string> FindStaticMemberUses()
        {
            var hits = new List<string>();
            var legacyPaths = new HashSet<string>(FindLegacyScripts().Select(l => l.Path));
            var pattern = new Regex(@"\bReturnEscapeEvent\s*\.\s*(EscapePressed|OkPressed)\b");

            foreach (var file in Directory.GetFiles(ToAbsolute("Assets"), "*.cs", SearchOption.AllDirectories))
            {
                var assetPath = ToAssetPath(file);
                if (legacyPaths.Contains(assetPath)) continue;
                if (assetPath.IndexOf("RewiredHelper", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                    if (pattern.IsMatch(lines[i]))
                        hits.Add(assetPath + ":" + (i + 1));
            }

            return hits;
        }

        /// <summary>How many scene/prefab/asset files still point at an old script.</summary>
        public static int CountSerializedReferences(List<LegacyScript> legacy)
        {
            if (legacy.Count == 0) return 0;
            var guids = new HashSet<string>(legacy.Select(l => l.Guid));
            return EnumerateSerializedFiles().Count(f => ContainsAny(SafeRead(f), guids));
        }

        // ── rewrite steps ──────────────────────────────────────────────────────────

        private static int RepointSerializedReferences(List<LegacyScript> legacy)
        {
            var changed = 0;

            foreach (var file in EnumerateSerializedFiles())
            {
                var text = SafeRead(file);
                if (text == null) continue;

                var updated = text;
                foreach (var l in legacy)
                    updated = updated.Replace("guid: " + l.Guid, "guid: " + l.PackageGuid);

                if (updated == text) continue;
                File.WriteAllText(file, updated);
                changed++;
            }

            return changed;
        }

        private static int AddNamespaceUsings(List<LegacyScript> legacy)
        {
            var names = legacy.Select(l => l.ClassName).Distinct().ToList();
            var legacyPaths = new HashSet<string>(legacy.Select(l => l.Path));
            var changed = 0;

            foreach (var file in Directory.GetFiles(ToAbsolute("Assets"), "*.cs", SearchOption.AllDirectories))
            {
                var assetPath = ToAssetPath(file);
                if (legacyPaths.Contains(assetPath)) continue;
                if (assetPath.IndexOf("RewiredHelper", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (assetPath.IndexOf("/Plugins/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (assetPath.IndexOf("/ThirdParty/", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                var text = File.ReadAllText(file);
                if (text.Contains(PackageUsing)) continue;

                var code = StripComments(text);
                if (!names.Any(n => Regex.IsMatch(code, @"\b" + Regex.Escape(n) + @"\b"))) continue;

                var updated = InsertUsing(text);
                if (updated == text) continue;
                File.WriteAllText(file, updated, new UTF8Encoding(true));
                changed++;
            }

            return changed;
        }

        /// <summary>Adds <see cref="PackageUsing"/> after the last using (or at the top). Pure, for tests.</summary>
        public static string InsertUsing(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            var lastUsing = -1;

            for (var i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith("using ") && trimmed.EndsWith(";")) { lastUsing = i; continue; }
                if (lastUsing >= 0 && trimmed.Length > 0 && !trimmed.StartsWith("//")) break;
            }

            lines.Insert(lastUsing + 1, PackageUsing);
            return string.Join("\n", lines);
        }

        private static string StripComments(string text)
        {
            text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(text, @"//[^\n]*", string.Empty);
        }

        // ── helpers ────────────────────────────────────────────────────────────────

        private static IEnumerable<string> EnumerateSerializedFiles() =>
            Directory.GetFiles(ToAbsolute("Assets"), "*", SearchOption.AllDirectories)
                .Where(f => SerializedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

        private static string SafeRead(string file)
        {
            try { return File.ReadAllText(file); }
            catch { return null; }
        }

        private static bool ContainsAny(string text, IEnumerable<string> guids) =>
            text != null && guids.Any(text.Contains);

        private static string ToAbsolute(string assetPath) =>
            System.IO.Path.Combine(Directory.GetCurrentDirectory(), assetPath).Replace('/', System.IO.Path.DirectorySeparatorChar);

        private static string ToAssetPath(string absolutePath) =>
            "Assets" + absolutePath.Substring(ToAbsolute("Assets").Length).Replace('\\', '/');
    }
}
