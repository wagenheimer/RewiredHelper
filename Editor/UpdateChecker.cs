using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Wagenheimer.RewiredHelper.Editor
{
    [InitializeOnLoad]
    internal static class UpdateChecker
    {
        const string PackageDisplayName = "Rewired Helper";
        internal const string GitUrl = "https://github.com/wagenheimer/RewiredHelper.git";
        const string PackageJsonUrl = "https://raw.githubusercontent.com/wagenheimer/RewiredHelper/main/package.json";
        const string ChangelogUrl = "https://raw.githubusercontent.com/wagenheimer/RewiredHelper/main/CHANGELOG.md";
        const string RepoUrl = "https://github.com/wagenheimer/RewiredHelper";
        const string PrefLastCheckTicks = "Wagenheimer.RewiredHelper.UpdateChecker.LastCheckTicks";
        const string PrefSkipVersion = "Wagenheimer.RewiredHelper.UpdateChecker.SkipVersion";
        const double CheckIntervalHours = 24;

        static UpdateChecker()
        {
            EditorApplication.delayCall += () => CheckForUpdate(force: false);
        }

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Check for Updates...", priority = 41)]
        static void CheckForUpdateMenuItem() => CheckForUpdate(force: true);

        static void CheckForUpdate(bool force)
        {
            if (!force && !IntervalElapsed())
                return;

            var request = UnityWebRequest.Get(PackageJsonUrl);
            request.timeout = 5;
            var op = request.SendWebRequest();
            op.completed += _ => OnPackageJsonComplete(request, force);
        }

        static bool IntervalElapsed()
        {
            var stored = EditorPrefs.GetString(PrefLastCheckTicks, "0");
            if (!long.TryParse(stored, out var ticks))
                return true;

            return (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalHours >= CheckIntervalHours;
        }

        static void OnPackageJsonComplete(UnityWebRequest request, bool force)
        {
            EditorPrefs.SetString(PrefLastCheckTicks, DateTime.UtcNow.Ticks.ToString());

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log($"[RewiredHelper] Update check failed: {request.error}");
                if (force)
                    EditorUtility.DisplayDialog(PackageDisplayName, $"Falha ao verificar atualizações:\n{request.error}", "OK");
                request.Dispose();
                return;
            }

            string remoteVersion = null;
            try
            {
                remoteVersion = JsonUtility.FromJson<PackageJsonVersionOnly>(request.downloadHandler.text)?.version;
            }
            catch (Exception e)
            {
                Debug.Log($"[RewiredHelper] Update check failed: could not parse remote package.json ({e.Message})");
            }

            request.Dispose();

            var localVersion = GetLocalVersion();
            if (string.IsNullOrEmpty(remoteVersion))
            {
                Debug.Log("[RewiredHelper] Update check failed: remote package.json has no version field.");
                if (force)
                    EditorUtility.DisplayDialog(PackageDisplayName, "Falha ao verificar atualizações: o package.json remoto não tem campo de versão.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(localVersion))
            {
                Debug.Log("[RewiredHelper] Update check failed: could not resolve the installed package version " +
                    "(PackageInfo.FindForAssembly returned null for this assembly).");
                if (force)
                    EditorUtility.DisplayDialog(PackageDisplayName, "Falha ao verificar atualizações: não foi possível identificar a versão instalada deste pacote.", "OK");
                return;
            }

            if (!IsNewer(remoteVersion, localVersion))
            {
                Debug.Log($"[RewiredHelper] Up to date (installed: {localVersion}).");
                if (force)
                    EditorUtility.DisplayDialog(PackageDisplayName, $"Você já está usando a versão mais recente ({localVersion}).", "OK");
                return;
            }

            if (!force && EditorPrefs.GetString(PrefSkipVersion, "") == remoteVersion)
            {
                Debug.Log($"[RewiredHelper] Version {remoteVersion} available (installed: {localVersion}) but ignored by user preference.");
                return;
            }

            Debug.Log($"[RewiredHelper] New version available: {remoteVersion} (installed: {localVersion}). See {RepoUrl}/releases/latest");
            FetchChangelogAndShow(localVersion, remoteVersion);
        }

        static void FetchChangelogAndShow(string localVersion, string remoteVersion)
        {
            var request = UnityWebRequest.Get(ChangelogUrl);
            request.timeout = 5;
            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                string notes = null;
                if (request.result == UnityWebRequest.Result.Success)
                    notes = ExtractVersionNotes(request.downloadHandler.text, remoteVersion);

                request.Dispose();
                UpdateAvailableWindow.Show("Rewired Helper", localVersion, remoteVersion, RepoUrl, GitUrl, notes, PrefSkipVersion);
            };
        }

        static string ExtractVersionNotes(string changelog, string version)
        {
            var marker = $"## [{version}]";
            var start = changelog.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                return null;

            var bodyStart = changelog.IndexOf('\n', start);
            if (bodyStart < 0)
                return null;

            var next = changelog.IndexOf("\n## [", bodyStart, StringComparison.Ordinal);
            var end = next >= 0 ? next : changelog.Length;
            return changelog.Substring(bodyStart, end - bodyStart).Trim();
        }

        static string GetLocalVersion()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UpdateChecker).Assembly);
            return packageInfo?.version;
        }

        static bool IsNewer(string remote, string local)
        {
            if (Version.TryParse(remote, out var remoteVer) && Version.TryParse(local, out var localVer))
                return remoteVer > localVer;

            return string.CompareOrdinal(remote, local) > 0;
        }

        [Serializable]
        class PackageJsonVersionOnly
        {
            public string version;
        }
    }
}
