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
    /// One-shot helper that migrates a consuming project away from a legacy, project-local
    /// <c>Dialog.cs</c> (global namespace, Assembly-CSharp) onto this package's dialog types
    /// (<see cref="Wagenheimer.RewiredHelper.UI.Dialog"/> / <c>ModalDialogStack</c> / legacy <c>Dialogs</c>).
    ///
    /// It performs three steps, all idempotent:
    /// <list type="number">
    /// <item>Deletes the legacy <c>Dialog.cs</c> script(s) and their <c>.meta</c>.</item>
    /// <item>Repoints every serialized reference (prefabs, scenes, assets, controllers) from the
    /// legacy script GUID to this package's <c>Dialog</c> script GUID, so no "Missing Script" appears.</item>
    /// <item>Adds <c>using Wagenheimer.RewiredHelper.UI;</c> to project scripts that reference the
    /// legacy types but do not import the namespace yet.</item>
    /// </list>
    ///
    /// The migration is intentionally conservative: it never rewrites identifiers, only adds a
    /// using and swaps the script GUID. Review the console report afterwards.
    /// </summary>
    public static class LegacyDialogMigrator
    {
        private const string MenuPath = "Tools/Wagenheimer/Rewired Helper/Migrate Legacy Dialog (delete old Dialog.cs)";
        private const string PackageUsing = "using Wagenheimer.RewiredHelper.UI;";

        private static readonly string[] SerializedExtensions = { ".prefab", ".unity", ".asset", ".controller", ".playable", ".mat" };

        // Tokens that can only come from the legacy global dialog types.
        private static readonly string[] StrongTokens =
        {
            "IBloqueiaDialogosDeSerExibidos",
            "ShowDialogEffect",
            "Dialogs."
        };

        [MenuItem(MenuPath)]
        public static void Migrate()
        {
            var newGuid = FindPackageDialogGuid();
            if (string.IsNullOrEmpty(newGuid))
            {
                EditorUtility.DisplayDialog("Rewired Helper",
                    "Could not find this package's Dialog.cs. Is com.wagenheimer.rewiredhelper installed?",
                    "OK");
                return;
            }

            var legacy = FindLegacyDialogScripts();
            if (legacy.Count == 0)
            {
                EditorUtility.DisplayDialog("Rewired Helper",
                    "No legacy project-local Dialog.cs was found. Nothing to migrate.",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Migrate Legacy Dialog",
                    $"Found {legacy.Count} legacy Dialog.cs file(s):\n\n" +
                    string.Join("\n", legacy.Select(l => l.path)) +
                    "\n\nThis will delete them and repoint serialized references to the package Dialog.\n" +
                    "Make sure the project is under source control (backup) before continuing.",
                    "Migrate", "Cancel"))
                return;

            var repointed = RepointSerializedReferences(legacy, newGuid);

            foreach (var l in legacy)
                AssetDatabase.DeleteAsset(l.path);

            AssetDatabase.Refresh();

            var usingAdded = AddNamespaceUsings(legacy);

            AssetDatabase.Refresh();

            Debug.Log($"[RewiredHelper] Legacy Dialog migration complete. " +
                      $"Deleted {legacy.Count} script(s), repointed {repointed} asset(s), " +
                      $"added the namespace using to {usingAdded} script(s).");
        }

        // ── discovery ──────────────────────────────────────────────────────────────

        private static string FindPackageDialogGuid()
        {
            foreach (var guid in AssetDatabase.FindAssets("Dialog t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (path.IndexOf("RewiredHelper", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!path.EndsWith("/UI/Dialog.cs")) continue;
                if (path.StartsWith("Assets/")) continue; // not the installed package copy
                return guid;
            }

            return null;
        }

        private static List<(string path, string guid)> FindLegacyDialogScripts()
        {
            var result = new List<(string path, string guid)>();

            foreach (var guid in AssetDatabase.FindAssets("Dialog t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (!path.StartsWith("Assets/") || !path.EndsWith("/Dialog.cs")) continue;
                if (path.IndexOf("RewiredHelper", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                // Confirm it really is the legacy global Dialog component (content-based, so it
                // still works while the project is in a broken/compiling state).
                string text;
                try { text = File.ReadAllText(ToAbsolute(path)); }
                catch { continue; }

                if (!Regex.IsMatch(text, @"(^|\s)class\s+Dialog\s*:\s*MonoBehaviour")) continue;
                if (Regex.IsMatch(text, @"\bnamespace\s+[\w\.]+\s*\{")) continue; // namespaced => not the legacy one

                result.Add((path, guid));
            }

            return result;
        }

        // ── serialized reference remap ─────────────────────────────────────────────

        private static int RepointSerializedReferences(List<(string path, string guid)> legacy, string newGuid)
        {
            var legacyGuids = new HashSet<string>(legacy.Select(l => l.guid));
            var dataPath = ToAbsolute("Assets");
            var changed = 0;

            foreach (var file in Directory.GetFiles(dataPath, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!SerializedExtensions.Contains(ext)) continue;

                var text = File.ReadAllText(file);
                if (!legacyGuids.Any(text.Contains)) continue;

                var updated = text;
                foreach (var g in legacyGuids)
                    updated = updated.Replace("guid: " + g, "guid: " + newGuid);

                if (updated != text)
                {
                    File.WriteAllText(file, updated);
                    changed++;
                }
            }

            return changed;
        }

        // ── using injection ────────────────────────────────────────────────────────

        private static int AddNamespaceUsings(List<(string path, string guid)> legacy)
        {
            var legacyPaths = new HashSet<string>(legacy.Select(l => l.path));
            var dataPath = ToAbsolute("Assets");
            var changed = 0;

            foreach (var file in Directory.GetFiles(dataPath, "*.cs", SearchOption.AllDirectories))
            {
                var assetPath = ToAssetPath(file);
                if (legacyPaths.Contains(assetPath)) continue;
                if (assetPath.IndexOf("RewiredHelper", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                var text = File.ReadAllText(file);
                if (text.Contains(PackageUsing)) continue;
                if (!NeedsNamespace(text)) continue;

                var updated = InsertUsing(text);
                if (updated == null || updated == text) continue;

                File.WriteAllText(file, updated, new UTF8Encoding(true));
                changed++;
                Debug.Log($"[RewiredHelper] Added '{PackageUsing}' to {assetPath}");
            }

            return changed;
        }

        private static bool NeedsNamespace(string text)
        {
            if (StrongTokens.Any(text.Contains)) return true;
            return Regex.IsMatch(text, @"\bDialog\b") || Regex.IsMatch(text, @"\bDialogs\b");
        }

        private static string InsertUsing(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();

            var lastUsing = -1;
            for (var i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith("using ") && trimmed.EndsWith(";")) { lastUsing = i; continue; }
                if (lastUsing >= 0 && trimmed.Length > 0 && !trimmed.StartsWith("//")) break;
            }

            if (lastUsing >= 0)
                lines.Insert(lastUsing + 1, PackageUsing);
            else
                lines.Insert(0, PackageUsing);

            return string.Join("\n", lines);
        }

        // ── path helpers ───────────────────────────────────────────────────────────

        private static string ToAbsolute(string assetPath) =>
            Path.Combine(Directory.GetCurrentDirectory(), assetPath).Replace('/', Path.DirectorySeparatorChar);

        private static string ToAssetPath(string absolutePath) =>
            "Assets" + absolutePath.Substring(ToAbsolute("Assets").Length).Replace('\\', '/');
    }
}
