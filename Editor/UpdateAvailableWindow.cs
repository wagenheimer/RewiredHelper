using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Wagenheimer.RewiredHelper.Editor
{
    internal class UpdateAvailableWindow : EditorWindow
    {
        string _packageDisplayName;
        string _currentVersion;
        string _latestVersion;
        string _repoUrl;
        string _gitUrl;
        string _releaseNotes;
        string _skipPrefKey;
        Vector2 _notesScroll;

        AddRequest _addRequest;
        bool _updating;
        string _updateError;

        public static void Show(string packageDisplayName, string currentVersion, string latestVersion,
            string repoUrl, string gitUrl, string releaseNotes, string skipPrefKey)
        {
            var window = CreateInstance<UpdateAvailableWindow>();
            window.titleContent = new GUIContent($"{packageDisplayName} — Update Available");
            window._packageDisplayName = packageDisplayName;
            window._currentVersion = currentVersion;
            window._latestVersion = latestVersion;
            window._repoUrl = repoUrl;
            window._gitUrl = gitUrl;
            window._releaseNotes = string.IsNullOrEmpty(releaseNotes)
                ? "(Sem notas de versão disponíveis — veja o Changelog completo no GitHub.)"
                : releaseNotes;
            window._skipPrefKey = skipPrefKey;

            var size = new Vector2(440, 380);
            window.minSize = size;
            window.maxSize = new Vector2(600, 700);
            window.ShowUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Nova versão do {_packageDisplayName} disponível!", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Instalada: {_currentVersion}    →    Disponível: {_latestVersion}");
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Novidades desta versão", EditorStyles.boldLabel);
            _notesScroll = EditorGUILayout.BeginScrollView(_notesScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField(_releaseNotes, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_updateError))
                EditorGUILayout.HelpBox(_updateError, MessageType.Error);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_updating))
                {
                    if (GUILayout.Button(_updating ? "Atualizando…" : "Atualizar Agora", GUILayout.Height(28)))
                        StartUpdate();
                }

                if (GUILayout.Button("Ver Changelog Completo"))
                    Application.OpenURL($"{_repoUrl}/blob/main/CHANGELOG.md");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Lembrar depois"))
                    Close();

                if (GUILayout.Button("Ignorar esta versão"))
                {
                    EditorPrefs.SetString(_skipPrefKey, _latestVersion);
                    Close();
                }
            }
        }

        void StartUpdate()
        {
            _updating = true;
            _updateError = null;
            _addRequest = Client.Add(_gitUrl);
            EditorApplication.update += PollUpdate;
        }

        void PollUpdate()
        {
            if (_addRequest == null || !_addRequest.IsCompleted)
                return;

            EditorApplication.update -= PollUpdate;

            if (this == null)
                return;

            _updating = false;

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[{_packageDisplayName}] Atualizado para a versão {_addRequest.Result.version}.");
                Close();
            }
            else
            {
                _updateError = _addRequest.Error?.message ?? "Falha desconhecida ao atualizar.";
                Debug.LogError($"[{_packageDisplayName}] Falha ao atualizar: {_updateError}");
                Repaint();
            }
        }

        void OnDestroy()
        {
            EditorApplication.update -= PollUpdate;
        }
    }
}
