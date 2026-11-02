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
    /// Finds project scripts that:
    /// <list type="number">
    /// <item><description>read Escape / Return / Submit / Cancel through legacy <c>Input</c> — the helper already
    /// routes those, so a second handler fires the same dialog button twice;</description></item>
    /// <item><description>read mouse/keyboard-only APIs that a gamepad cannot reach (<c>Input.GetMouseButton</c>,
    /// <c>Input.anyKey</c>, <c>Input.GetKey</c>, <c>Input.GetButton("name")</c>, <c>Input.mousePosition</c>) —
    /// a joystick-only player simply cannot trigger them;</description></item>
    /// <item><description>expose a Custom Cursor preference to a UI Toggle without reacting to the active device —
    /// the option must disappear when the player uses a joystick/touch.</description></item>
    /// </list>
    /// Add <c>rewired-helper: ignore-input</c> to a file to opt out of every scan (deliberate legacy input),
    /// or <c>rewired-helper: ignore-legacy-input</c> to opt out of the gamepad-reachability scan only
    /// (developer shortcuts that read a key while the helper-managed Escape/Return scan must keep running).
    /// </summary>
    public static class ProjectInputScanner
    {
        public const string IgnoreMarker = "rewired-helper: ignore-input";
        public const string IgnoreLegacyInputMarker = "rewired-helper: ignore-legacy-input";

        /// <summary>
        /// Optional, project-controlled list of path prefixes to skip (one per line, <c>#</c> starts a
        /// comment). Use it for third-party assets/folders whose own examples read Unity's legacy Input.
        /// <c>Assets/Rewired/</c> and any <c>/Editor/</c> folder are always skipped.
        /// </summary>
        public const string IgnoreFile = "ProjectSettings/RewiredHelperIgnore.txt";

        static readonly string[] DefaultIgnoredPrefixes = { "Assets/Rewired/" };

        static readonly Regex RawInput = new Regex(
            @"Input\s*\.\s*(GetKey|GetKeyDown|GetKeyUp)\s*\(\s*KeyCode\s*\.\s*(Escape|Return|KeypadEnter)\s*\)" +
            @"|Input\s*\.\s*GetButton(?:Down|Up)?\s*\(\s*""(Submit|Cancel)""\s*\)",
            RegexOptions.Compiled);

        /// <summary>
        /// Mouse/keyboard-only reads. Touch APIs are intentionally NOT included: touch is a valid device of
        /// its own, not a gamepad-only gap.
        /// </summary>
        static readonly Regex LegacyInput = new Regex(
            @"\bInput\s*\.\s*(?<api>GetMouseButton(?:Down|Up)?|mousePosition|mouseScrollDelta|anyKey(?:Down|Up)?|" +
            @"GetKey(?:Down|Up)?|GetButton(?:Down|Up)?|GetAxis(?:Raw)?)\b",
            RegexOptions.Compiled);

        /// <summary>Tokens that prove the script reacts to the active input device.</summary>
        static readonly string[] DeviceAwareTokens =
        {
            "InputVisibilityController",
            "ShowOnMouseOrKeyboardHideOnJoystickOrTouch",
            "OnInputSpecializationChanged",
            "IsUsingJoystickCursor",
            "CurrentControllerType",
            "CanShowCustomCursor",
            "IsUsingTouch"
        };

        const int MaxFileBytes = 512 * 1024;

        public static List<InputCodeHit> FindRawInput(int maxHits = 20)
        {
            return Scan(
                line => RawInput.IsMatch(line),
                maxHits, preFilter: "Input", ignoreMarkers: new[] { IgnoreMarker });
        }

        /// <summary>
        /// Lines that read hardware mouse/keyboard state directly. A gamepad (and the helper's virtual
        /// cursor, which does not move <c>Input.mousePosition</c>) can never satisfy them, so the action
        /// is unreachable for a controller-only player.
        /// </summary>
        public static List<InputCodeHit> FindLegacyInput(int maxHits = 40)
        {
            return Scan(
                line => IsLegacyInputLine(line, out _),
                maxHits, preFilter: "Input",
                ignoreMarkers: new[] { IgnoreMarker, IgnoreLegacyInputMarker });
        }

        /// <summary>
        /// Pure, unit-tested rule for one source line. Comments and the Escape/Return pattern
        /// (already reported by <see cref="FindRawInput"/>) are ignored.
        /// </summary>
        public static bool IsLegacyInputLine(string line, out string api)
        {
            api = null;
            if (string.IsNullOrEmpty(line)) return false;

            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith("*", StringComparison.Ordinal) ||
                trimmed.StartsWith("/*", StringComparison.Ordinal))
                return false;

            if (RawInput.IsMatch(line)) return false;

            var match = LegacyInput.Match(line);
            if (!match.Success) return false;

            api = match.Groups["api"].Value;
            return true;
        }

        /// <summary>
        /// Files that expose a Custom Cursor preference to a Toggle/Selectable but never look at the
        /// active device. The OS cursor texture only exists on the mouse; with a joystick/touch the helper
        /// draws its own cursor, so the option does nothing and should disappear.
        /// </summary>
        public static List<string> FindDeviceAgnosticCustomCursorOptions(int maxFiles = 10)
        {
            var files = new List<string>();

            foreach (var (path, text) in ReadProjectScripts(preFilter: "CustomCursor",
                         ignoreMarkers: new[] { IgnoreMarker, IgnoreLegacyInputMarker }))
            {
                if (!NeedsDeviceAwareCustomCursor(text)) continue;

                files.Add(path);
                if (files.Count >= maxFiles) break;
            }

            return files;
        }

        /// <summary>Pure, unit-tested rule behind <see cref="FindDeviceAgnosticCustomCursorOptions"/>.</summary>
        public static bool NeedsDeviceAwareCustomCursor(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (text.IndexOf("CustomCursor", StringComparison.OrdinalIgnoreCase) < 0) return false;

            bool hasUiControl = text.IndexOf("Toggle", StringComparison.Ordinal) >= 0 ||
                                text.IndexOf("Selectable", StringComparison.Ordinal) >= 0;
            if (!hasUiControl) return false;

            foreach (string token in DeviceAwareTokens)
            {
                if (text.IndexOf(token, StringComparison.Ordinal) >= 0) return false;
            }

            return true;
        }

        private static List<InputCodeHit> Scan(Func<string, bool> isHit, int maxHits,
            string preFilter, string[] ignoreMarkers)
        {
            var hits = new List<InputCodeHit>();

            foreach (var (path, text) in ReadProjectScripts(preFilter, ignoreMarkers))
            {
                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!isHit(lines[i])) continue;

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

        private static List<(string Path, string Text)> ReadProjectScripts(string preFilter, string[] ignoreMarkers)
        {
            var files = new List<(string Path, string Text)>();

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
                return files;
            }

            var ignoredPrefixes = ReadIgnoredPathPrefixes();

            foreach (string path in scripts)
            {
                if (IsIgnoredPath(path, ignoredPrefixes)) continue;

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

                if (preFilter != null && text.IndexOf(preFilter, StringComparison.Ordinal) < 0) continue;

                bool ignored = false;
                foreach (string marker in ignoreMarkers)
                {
                    if (text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    ignored = true;
                    break;
                }
                if (ignored) continue;

                files.Add((path, text));
            }

            return files;
        }

        /// <summary>Prefixes from <see cref="IgnoreFile"/>, plus the always-ignored Rewired asset folder.</summary>
        private static List<string> ReadIgnoredPathPrefixes()
        {
            var prefixes = new List<string>(DefaultIgnoredPrefixes);

            try
            {
                string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot)) return prefixes;

                string file = Path.Combine(projectRoot, IgnoreFile);
                if (!File.Exists(file)) return prefixes;

                foreach (string raw in File.ReadAllLines(file))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    prefixes.Add(line);
                }
            }
            catch
            {
                // A malformed/half-saved ignore file must never break the audit.
            }

            return prefixes;
        }

        /// <summary>
        /// Pure, unit-tested path filter: any <c>/Editor/</c> folder and every prefix from the ignore file
        /// (or <see cref="DefaultIgnoredPrefixes"/>) is skipped.
        /// </summary>
        public static bool IsIgnoredPath(string path, IEnumerable<string> ignoredPrefixes)
        {
            if (string.IsNullOrEmpty(path)) return false;

            string normalized = path.Replace('\\', '/');
            if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            if (ignoredPrefixes == null) return false;

            foreach (string prefix in ignoredPrefixes)
            {
                if (string.IsNullOrWhiteSpace(prefix)) continue;

                string clean = prefix.Replace('\\', '/').Trim().TrimEnd('/');
                if (clean.Length == 0) continue;

                if (normalized.Equals(clean, StringComparison.OrdinalIgnoreCase) ||
                    normalized.StartsWith(clean + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
