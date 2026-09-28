using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Detects and migrates the legacy global <c>RewiredHelper</c> component (a backward-compatibility
    /// subclass of <see cref="RewiredInputManager"/>, kept for projects that never renamed it) to a plain
    /// <see cref="RewiredInputManager"/>. Since the subclass adds zero serialized fields, migration swaps the
    /// component's script reference in place (the same technique Unity itself uses for an in-place class
    /// rename): no data is lost and no other object's reference to this exact component instance breaks,
    /// unlike destroying the old component and adding a new one.
    /// </summary>
    public static class RewiredLegacyMigrator
    {
        private const string MenuPath = "Tools/Wagenheimer/Rewired Helper/Migrate Legacy RewiredHelper Component...";

        [MenuItem(MenuPath, priority = 145)]
        public static void MenuMigrateProject()
        {
            var script = FindInputManagerScript();
            if (script == null)
            {
                EditorUtility.DisplayDialog("Rewired Helper",
                    "Could not find the RewiredInputManager script asset. Is com.wagenheimer.rewiredhelper installed correctly?", "OK");
                return;
            }

            var scenePaths = ScenesToScan();
            var prefabPaths = AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath).ToList();

            int sceneHits = scenePaths.Sum(p => CountLegacyInScene(p));
            int prefabHits = prefabPaths.Sum(p => CountLegacyInPrefab(p));

            if (sceneHits + prefabHits == 0)
            {
                EditorUtility.DisplayDialog("Rewired Helper",
                    "No legacy RewiredHelper component was found in any scene or prefab. Nothing to migrate.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Migrate Legacy RewiredHelper Component",
                    $"Found {sceneHits} instance(s) in scenes and {prefabHits} in prefabs.\n\n" +
                    "Each will be swapped in place to RewiredInputManager (same data, no component recreated).\n" +
                    "Make sure the project is under source control before continuing.",
                    "Migrate", "Cancel"))
                return;

            int migratedScenes = MigrateAllScenes(script, scenePaths);
            int migratedPrefabs = MigrateAllPrefabs(script, prefabPaths);

            Debug.Log($"[RewiredHelper] Legacy component migration complete: {migratedScenes} scene instance(s), {migratedPrefabs} prefab instance(s).");
            EditorUtility.DisplayDialog("Migration Complete",
                $"Migrated {migratedScenes} scene instance(s) and {migratedPrefabs} prefab instance(s) to RewiredInputManager.", "OK");
        }

        #region Detection (used by the Setup Audit / Inspector one-click fix)

        /// <summary>True for a genuine legacy subclass (e.g. <c>global::RewiredHelper</c>), false for a plain RewiredInputManager.</summary>
        internal static bool IsLegacyComponent(RewiredInputManager component) =>
            component != null && component.GetType() != typeof(RewiredInputManager);

        /// <summary>Legacy components on the currently open scene(s) only (what the Inspector/Dashboard audit can see and fix).</summary>
        internal static List<RewiredInputManager> FindLegacyInOpenScenes() =>
            RewiredHelperAudit.FindAll<RewiredInputManager>().Where(IsLegacyComponent).ToList();

        /// <summary>Swaps every legacy component in the currently open scene(s). Used as the Setup Audit fix action.</summary>
        internal static void MigrateOpenScenes()
        {
            var script = FindInputManagerScript();
            if (script == null)
            {
                Debug.LogError("[RewiredHelper] Could not find the RewiredInputManager script asset; migration aborted.");
                return;
            }

            int migrated = 0;
            foreach (var legacy in FindLegacyInOpenScenes())
            {
                if (SwapScript(legacy, script))
                {
                    migrated++;
                    EditorSceneManager.MarkSceneDirty(legacy.gameObject.scene);
                }
            }

            Debug.Log($"[RewiredHelper] Migrated {migrated} legacy RewiredHelper component(s) in the open scene(s).");
        }

        #endregion

        #region Project-wide scan (menu item)

        private static List<string> ScenesToScan()
        {
            var paths = new HashSet<string>(EditorBuildSettings.scenes.Select(s => s.path));
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && !string.IsNullOrEmpty(scene.path)) paths.Add(scene.path);
            }
            return paths.Where(p => !string.IsNullOrEmpty(p)).ToList();
        }

        private static int CountLegacyInScene(string scenePath)
        {
            var openScene = FindOpenScene(scenePath);
            bool wasAlreadyOpen = openScene.IsValid();
            var scene = wasAlreadyOpen ? openScene : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            int count = CountLegacyInRoots(scene.GetRootGameObjects());

            if (!wasAlreadyOpen) EditorSceneManager.CloseScene(scene, true);
            return count;
        }

        private static int MigrateAllScenes(MonoScript script, List<string> scenePaths)
        {
            int total = 0;
            foreach (var scenePath in scenePaths)
            {
                var openScene = FindOpenScene(scenePath);
                bool wasAlreadyOpen = openScene.IsValid();
                var scene = wasAlreadyOpen ? openScene : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                int changed = 0;
                foreach (var legacy in FindLegacyInRoots(scene.GetRootGameObjects()))
                {
                    if (SwapScript(legacy, script)) changed++;
                }

                if (changed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    total += changed;
                }

                if (!wasAlreadyOpen) EditorSceneManager.CloseScene(scene, true);
            }
            return total;
        }

        private static Scene FindOpenScene(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.path == path) return scene;
            }
            return default;
        }

        private static int CountLegacyInPrefab(string prefabPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return asset == null ? 0 : asset.GetComponentsInChildren<RewiredInputManager>(true).Count(IsLegacyComponent);
        }

        private static int MigrateAllPrefabs(MonoScript script, List<string> prefabPaths)
        {
            int total = 0;
            foreach (var prefabPath in prefabPaths)
            {
                if (CountLegacyInPrefab(prefabPath) == 0) continue;

                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    int changed = FindLegacyInRoots(new[] { contents }).Count(legacy => SwapScript(legacy, script));
                    if (changed > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                        total += changed;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            return total;
        }

        private static int CountLegacyInRoots(GameObject[] roots) => FindLegacyInRoots(roots).Count();

        private static IEnumerable<RewiredInputManager> FindLegacyInRoots(GameObject[] roots) =>
            roots.SelectMany(root => root.GetComponentsInChildren<RewiredInputManager>(true)).Where(IsLegacyComponent);

        #endregion

        #region Script swap

        /// <summary>
        /// Re-points the component's own script reference to <paramref name="targetScript"/>, in place. Field
        /// data with matching names/types carries over automatically (Unity's normal behavior for this), and
        /// since the Component object itself is never destroyed, every other asset's reference to it survives.
        /// </summary>
        private static bool SwapScript(RewiredInputManager legacy, MonoScript targetScript)
        {
            if (legacy == null) return false;

            try
            {
                Undo.RecordObject(legacy, "Migrate RewiredHelper to RewiredInputManager");
                var so = new SerializedObject(legacy);
                so.Update();
                so.FindProperty("m_Script").objectReferenceValue = targetScript;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(legacy);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RewiredHelper] Failed to migrate legacy component on '{legacy.gameObject.name}': {ex.Message}");
                return false;
            }
        }

        private static MonoScript FindInputManagerScript()
        {
            foreach (var guid in AssetDatabase.FindAssets("RewiredInputManager t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == typeof(RewiredInputManager))
                    return script;
            }
            return null;
        }

        #endregion
    }
}
