using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Wagenheimer.RewiredHelper.Editor
{
    public sealed class InputCodeHit
    {
        public string Path;
        public int Line;
        public string Snippet;

        public string ScriptName => System.IO.Path.GetFileName(Path);
        public UnityEngine.Object Asset => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Path);
    }

    /// <summary>
    /// Finds project scripts that read Escape / Return / Submit / Cancel through legacy
    /// <c>Input</c>. <see cref="RewiredInputManager"/> already routes those (MenuButton/BackButton plus
    /// the <see cref="IModalStackProvider"/> stack), so a second handler makes the same dialog button
    /// fire twice — or cancels the same screen twice.
    /// Add <c>rewired-helper: ignore-input</c> to a file to opt out (editor-only shortcuts, etc.).
    /// </summary>
    public static class ProjectInputScanner
    {
        public const string IgnoreMarker = "rewired-helper: ignore-input";

        static readonly Regex RawInput = new Regex(
            @"Input\s*\.\s*(GetKey|GetKeyDown|GetKeyUp)\s*\(\s*KeyCode\s*\.\s*(Escape|Return|KeypadEnter)\s*\)" +
            @"|Input\s*\.\s*GetButton(?:Down|Up)?\s*\(\s*""(Submit|Cancel)""\s*\)",
            RegexOptions.Compiled);

        const int MaxFileBytes = 512 * 1024;

        public static List<InputCodeHit> FindRawInput(int maxHits = 20)
        {
            var hits = new List<InputCodeHit>();

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

                if (text.IndexOf("Input", StringComparison.Ordinal) < 0) continue;
                if (text.IndexOf(IgnoreMarker, StringComparison.OrdinalIgnoreCase) >= 0) continue;

                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!RawInput.IsMatch(lines[i])) continue;

                    hits.Add(new InputCodeHit
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
