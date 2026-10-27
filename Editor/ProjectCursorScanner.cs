using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Wagenheimer.RewiredHelper.Editor
{
    public sealed class CursorCodeHit
    {
        public string Path;
        public int Line;
        public string Snippet;

        public string ScriptName => System.IO.Path.GetFileName(Path);
        public UnityEngine.Object Asset => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Path);
    }

    /// <summary>
    /// Finds project scripts that write the OS cursor directly (<c>Cursor.visible</c> /
    /// <c>Cursor.SetCursor</c>). The helper decides cursor visibility from the active input
    /// device, so a second owner shows two pointers (project cursor + OS cursor) or none at all.
    /// </summary>
    public static class ProjectCursorScanner
    {
        static readonly Regex CursorWrite = new Regex(
            @"Cursor\s*\.\s*(visible\s*=|SetCursor\s*\()",
            RegexOptions.Compiled);

        const int MaxFileBytes = 512 * 1024;

        public static List<CursorCodeHit> FindCursorOverrides(int maxHits = 20)
        {
            var hits = new List<CursorCodeHit>();

            IEnumerable<string> scripts;
            try
            {
                scripts = AssetDatabase.FindAssets("t:MonoScript")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                                p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return hits;
            }

            foreach (string path in scripts)
            {
                string text;
                try
                {
                    var info = new FileInfo(path);
                    if (!info.Exists || info.Length == 0 || info.Length > MaxFileBytes) continue;
                    text = File.ReadAllText(path);
                }
                catch
                {
                    continue;
                }

                // Cheap pre-filter: most scripts never mention the cursor.
                if (text.IndexOf("Cursor", StringComparison.Ordinal) < 0) continue;

                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!CursorWrite.IsMatch(lines[i])) continue;

                    hits.Add(new CursorCodeHit
                    {
                        Path = path,
                        Line = i + 1,
                        Snippet = lines[i].Trim()
                    });

                    if (hits.Count >= maxHits) return hits;
                }
            }

            return hits;
        }
    }
}
