using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using UnityEditor;

using UnityEngine;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Rewired's UnityUI input module reads <c>ReInput.touch</c> in <c>Awake</c>, but Rewired may still be initializing at
    /// that point: on iOS the platform init is deferred, and the Event System usually awakens in the same frame as the
    /// Rewired Input Manager (they often live in the same prefab). <c>ReInput.touch</c> is null until then, so the module
    /// threw a NullReferenceException inside <c>Awake</c>, its <c>OnEnable</c> never ran, and that session lost controller
    /// UI navigation and clicks - plus one crash report per start.
    /// <para>
    /// Rewired has no setting for this, so the fix is a guard in its integration source, which ships as loose scripts under
    /// <c>Assets/Rewired</c>. This class applies the guard and is idempotent: a Rewired update that restores the original
    /// file gets patched again on the next editor load, and the Setup Audit reports it if that did not happen.
    /// </para>
    /// </summary>
    internal static class RewiredInputModulePatch
    {
        internal const string Marker = "[RewiredHelper]";

        internal const string PointerModuleFile = "RewiredPointerInputModule.cs";
        internal const string StandaloneModuleFile = "RewiredStandaloneInputModule.cs";

        private const string TouchSupportedRead = "isTouchSupported = defaultTouchInputSource.touchSupported;";

        private static readonly Regex TouchSupportedStatement = Statement(
            @"return Rewired\.ReInput\.touch\.touchSupported;");

        private static readonly Regex TouchCountStatement = Statement(
            @"return Rewired\.ReInput\.touch\.touchCount;");

        private static readonly Regex GetTouchStatement = Statement(
            @"return Rewired\.ReInput\.touch\.GetTouch\(index\);");

        private static Regex Statement(string statement) =>
            new Regex(@"(?m)^(?<indent>[ \t]*)" + statement + @"(?<cr>\r?)$", RegexOptions.Compiled);

        /// <summary>True when Rewired's UnityUI integration ships as source and can be guarded.</summary>
        internal static bool IsRewiredPresent() => FindModulePath(PointerModuleFile) != null;

        /// <summary>True when every guard is already in place (or when Rewired is not installed as source).</summary>
        internal static bool IsApplied()
        {
            var pointerPath = FindModulePath(PointerModuleFile);
            if (pointerPath == null) return true;

            if (NeedsPatch(File.ReadAllText(pointerPath))) return false;

            var standalonePath = FindModulePath(StandaloneModuleFile);
            return standalonePath == null || !NeedsStandalonePatch(File.ReadAllText(standalonePath));
        }

        /// <summary>Applies the guard to the project. Returns true when a file changed.</summary>
        internal static bool ApplyToProject()
        {
            var pointerPath = FindModulePath(PointerModuleFile);
            if (pointerPath == null) return false;

            var changed = PatchFile(pointerPath, PatchPointerModule);
            var standalonePath = FindModulePath(StandaloneModuleFile);
            if (standalonePath != null) changed |= PatchFile(standalonePath, PatchStandaloneModule);

            if (changed)
            {
                Debug.Log("[RewiredHelper] Patched Rewired's UnityUI input module: it no longer throws when it awakens before " +
                          "Rewired is initialized (deferred platform init on iOS). The guard is reapplied automatically if a " +
                          "Rewired update restores the original file.");
            }

            return changed;
        }

        /// <summary>Audit fix entry point: patches silently when already applied.</summary>
        internal static void Fix() => ApplyToProject();

        internal static bool NeedsPatch(string pointerSource) =>
            !string.IsNullOrEmpty(pointerSource) &&
            (TouchSupportedStatement.IsMatch(pointerSource) ||
             TouchCountStatement.IsMatch(pointerSource) ||
             GetTouchStatement.IsMatch(pointerSource));

        internal static bool NeedsStandalonePatch(string standaloneSource) =>
            !string.IsNullOrEmpty(standaloneSource) &&
            CountOccurrences(standaloneSource, TouchSupportedRead) < 2;

        /// <summary>Guards the touch getters of <c>RewiredPointerInputModule.UnityInputSource</c>. Pure and idempotent.</summary>
        internal static string PatchPointerModule(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;

            var eol = DetectEol(source);

            source = TouchSupportedStatement.Replace(source, match => Block(match, eol,
                "// " + Marker + " ReInput.touch is null until Rewired is initialized, and the Event System can awaken first.",
                "// Report \"no touch\" instead of throwing; the real value is read again when Rewired is ready.",
                "return Rewired.ReInput.touch != null && Rewired.ReInput.touch.touchSupported;"));

            source = TouchCountStatement.Replace(source, match => Block(match, eol,
                "// " + Marker + " ReInput.touch is null until Rewired is initialized: there are no touches to report.",
                "var touch = Rewired.ReInput.touch;",
                "if(touch == null) return 0;",
                "return touch.touchCount;"));

            source = GetTouchStatement.Replace(source, match => Block(match, eol,
                "// " + Marker + " ReInput.touch is null until Rewired is initialized (touchCount is 0 then).",
                "var touch = Rewired.ReInput.touch;",
                "if(touch == null) return default(Rewired.UnityTouch);",
                "return touch.GetTouch(index);"));

            return source;
        }

        /// <summary>Re-reads <c>isTouchSupported</c> once Rewired is ready. Pure and idempotent.</summary>
        internal static string PatchStandaloneModule(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            if (!NeedsStandalonePatch(source)) return source;

            var methodIndex = source.IndexOf("private void InitializeRewired()", StringComparison.Ordinal);
            if (methodIndex < 0) return source;

            var insertIndex = source.IndexOf("SetupRewiredVars();", methodIndex, StringComparison.Ordinal);
            if (insertIndex < 0) return source;

            var lineStart = source.LastIndexOf('\n', Math.Max(insertIndex - 1, 0));
            var indentStart = lineStart < 0 ? 0 : lineStart + 1;
            var indent = source.Substring(indentStart, insertIndex - indentStart);
            if (indent.Trim().Length != 0) return source; // Unexpected layout: leave the file alone.

            var eol = DetectEol(source);
            var block = indent + "// " + Marker + " Touch support is only readable once Rewired is initialized." + eol +
                        indent + TouchSupportedRead + eol + eol;

            return source.Insert(indentStart, block);
        }

        private static bool PatchFile(string path, Func<string, string> patch)
        {
            var text = File.ReadAllText(path);
            var patched = patch(text);
            if (patched == text) return false;

            // Keep the file's original line endings/encoding: Rewired's scripts are ASCII, but the BOM (when present) must survive.
            var bytes = File.ReadAllBytes(path);
            var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            File.WriteAllText(path, patched, new UTF8Encoding(hasBom));
            AssetDatabase.ImportAsset(path);
            return true;
        }

        private static string Block(Match match, string eol, params string[] lines)
        {
            var indent = match.Groups["indent"].Value;
            return string.Join(eol, lines.Select(line => indent + line)) + match.Groups["cr"].Value;
        }

        internal static string FindModulePath(string fileName)
        {
            var conventional = "Assets/Rewired/Integration/UnityUI/" + fileName;
            if (File.Exists(conventional)) return conventional;

            var suffix = "Integration/UnityUI/" + fileName;
            foreach (var guid in AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(fileName) + " t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid)?.Replace('\\', '/');
                if (path != null && path.EndsWith(suffix, StringComparison.Ordinal)) return path;
            }

            return null;
        }

        private static string DetectEol(string source) => source.Contains("\r\n") ? "\r\n" : "\n";

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var index = 0;

            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }

    /// <summary>
    /// Applies <see cref="RewiredInputModulePatch"/> as soon as the editor loads (including batch builds, so a build made on a
    /// machine that never opened this project still ships the guard). Only touches the disk when the guard is missing.
    /// </summary>
    [InitializeOnLoad]
    internal static class RewiredInputModulePatchRunner
    {
        private const string SessionKey = "Wagenheimer.RewiredHelper.InputModulePatch";

        static RewiredInputModulePatchRunner()
        {
            EditorApplication.delayCall += ApplyIfNeeded;
        }

        private static void ApplyIfNeeded()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            try
            {
                RewiredInputModulePatch.ApplyToProject();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RewiredHelper] Could not patch Rewired's UI input module: " + ex.Message);
            }
        }

        [MenuItem(RewiredHelperMenu.FixRewiredInputModule, priority = RewiredHelperMenu.FixRewiredInputModulePriority)]
        private static void PatchMenu()
        {
            if (!RewiredInputModulePatch.IsRewiredPresent())
            {
                EditorUtility.DisplayDialog("Rewired Helper",
                    "Rewired's UnityUI integration source was not found under Assets/Rewired, so there is nothing to patch.", "OK");
                return;
            }

            var changed = RewiredInputModulePatch.ApplyToProject();
            EditorUtility.DisplayDialog("Rewired Helper",
                changed
                    ? "Rewired's input module was patched. The guard is reapplied automatically after a Rewired update."
                    : "Nothing to do: Rewired's input module is already guarded.", "OK");
        }
    }
}
