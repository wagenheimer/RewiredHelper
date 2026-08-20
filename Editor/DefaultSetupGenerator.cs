using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Rewired;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Scene-building helpers under <b>Tools → Wagenheimer → Rewired Helper</b> that create a
    /// standard <see cref="RewiredInputManager"/> setup and a controller-help form wired to it.
    /// These build plain GameObjects directly in the open scene (rather than shipping a
    /// hand-authored .prefab file, which this package's CI can't validate compiles or instantiates
    /// correctly) — save the result as a prefab in your own project once you're happy with it.
    ///
    /// The controller-help form's row list is populated at runtime by
    /// <see cref="Wagenheimer.RewiredHelper.UI.ControllerHelpRowBuilder"/> (attached to its
    /// "Content" container here), not baked at edit time — <c>ReInput.mapping.Actions</c> is only
    /// populated once Rewired is initialized, so edit-time generation would either see stale data
    /// or, before Play mode has ever run, nothing at all.
    /// </summary>
    internal static class DefaultSetupGenerator
    {
        const string InputManagerPrefabPath = "Packages/com.wagenheimer.rewiredhelper/Runtime/Prefabs/Rewired Input Manager.prefab";
        const string EventSystemPrefabPath = "Packages/com.wagenheimer.rewiredhelper/Runtime/Prefabs/Rewired Event System.prefab";
        const string FormControllerPrefabPath = "Packages/com.wagenheimer.rewiredhelper/Runtime/Prefabs/formController.prefab";

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Create Rewired Input Manager", priority = 11)]
        internal static void CreateRewiredInputManager()
        {
            // 1. Instantiate Rewired Input Manager if not present
            var rewiredManager = FindInputManagerInScene();
            if (rewiredManager == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InputManagerPrefabPath);
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    Undo.RegisterCreatedObjectUndo(instance, "Create Rewired Input Manager");
                    rewiredManager = instance.GetComponent("InputManager");
                }
                else
                {
                    Debug.LogWarning($"[RewiredHelper] Prefab not found at {InputManagerPrefabPath}");
                }
            }

            // If still null (e.g. prefab missing), create a fallback empty gameobject
            GameObject managerGo = rewiredManager != null ? rewiredManager.gameObject : null;
            if (managerGo == null)
            {
                managerGo = new GameObject("Rewired Input Manager");
                Undo.RegisterCreatedObjectUndo(managerGo, "Create Rewired Input Manager");
            }

            // 2. Add our RewiredInputManager component if not present
            var helper = managerGo.GetComponent<RewiredInputManager>();
            if (helper == null)
            {
                helper = managerGo.AddComponent<RewiredInputManager>();
                Undo.RegisterCompleteObjectUndo(managerGo, "Add RewiredInputManager Component");
            }

            // 3. Instantiate Rewired Event System (with RewiredStandaloneInputModule) if not present
            EnsureRewiredEventSystem();

            EnsureGlyphProvider(managerGo);

            Selection.activeGameObject = managerGo;
            MarkSceneDirty();
        }

        const string RewiredInputModuleTypeName = "Rewired.Integration.UnityUI.RewiredStandaloneInputModule";

        internal static Type FindRewiredInputModuleType() => FindTypeByName(RewiredInputModuleTypeName);

        /// <summary>
        /// True only if a RewiredStandaloneInputModule is active in the scene — a plain Unity
        /// EventSystem (the default StandaloneInputModule) passes Unity's own null checks but never
        /// routes Rewired's controller input into UI navigation, so checking for "any EventSystem"
        /// is not enough.
        /// </summary>
        internal static bool HasRewiredEventSystemInScene()
        {
            var moduleType = FindRewiredInputModuleType();
            return moduleType != null && UnityEngine.Object.FindObjectOfType(moduleType) != null;
        }

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Create Rewired Event System", priority = 14)]
        internal static void CreateRewiredEventSystem() => EnsureRewiredEventSystem();

        /// <summary>
        /// Ensures the scene has an EventSystem running Rewired's own RewiredStandaloneInputModule.
        /// If a plain EventSystem already exists (e.g. one Unity auto-created), its default
        /// StandaloneInputModule is swapped in place for the Rewired one instead of spawning a
        /// second EventSystem, which Unity doesn't support running side by side.
        /// </summary>
        internal static void EnsureRewiredEventSystem()
        {
            if (HasRewiredEventSystemInScene())
                return;

            var moduleType = FindRewiredInputModuleType();
            var existingEventSystem = UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();

            if (existingEventSystem != null && moduleType != null)
            {
                var vanillaModule = existingEventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                if (vanillaModule != null)
                    Undo.DestroyObjectImmediate(vanillaModule);

                Undo.AddComponent(existingEventSystem.gameObject, moduleType);
                Debug.Log("[RewiredHelper] Replaced the scene's Event System input module with RewiredStandaloneInputModule so controller UI navigation works.");
                MarkSceneDirty();
                return;
            }

            var eventSystemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EventSystemPrefabPath);
            if (eventSystemPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(eventSystemPrefab);
                Undo.RegisterCreatedObjectUndo(instance, "Create Rewired Event System");
                Debug.Log("[RewiredHelper] Created Rewired Event System.");
            }
            else
            {
                var suffix = moduleType == null
                    ? " and RewiredStandaloneInputModule wasn't found in this project either — is Rewired's UnityUI integration installed?"
                    : ".";
                Debug.LogWarning($"[RewiredHelper] Prefab not found at {EventSystemPrefabPath}{suffix}");
            }

            MarkSceneDirty();
        }

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Remove Duplicate Event Systems (All Scenes)", priority = 15)]
        internal static void RemoveDuplicateEventSystemsInAllScenes()
        {
            if (!EditorUtility.DisplayDialog("Remove Duplicate Event Systems",
                    "This opens every scene in the project, removes every EventSystem except the one " +
                    "running Rewired's RewiredStandaloneInputModule, and saves any scene that changed.\n\n" +
                    "Make sure your work is saved/committed first — this can't be undone with Ctrl+Z once a " +
                    "scene is saved.",
                    "Proceed", "Cancel"))
                return;

            var scenePaths = AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(p => p)
                .ToArray();

            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            int scenesChanged = 0;
            int systemsRemoved = 0;

            try
            {
                for (int i = 0; i < scenePaths.Length; i++)
                {
                    var path = scenePaths[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Removing Duplicate Event Systems",
                            path, (float)i / scenePaths.Length))
                        break;

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var removed = RemoveDuplicateEventSystemsInOpenScene();
                    if (removed > 0)
                    {
                        systemsRemoved += removed;
                        scenesChanged++;
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (originalSetup is { Length: > 0 })
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }

            var message = $"Removed {systemsRemoved} duplicate Event System(s) across {scenesChanged} scene(s).";
            Debug.Log($"[RewiredHelper] {message}");
            EditorUtility.DisplayDialog("Remove Duplicate Event Systems", message, "OK");
        }

        /// <summary>
        /// Removes every <see cref="UnityEngine.EventSystems.EventSystem"/> in the currently open scene
        /// except the one running Rewired's input module (or, if none of them do, the first one — which
        /// gets upgraded in place, matching <see cref="EnsureRewiredEventSystem"/>'s own behavior).
        /// Returns how many GameObjects were removed.
        /// </summary>
        private static int RemoveDuplicateEventSystemsInOpenScene()
        {
            var moduleType = FindRewiredInputModuleType();
            var allSystems = UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>(true);
            if (allSystems.Length == 0)
                return 0;

            var keep = moduleType != null
                ? allSystems.FirstOrDefault(es => es.GetComponent(moduleType) != null) ?? allSystems[0]
                : allSystems[0];

            if (moduleType != null && keep.GetComponent(moduleType) == null)
            {
                var vanilla = keep.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                if (vanilla != null) UnityEngine.Object.DestroyImmediate(vanilla);
                keep.gameObject.AddComponent(moduleType);
            }

            int removed = 0;
            foreach (var es in allSystems)
            {
                if (es == keep) continue;
                UnityEngine.Object.DestroyImmediate(es.gameObject);
                removed++;
            }

            return removed;
        }

        const string GlyphProviderTypeName = "Rewired.Glyphs.GlyphProvider";
        const string GlyphSetCollectionTypeName = "Rewired.Glyphs.GlyphSetCollection";

        internal static Type FindGlyphProviderType() => FindTypeByName(GlyphProviderTypeName);
        internal static Type FindGlyphSetCollectionType() => FindTypeByName(GlyphSetCollectionTypeName);

        private static Type FindTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }

        /// <summary>
        /// Adds Rewired's Glyph Provider (if the Glyphs addon is installed) and assigns a
        /// GlyphSetCollection asset found in the project, if any. Without a Glyph Provider,
        /// ReInput.glyphs.glyphProvider is never set and every glyph tag falls back to plain text —
        /// this is the single most common reason "the icons don't show up".
        /// </summary>
        internal static void EnsureGlyphProvider(GameObject managerGo)
        {
            var glyphProviderType = FindGlyphProviderType();
            if (glyphProviderType == null) return; // Glyphs addon not installed — nothing to do

            var existing = managerGo.GetComponent(glyphProviderType);
            bool created = existing == null;
            if (created)
            {
                existing = Undo.AddComponent(managerGo, glyphProviderType);
            }

            var so = new SerializedObject(existing);
            var collectionsProp = so.FindProperty("_glyphSetCollections");
            if (collectionsProp != null && collectionsProp.arraySize == 0)
            {
                var collectionType = FindGlyphSetCollectionType();
                if (collectionType != null)
                {
                    // Prefer the shortest matching asset path: per-controller sub-collections (e.g.
                    // "...(_Joysticks/_Templates/_KeyboardMouse).asset") are aggregated *inside* a
                    // root collection asset, so the root's name is naturally the shortest — assigning
                    // a sub-collection directly alongside the root would double-count its entries and
                    // log "duplicate glyph key" errors.
                    string bestPath = null;
                    foreach (var guid in AssetDatabase.FindAssets($"t:{collectionType.Name}"))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (bestPath == null || path.Length < bestPath.Length)
                            bestPath = path;
                    }

                    if (bestPath != null)
                    {
                        var collectionAsset = AssetDatabase.LoadAssetAtPath(bestPath, collectionType);
                        collectionsProp.arraySize = 1;
                        collectionsProp.GetArrayElementAtIndex(0).objectReferenceValue = collectionAsset;
                        so.ApplyModifiedProperties();
                        Debug.Log($"[RewiredHelper] Assigned Glyph Set Collection '{bestPath}' to Glyph Provider.");
                    }
                    else
                    {
                        Debug.LogWarning("[RewiredHelper] Glyph Provider added, but no GlyphSetCollection asset was found. " +
                            "Install Rewired's Glyphs pack (Window > Rewired > Extras > Glyphs > Install) and assign a collection manually.");
                    }
                }
            }

            if (created)
            {
                Undo.RegisterCompleteObjectUndo(managerGo, "Add Glyph Provider");
                Debug.Log("[RewiredHelper] Added Glyph Provider to Rewired Input Manager so controller glyph icons can render.");
            }
        }

        // Design-time English text for the terms this package's own formController.prefab (Rewired's
        // stock "Controller Support" dialog) and the runtime-generated Controller Help Form use —
        // taken straight from that prefab's baked m_text fallback values. Used as a baseline on top
        // of whatever a live scan of the prefab/scene turns up (see CollectTermGuesses), so the check
        // still means something before formController.prefab is ever instantiated in a scene.
        private static readonly Dictionary<string, string> KnownI2TermTranslations = new Dictionary<string, string>
        {
            { "back", "Back" },
            { "click", "Click" },
            { "cursormovement", "Cursor Movement" },
            { "ok", "OK" },
            { "controllersupport", "Gamepad Support!" },
            { "menu", "Menu" },
            { "GAMEPAD CONTROLS", "Gamepad Controls" },
            { "KEYBOARD_CONTROLS", "Keyboard & Mouse Controls" },
        };

        /// <summary>
        /// True only if every term this package could need — the known baseline plus anything found
        /// by scanning formController.prefab and any I2.Loc.Localize component in the open scene(s)
        /// — exists in an I2 Language Source AND already has a non-empty English translation. A term
        /// that merely exists but has no translation still renders blank/untranslated at runtime, so
        /// existence alone isn't "done".
        /// </summary>
        internal static bool AllI2TermsExist(out int missingCount) =>
            AllI2TermsExist(out missingCount, out _);

        /// <summary>Same as the single-out overload, but also returns the actual missing term names.</summary>
        internal static bool AllI2TermsExist(out int missingCount, out List<string> missingTerms)
        {
            missingTerms = new List<string>();
            var api = ResolveI2Api();
            var termGuesses = CollectTermGuesses(api);
            if (api == null)
            {
                missingTerms.AddRange(termGuesses.Keys);
                missingCount = missingTerms.Count;
                return false;
            }

            var sources = api.GetSources();
            if (sources == null || sources.Count == 0)
            {
                missingTerms.AddRange(termGuesses.Keys);
                missingCount = missingTerms.Count;
                return false;
            }

            foreach (var term in termGuesses.Keys)
            {
                if (!api.TermHasEnglishTranslation(sources, term))
                    missingTerms.Add(term);
            }
            missingCount = missingTerms.Count;
            return missingCount == 0;
        }

        /// <summary>
        /// Adds every missing term (creating it if needed) with an English translation, to the
        /// project's first I2 Language Source. Term text comes from <see cref="KnownI2TermTranslations"/>
        /// for terms this package ships, or is guessed from the sibling Text/TextMeshProUGUI's
        /// design-time text for anything else found by <see cref="CollectTermGuesses"/> — an empty
        /// term with no translation still shows blank at runtime, so adding the key alone isn't enough.
        /// </summary>
        internal static void EnsureI2Terms()
        {
            var api = ResolveI2Api();
            if (api == null) return;

            var sources = api.GetSources();
            if (sources == null || sources.Count == 0)
            {
                Debug.LogWarning("[RewiredHelper] No I2 Localization Language Source found in the project — create one first (I2 Localization > Languages Manager > New Language Source).");
                return;
            }

            var primarySource = sources[0];
            var termGuesses = CollectTermGuesses(api);

            int termsAdded = 0, translationsAdded = 0;
            foreach (var kvp in termGuesses)
            {
                bool wroteTranslation = api.EnsureTermAndEnglishTranslation(primarySource, sources, kvp.Key, kvp.Value, out var termCreated);
                if (termCreated) termsAdded++;
                if (wroteTranslation) translationsAdded++;
            }

            api.SaveSource(primarySource);

            Debug.Log(termsAdded == 0 && translationsAdded == 0
                ? "[RewiredHelper] All required I2 Localization terms already exist with an English translation."
                : $"[RewiredHelper] I2 Localization: added {termsAdded} new term(s) and {translationsAdded} English translation(s) to '{primarySource}'.");
        }

        /// <summary>
        /// Union of the known baseline terms and whatever I2.Loc.Localize components are found on
        /// formController.prefab (whether or not it's in the open scene) and in the open scene(s)
        /// themselves — covers hand-customized copies of the dialog or extra Localize components this
        /// package doesn't know about by name, which a fixed term list would silently miss.
        /// </summary>
        private static Dictionary<string, string> CollectTermGuesses(I2Api api)
        {
            var result = new Dictionary<string, string>(KnownI2TermTranslations);
            if (api?.LocalizeType == null || api.TermField == null)
                return result;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FormControllerPrefabPath);
            if (prefab != null)
            {
                foreach (var comp in prefab.GetComponentsInChildren(api.LocalizeType, true))
                    AddTermGuess(api, comp, result);
            }

            foreach (var obj in UnityEngine.Object.FindObjectsOfType(api.LocalizeType, true))
            {
                if (obj is Component comp)
                    AddTermGuess(api, comp, result);
            }

            return result;
        }

        private static void AddTermGuess(I2Api api, Component localizeComponent, Dictionary<string, string> result)
        {
            var term = api.TermField.GetValue(localizeComponent) as string;
            if (string.IsNullOrEmpty(term) || result.ContainsKey(term))
                return;

            result[term] = api.GuessEnglishTextFor(localizeComponent) ?? NicifyTerm(term);
        }

        private static string NicifyTerm(string term)
        {
            if (string.IsNullOrEmpty(term)) return term;
            return char.ToUpperInvariant(term[0]) + term.Substring(1);
        }

        private static I2Api ResolveI2Api()
        {
            var locManagerType = FindTypeByName("I2.Loc.LocalizationManager");
            var sourceType = FindTypeByName("I2.Loc.LanguageSourceData");
            var termDataType = FindTypeByName("I2.Loc.TermData");
            var localizeType = FindTypeByName("I2.Loc.Localize");
            if (locManagerType == null || sourceType == null || termDataType == null)
                return null;

            var api = new I2Api(locManagerType, sourceType, termDataType, localizeType);
            if (!api.IsValid)
            {
                Debug.LogWarning("[RewiredHelper] Could not resolve I2 Localization's term API — I2 version may differ from the one this was built against.");
                return null;
            }
            return api;
        }

        /// <summary>
        /// Reflection-only wrapper over I2 Localization's term/language API (never <c>using I2.Loc;</c>
        /// — this package must still compile when I2 isn't installed).
        /// </summary>
        private sealed class I2Api
        {
            public readonly Type LocalizeType;
            public readonly FieldInfo TermField;

            private readonly MethodInfo _updateSourcesMethod;
            private readonly FieldInfo _sourcesField;
            private readonly MethodInfo _addTermMethod;
            private readonly MethodInfo _getTermDataMethod;
            private readonly MethodInfo _getLanguageIndexMethod;
            private readonly MethodInfo _addLanguageMethod;
            private readonly MethodInfo _setTranslationMethod;
            private readonly MethodInfo _editorSetDirtyMethod;
            private readonly FieldInfo _languagesField;

            public I2Api(Type locManagerType, Type sourceType, Type termDataType, Type localizeType)
            {
                LocalizeType = localizeType;

                _updateSourcesMethod = locManagerType.GetMethod("UpdateSources", BindingFlags.Public | BindingFlags.Static);
                _sourcesField = locManagerType.GetField("Sources", BindingFlags.Public | BindingFlags.Static);

                _addTermMethod = sourceType.GetMethod("AddTerm", new[] { typeof(string) });
                _getTermDataMethod = sourceType.GetMethod("GetTermData", new[] { typeof(string), typeof(bool) });
                _getLanguageIndexMethod = sourceType.GetMethod("GetLanguageIndex", new[] { typeof(string), typeof(bool), typeof(bool) });
                _addLanguageMethod = sourceType.GetMethod("AddLanguage", new[] { typeof(string) });
                _editorSetDirtyMethod = sourceType.GetMethod("Editor_SetDirty", BindingFlags.Public | BindingFlags.Instance);

                _setTranslationMethod = termDataType.GetMethod("SetTranslation", new[] { typeof(int), typeof(string), typeof(string) });
                _languagesField = termDataType.GetField("Languages", BindingFlags.Public | BindingFlags.Instance);

                TermField = localizeType?.GetField("mTerm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }

            public bool IsValid =>
                _addTermMethod != null && _getTermDataMethod != null &&
                _getLanguageIndexMethod != null && _addLanguageMethod != null &&
                _setTranslationMethod != null && _languagesField != null && _sourcesField != null;

            public System.Collections.IList GetSources()
            {
                _updateSourcesMethod?.Invoke(null, null);
                return _sourcesField.GetValue(null) as System.Collections.IList;
            }

            public bool TermHasEnglishTranslation(System.Collections.IList sources, string term)
            {
                foreach (var source in sources)
                {
                    var termData = _getTermDataMethod.Invoke(source, new object[] { term, false });
                    if (termData == null) continue;

                    int langIdx = (int)_getLanguageIndexMethod.Invoke(source, new object[] { "English", true, true });
                    if (langIdx < 0) continue;

                    var languages = _languagesField.GetValue(termData) as string[];
                    if (languages != null && langIdx < languages.Length && !string.IsNullOrEmpty(languages[langIdx]))
                        return true;
                }
                return false;
            }

            /// <returns>True if an English translation was written (term created and/or translation filled in).</returns>
            public bool EnsureTermAndEnglishTranslation(object primarySource, System.Collections.IList sources, string term, string englishText, out bool termCreated)
            {
                termCreated = false;
                object termData = null;
                object owningSource = null;

                foreach (var source in sources)
                {
                    var data = _getTermDataMethod.Invoke(source, new object[] { term, false });
                    if (data == null) continue;
                    termData = data;
                    owningSource = source;
                    break;
                }

                if (termData == null)
                {
                    termData = _addTermMethod.Invoke(primarySource, new object[] { term });
                    owningSource = primarySource;
                    termCreated = true;
                }

                int langIdx = (int)_getLanguageIndexMethod.Invoke(owningSource, new object[] { "English", true, true });
                if (langIdx < 0)
                {
                    _addLanguageMethod.Invoke(owningSource, new object[] { "English" });
                    langIdx = (int)_getLanguageIndexMethod.Invoke(owningSource, new object[] { "English", true, true });
                    if (langIdx < 0) return termCreated;
                }

                var languages = _languagesField.GetValue(termData) as string[];
                if (languages != null && langIdx < languages.Length && !string.IsNullOrEmpty(languages[langIdx]))
                    return termCreated; // already translated — nothing more to write

                _setTranslationMethod.Invoke(termData, new object[] { langIdx, englishText, null });
                return true;
            }

            public void SaveSource(object source)
            {
                _editorSetDirtyMethod?.Invoke(source, null);
                AssetDatabase.SaveAssets();
            }

            public string GuessEnglishTextFor(Component localizeComponent)
            {
                var tmp = localizeComponent.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmp != null && !string.IsNullOrEmpty(tmp.text)) return tmp.text;

                var uiText = localizeComponent.GetComponent<UnityEngine.UI.Text>();
                if (uiText != null && !string.IsNullOrEmpty(uiText.text)) return uiText.text;

                return null;
            }
        }

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Create Controller Help Form", priority = 12)]
        internal static void CreateControllerHelpForm()
        {
            var canvas = FindOrCreateCanvas();
            var formGo = GenerateRowBasedHelpForm(canvas.transform);

            Undo.RegisterCreatedObjectUndo(formGo, "Create Controller Help Form");
            formGo.SetActive(false);

            Selection.activeGameObject = formGo;
            MarkSceneDirty();

            Debug.Log("[RewiredHelper] Created a custom Row-Based Controller Help form (inactive by default). " +
                "Wire RewiredInputManager.OnShowControllerHelp to SetActive(true) it.");
        }

        internal static void CreateControllerHelpFormAndWire(RewiredInputManager manager)
        {
            var canvas = FindOrCreateCanvas();
            var formGo = GenerateRowBasedHelpForm(canvas.transform);

            Undo.RegisterCreatedObjectUndo(formGo, "Create Controller Help Form");
            formGo.SetActive(false);

            if (manager != null)
            {
                // Register persistent listener to OnShowControllerHelp
                var methodInfo = typeof(GameObject).GetMethod("SetActive", new[] { typeof(bool) });
                if (methodInfo != null)
                {
                    var delegateAction = Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<bool>), formGo, methodInfo) as UnityEngine.Events.UnityAction<bool>;
                    UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(manager.OnShowControllerHelp, delegateAction, true);
                }
            }

            Selection.activeGameObject = formGo;
            MarkSceneDirty();

            Debug.Log("[RewiredHelper] Created custom Row-Based Controller Help form and wired to OnShowControllerHelp event.");
        }

        internal static void CreatePauseScreenAndWire(RewiredInputManager manager, SerializedObject serializedObject)
        {
            var canvas = FindOrCreateCanvas();
            var pauseGo = CreatePauseScreen(canvas.transform);

            Undo.RegisterCreatedObjectUndo(pauseGo, "Create Pause Screen");

            if (serializedObject != null)
            {
                var prop = serializedObject.FindProperty("GamePaused");
                if (prop != null)
                {
                    prop.objectReferenceValue = pauseGo;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            Selection.activeGameObject = pauseGo;
            MarkSceneDirty();

            Debug.Log("[RewiredHelper] Created Pause Screen and linked to GamePaused field.");
        }

        internal static void CreateGameCursorAndWire(RewiredInputManager manager, SerializedObject serializedObject)
        {
            var canvas = FindOrCreateCanvas();
            
            // Create a GameCursor GameObject under the Canvas
            var cursorGo = new GameObject("GameCursor", typeof(RectTransform), typeof(Image));
            var cursorRect = (RectTransform)cursorGo.transform;
            cursorRect.SetParent(canvas.transform, false);
            cursorRect.anchorMin = new Vector2(0f, 1f); // top-left corner anchor
            cursorRect.anchorMax = new Vector2(0f, 1f);
            cursorRect.pivot = new Vector2(0f, 1f);      // top-left pivot
            cursorRect.sizeDelta = new Vector2(32, 32);
            cursorRect.anchoredPosition = Vector2.zero;

            var img = cursorGo.GetComponent<Image>();
            img.raycastTarget = false; // MUST be false so it doesn't block UI clicks!
            img.color = Color.white;

            var cursorCanvas = cursorGo.AddComponent<Canvas>();
            cursorCanvas.overrideSorting = true;
            cursorCanvas.sortingOrder = 1000;

            cursorGo.AddComponent<Wagenheimer.RewiredHelper.UI.GameCursorPositioner>();

            Undo.RegisterCreatedObjectUndo(cursorGo, "Create Game Cursor");

            if (serializedObject != null)
            {
                var prop = serializedObject.FindProperty("GameCursor");
                if (prop != null)
                {
                    prop.objectReferenceValue = img;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            Selection.activeGameObject = cursorGo;
            MarkSceneDirty();

            Debug.Log("[RewiredHelper] Created custom Game Cursor UI Image (raycastTarget=false) and linked to GameCursor field.");
        }

        const string PlayerMouseTypeName = "Rewired.Components.PlayerMouse";

        internal static Type FindPlayerMouseType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(PlayerMouseTypeName);
                if (type != null) return type;
            }
            return null;
        }

        internal static Component FindPlayerMouseInScene(Type playerMouseType)
        {
            return playerMouseType != null ? UnityEngine.Object.FindObjectOfType(playerMouseType) as Component : null;
        }

        /// <summary>
        /// Counts axis elements actually bound to a Rewired action inside PlayerMouse's Elements
        /// list (private serialized field "_elements", verified against Rewired's own shipped
        /// PlayerMouseUnityUI example). Returns -1 if the field couldn't be found (different
        /// Rewired version) so callers don't report a false failure.
        /// </summary>
        internal static int CountConfiguredMouseElements(SerializedObject playerMouseSerialized)
        {
            var groups = playerMouseSerialized.FindProperty("_elements");
            if (groups == null) return -1;

            int count = 0;
            for (int i = 0; i < groups.arraySize; i++)
            {
                var nested = groups.GetArrayElementAtIndex(i).FindPropertyRelative("_elements");
                if (nested == null) continue;

                for (int j = 0; j < nested.arraySize; j++)
                {
                    var actionIdProp = nested.GetArrayElementAtIndex(j).FindPropertyRelative("_actionId");
                    if (actionIdProp != null && actionIdProp.intValue >= 0)
                        count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Adds a "Movement" element group bound to the MouseX/MouseY actions (the same action
        /// names RewiredInputManager already reads via Player.GetAxis) — same shape as Rewired's
        /// own PlayerMouseUnityUI example. Only appends; never touches existing groups.
        /// </summary>
        internal static void ConfigureMouseMovementElements(Component playerMouse)
        {
            var so = new SerializedObject(playerMouse);
            var groups = so.FindProperty("_elements");
            if (groups == null)
            {
                Debug.LogWarning("[RewiredHelper] Could not find Player Mouse's Elements field — Rewired version may differ from the one this was built against.");
                return;
            }

            int horizontalId = ResolveActionId("MouseX");
            int verticalId = ResolveActionId("MouseY");

            int groupIndex = groups.arraySize;
            groups.arraySize++;
            var group = groups.GetArrayElementAtIndex(groupIndex);
            group.FindPropertyRelative("_name").stringValue = "Movement";
            group.FindPropertyRelative("_elementType").intValue = 101;
            group.FindPropertyRelative("_enabled").boolValue = true;

            var nested = group.FindPropertyRelative("_elements");
            nested.arraySize = 2;
            SetAxisElement(nested.GetArrayElementAtIndex(0), "Horizontal", horizontalId);
            SetAxisElement(nested.GetArrayElementAtIndex(1), "Vertical", verticalId);

            so.ApplyModifiedProperties();
            Undo.RegisterCompleteObjectUndo(playerMouse, "Configure Player Mouse Movement");
            MarkSceneDirty();

            Debug.Log("[RewiredHelper] Added a 'Movement' element group to Player Mouse, bound to the MouseX/MouseY actions.");
        }

        private static void SetAxisElement(SerializedProperty element, string name, int actionId)
        {
            element.FindPropertyRelative("_name").stringValue = name;
            element.FindPropertyRelative("_elementType").intValue = 2; // Axis
            element.FindPropertyRelative("_enabled").boolValue = true;
            element.FindPropertyRelative("_actionId").intValue = actionId;
            element.FindPropertyRelative("_coordinateMode").intValue = 1; // Relative
            element.FindPropertyRelative("_absoluteToRelativeSensitivity").floatValue = 600f;
            element.FindPropertyRelative("_repeatRate").floatValue = 4f;
        }

        /// <summary>
        /// Resolves a Rewired action's id by name directly from the scene's InputManager component
        /// data (serialized under "_userData.actions", same shape as in the shipped prefab) instead
        /// of <c>ReInput.mapping</c> — Rewired is only initialized at runtime (normally on
        /// InputManager.Awake), so <c>ReInput.mapping</c> throws "Rewired is not initialized" and
        /// returns null for every lookup made from editor code, silently leaving elements unbound.
        /// </summary>
        private static int ResolveActionId(string actionName)
        {
            var inputManagerComponent = FindInputManagerInScene();
            if (inputManagerComponent == null)
            {
                Debug.LogWarning($"[RewiredHelper] Could not resolve action '{actionName}': no Rewired Input Manager found in the scene.");
                return -1;
            }

            var so = new SerializedObject(inputManagerComponent);
            var actions = so.FindProperty("_userData.actions");
            if (actions == null)
            {
                Debug.LogWarning($"[RewiredHelper] Could not resolve action '{actionName}': Input Manager's action list field wasn't found — Rewired version may differ from the one this was built against.");
                return -1;
            }

            for (int i = 0; i < actions.arraySize; i++)
            {
                var action = actions.GetArrayElementAtIndex(i);
                var nameProp = action.FindPropertyRelative("_name");
                if (nameProp != null && nameProp.stringValue == actionName)
                {
                    var idProp = action.FindPropertyRelative("_id");
                    return idProp != null ? idProp.intValue : -1;
                }
            }

            Debug.LogWarning($"[RewiredHelper] Could not resolve action '{actionName}': no such action exists in the Input Manager. Create it in the Rewired Input Manager editor first.");
            return -1;
        }

        internal static void CreatePlayerMouseAndWire(RewiredInputManager manager)
        {
            var type = FindPlayerMouseType();
            if (type == null)
            {
                Debug.LogWarning("[RewiredHelper] Rewired.Components.PlayerMouse type not found — check your Rewired installation.");
                return;
            }

            var go = new GameObject("PlayerMouse_Player0");
            var comp = go.AddComponent(type);
            Undo.RegisterCreatedObjectUndo(go, "Create Player Mouse");

            var so = new SerializedObject(comp);
            SetIfPresent(so, "_rewiredInputManager", FindInputManagerInScene());
            SetIfPresent(so, "_playerId", 0);
            SetIfPresent(so, "_pointerSpeed", 1f);
            SetIfPresent(so, "_useHardwarePointerPosition", true);
            SetIfPresent(so, "_clampToMovementArea", true);
            SetIfPresent(so, "_defaultToCenter", true);
            so.ApplyModifiedProperties();

            ConfigureMouseMovementElements(comp);

            Selection.activeGameObject = go;
            MarkSceneDirty();
            Debug.Log("[RewiredHelper] Created Player Mouse (Rewired.Components.PlayerMouse) with Movement elements bound to MouseX/MouseY.");
        }

        private static void SetIfPresent(SerializedObject so, string propertyName, object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null) return;

            switch (value)
            {
                case bool b: prop.boolValue = b; break;
                case int i: prop.intValue = i; break;
                case float f: prop.floatValue = f; break;
                case Component c: prop.objectReferenceValue = c; break;
            }
        }

        private static GameObject CreatePauseScreen(Transform parent)
        {
            var pauseGo = new GameObject("PauseScreen", typeof(RectTransform), typeof(Image));
            var pauseRect = (RectTransform)pauseGo.transform;
            pauseRect.SetParent(parent, false);
            pauseRect.anchorMin = Vector2.zero;
            pauseRect.anchorMax = Vector2.one;
            pauseRect.sizeDelta = Vector2.zero;
            pauseRect.anchoredPosition = Vector2.zero;

            var bg = pauseGo.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);

            var titleGo = new GameObject("PauseTitle", typeof(RectTransform));
            var titleRect = (RectTransform)titleGo.transform;
            titleRect.SetParent(pauseRect, false);
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(400, 100);
            titleRect.anchoredPosition = new Vector2(0, 50);

            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "GAME PAUSED";
            titleText.fontSize = 40;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;

            var subGo = new GameObject("PauseSubtitle", typeof(RectTransform));
            var subRect = (RectTransform)subGo.transform;
            subRect.SetParent(pauseRect, false);
            subRect.anchorMin = new Vector2(0.5f, 0.5f);
            subRect.anchorMax = new Vector2(0.5f, 0.5f);
            subRect.sizeDelta = new Vector2(400, 50);
            subRect.anchoredPosition = new Vector2(0, -20);

            var subText = subGo.AddComponent<TextMeshProUGUI>();
            subText.text = "Press ESC or Menu button to resume";
            subText.fontSize = 18;
            subText.color = new Color(0.7f, 0.7f, 0.75f);
            subText.alignment = TextAlignmentOptions.Center;

            pauseGo.SetActive(false);
            return pauseGo;
        }

        static GameObject GenerateRowBasedHelpForm(Transform parent)
        {
            // 1. Create Root Backdrop Panel (Stretches full screen)
            var formGo = new GameObject("ControllerHelpForm", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var formRect = (RectTransform)formGo.transform;
            formRect.SetParent(parent, false);
            formRect.anchorMin = Vector2.zero;
            formRect.anchorMax = Vector2.one;
            formRect.sizeDelta = Vector2.zero;
            formRect.anchoredPosition = Vector2.zero;

            var bgOverlay = formGo.GetComponent<Image>();
            bgOverlay.color = new Color(0f, 0f, 0f, 0.6f); // Semi-transparent black backdrop overlay

            // Add Dialog component from the package
            var modalDialog = formGo.AddComponent<Wagenheimer.RewiredHelper.UI.Dialog>();
            modalDialog.Black = bgOverlay;
            modalDialog.BlackAlpha = 0.6f;
            modalDialog.ShowEffect = Wagenheimer.RewiredHelper.UI.ShowDialogEffect.Fade;
            modalDialog.ShowHideDialogTime = 0.15f;

            // 1b. Create Main Dialog Window Panel (600x450)
            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            var cardRect = (RectTransform)cardGo.transform;
            cardRect.SetParent(formRect, false);
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(600, 450);
            cardRect.anchoredPosition = Vector2.zero;

            var bgCard = cardGo.GetComponent<Image>();
            bgCard.color = new Color(0.08f, 0.08f, 0.10f, 0.98f); // Sleek dark blue-black background

            // Top Color Highlight Bar (Accent)
            var topBarGo = new GameObject("TopAccentBar", typeof(RectTransform), typeof(Image));
            var topBarRect = (RectTransform)topBarGo.transform;
            topBarRect.SetParent(cardRect, false);
            topBarRect.anchorMin = new Vector2(0, 1);
            topBarRect.anchorMax = new Vector2(1, 1);
            topBarRect.pivot = new Vector2(0.5f, 1);
            topBarRect.sizeDelta = new Vector2(0, 5);
            topBarRect.anchoredPosition = Vector2.zero;
            topBarGo.GetComponent<Image>().color = new Color(0.22f, 0.60f, 1.00f); // Accent Blue

            // 2. Create Header Title
            var headerGo = new GameObject("HeaderTitle", typeof(RectTransform));
            var headerRect = (RectTransform)headerGo.transform;
            headerRect.SetParent(cardRect, false);
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 45);
            headerRect.anchoredPosition = new Vector2(0, -15);

            var headerText = headerGo.AddComponent<TextMeshProUGUI>();
            headerText.text = "GAMEPAD CONTROLS";
            headerText.fontSize = 20;
            headerText.color = Color.white;
            headerText.fontStyle = FontStyles.Bold;
            headerText.alignment = TextAlignmentOptions.Center;

            // Separator Underline
            var sepGo = new GameObject("HeaderSeparator", typeof(RectTransform), typeof(Image));
            var sepRect = (RectTransform)sepGo.transform;
            sepRect.SetParent(cardRect, false);
            sepRect.anchorMin = new Vector2(0, 1);
            sepRect.anchorMax = new Vector2(1, 1);
            sepRect.pivot = new Vector2(0.5f, 1);
            sepRect.sizeDelta = new Vector2(-40, 1);
            sepRect.anchoredPosition = new Vector2(0, -60);
            sepGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.22f, 1f);

            // 3. Create Scroll View
            var scrollViewGo = new GameObject("Scroll View", typeof(RectTransform), typeof(ScrollRect));
            var scrollRect = scrollViewGo.GetComponent<ScrollRect>();
            var scrollRectTransform = (RectTransform)scrollViewGo.transform;
            scrollRectTransform.SetParent(cardRect, false);
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(20, 50); // Leave room for footer
            scrollRectTransform.offsetMax = new Vector2(-20, -75); // Offset top for header

            // Viewport — RectMask2D instead of Mask+Image: Mask clips via a stencil buffer that a
            // fully transparent Image writes to, and depending on Unity version/render pipeline that
            // write can get skipped or discarded at the shader level even with cullTransparentMesh
            // disabled, silently clipping every child out instead of nothing. RectMask2D clips
            // purely via the shader's _ClipRect, with no Graphic/stencil involved, so it can't hit
            // that failure mode.
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewportRect = (RectTransform)viewportGo.transform;
            viewportRect.SetParent(scrollRectTransform, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;

            // Content Container
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            var contentRect = (RectTransform)contentGo.transform;
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);

            var glg = contentGo.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(265, 48);
            glg.spacing = new Vector2(10, 8);
            glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
            glg.startAxis = GridLayoutGroup.Axis.Horizontal;
            glg.childAlignment = TextAnchor.UpperCenter;
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Footer Prompt
            var footerGo = new GameObject("FooterPrompt", typeof(RectTransform));
            var footerRect = (RectTransform)footerGo.transform;
            footerRect.SetParent(cardRect, false);
            footerRect.anchorMin = new Vector2(0, 0);
            footerRect.anchorMax = new Vector2(1, 0);
            footerRect.pivot = new Vector2(0.5f, 0);
            footerRect.sizeDelta = new Vector2(0, 30);
            footerRect.anchoredPosition = new Vector2(0, 12);

            var footerText = footerGo.AddComponent<TextMeshProUGUI>();
            footerText.text = "PRESS ANY BUTTON TO RESUME";
            footerText.fontSize = 11;
            footerText.color = new Color(0.45f, 0.45f, 0.50f);
            footerText.fontStyle = FontStyles.Bold;
            footerText.alignment = TextAlignmentOptions.Center;

            // 4. Rows are built at runtime (see Wagenheimer.RewiredHelper.UI.ControllerHelpRowBuilder)
            // instead of baked here, because ReInput.mapping.Actions is only populated once Rewired
            // is initialized (normally Play mode) — generating rows at edit time means either stale
            // data or, if the mapping was empty, placeholder rows with fake action names and no
            // glyphs. The runtime component clears and rebuilds from the live action map on Awake
            // every time the game starts, so it always matches the current Rewired configuration.
            contentGo.AddComponent<Wagenheimer.RewiredHelper.UI.ControllerHelpRowBuilder>();

            return formGo;
        }

        static Canvas FindOrCreateCanvas()
        {
            var existing = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (existing != null)
                return existing;

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            return canvas;
        }

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Verify && Fix Game Cursor Wiring", priority = 13)]
        internal static void VerifyAndFixGameCursorWiring()
        {
            var manager = FindInputManagerInScene()?.GetComponent<RewiredInputManager>();
            if (manager == null)
            {
                Debug.LogWarning("[RewiredHelper] No RewiredInputManager found in the scene — nothing to fix.");
                return;
            }

            if (manager.GameCursor == null)
            {
                Debug.LogWarning("[RewiredHelper] RewiredInputManager.GameCursor isn't assigned — assign it first (or run Create Rewired Input Manager's cursor step).");
                return;
            }

            var playerMouseType = FindPlayerMouseType();
            var playerMouse = FindPlayerMouseInScene(playerMouseType);
            if (playerMouse == null)
            {
                Debug.LogWarning("[RewiredHelper] No Rewired.Components.PlayerMouse found in the scene — nothing to re-wire.");
                return;
            }

            WirePlayerMouseEvents(manager, playerMouse);
            Debug.Log("[RewiredHelper] Verified Game Cursor wiring — GameCursorPositioner is present and PlayerMouse's screen-position event points at it.");
        }

        internal static void WirePlayerMouseEvents(RewiredInputManager manager, Component playerMouse)
        {
            if (manager == null || playerMouse == null || manager.GameCursor == null)
            {
                Debug.LogWarning("[RewiredHelper] Cannot wire Player Mouse events: make sure Game Cursor is assigned first.");
                return;
            }

            var so = new SerializedObject(playerMouse);

            // On Screen Position Changed — wire to GameCursorPositioner.SetScreenPosition(Vector2).
            // NOT bound to RectTransform.anchoredPosition directly: PlayerMouse reports raw screen
            // pixels, which only line up with anchoredPosition when the Canvas is Screen Space -
            // Overlay AND its Canvas Scaler is 1:1 with the real resolution. Any other setup
            // (Screen Space - Camera, World Space, or a scaled reference resolution) needs the
            // screen point converted through the Canvas first, which the positioner does.
            var positioner = manager.GameCursor.GetComponent<Wagenheimer.RewiredHelper.UI.GameCursorPositioner>();
            if (positioner == null)
            {
                positioner = manager.GameCursor.gameObject.AddComponent<Wagenheimer.RewiredHelper.UI.GameCursorPositioner>();
                Undo.RegisterCreatedObjectUndo(positioner, "Add Game Cursor Positioner");
                Debug.Log("[RewiredHelper] Added missing GameCursorPositioner to Game Cursor.");
            }

            var screenPosCalls = so.FindProperty("_onScreenPositionChanged.m_PersistentCalls.m_Calls");
            if (screenPosCalls != null)
            {
                const string methodName = "SetScreenPosition";
                PruneNullSerializedListeners(screenPosCalls);
                RemoveSerializedListenersToOtherTargets(screenPosCalls, positioner);
                if (!HasSerializedListener(screenPosCalls, positioner, methodName))
                {
                    AppendSerializedListener(
                        screenPosCalls,
                        target: positioner,
                        assemblyTypeName: $"{typeof(Wagenheimer.RewiredHelper.UI.GameCursorPositioner).FullName}, {typeof(Wagenheimer.RewiredHelper.UI.GameCursorPositioner).Assembly.GetName().Name}",
                        methodName: methodName,
                        mode: 0 // EventDefined (dynamic — matches the Vector2 the event passes)
                    );
                }
            }

            // On Enabled State Changed — wire to GameObject.SetActive(bool)
            var enabledCalls = so.FindProperty("_onEnabledStateChanged.m_PersistentCalls.m_Calls");
            if (enabledCalls != null)
            {
                PruneNullSerializedListeners(enabledCalls);
                if (!HasSerializedListener(enabledCalls, manager.GameCursor.gameObject, "SetActive"))
                {
                    AppendSerializedListener(
                        enabledCalls,
                        target: manager.GameCursor.gameObject,
                        assemblyTypeName: $"{typeof(GameObject).FullName}, UnityEngine",
                        methodName: "SetActive",
                        mode: 0 // EventDefined (dynamic — matches the bool the event passes)
                    );
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(playerMouse);
            MarkSceneDirty();
            Debug.Log("[RewiredHelper] Player Mouse events auto-wired to Game Cursor (anchoredPosition & SetActive).");
        }

        private static void PruneNullListeners(UnityEngine.Events.UnityEventBase ev)
        {
            for (int i = ev.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                if (ev.GetPersistentTarget(i) == null)
                    UnityEditor.Events.UnityEventTools.RemovePersistentListener(ev, i);
            }
        }

        private static bool HasListener(UnityEngine.Events.UnityEventBase ev, UnityEngine.Object target, string methodName)
        {
            for (int i = 0; i < ev.GetPersistentEventCount(); i++)
            {
                if (ev.GetPersistentTarget(i) == target && ev.GetPersistentMethodName(i) == methodName)
                    return true;
            }
            return false;
        }

        private static void PruneNullSerializedListeners(SerializedProperty callsArray)
        {
            for (int i = callsArray.arraySize - 1; i >= 0; i--)
            {
                var target = callsArray.GetArrayElementAtIndex(i).FindPropertyRelative("m_Target").objectReferenceValue;
                if (target == null)
                    callsArray.DeleteArrayElementAtIndex(i);
            }
        }

        /// <summary>
        /// Strips any persistent listener that doesn't target <paramref name="keepTarget"/> — used
        /// to clear out a stale direct-to-anchoredPosition binding left over from an older wiring
        /// (or an earlier RewiredHelper version) before re-adding the correct one.
        /// </summary>
        private static void RemoveSerializedListenersToOtherTargets(SerializedProperty callsArray, UnityEngine.Object keepTarget)
        {
            for (int i = callsArray.arraySize - 1; i >= 0; i--)
            {
                var target = callsArray.GetArrayElementAtIndex(i).FindPropertyRelative("m_Target").objectReferenceValue;
                if (target != keepTarget)
                    callsArray.DeleteArrayElementAtIndex(i);
            }
        }

        private static bool HasSerializedListener(SerializedProperty callsArray, UnityEngine.Object target, string methodName)
        {
            for (int i = 0; i < callsArray.arraySize; i++)
            {
                var el = callsArray.GetArrayElementAtIndex(i);
                var t = el.FindPropertyRelative("m_Target").objectReferenceValue;
                var m = el.FindPropertyRelative("m_MethodName").stringValue;
                if (t == target && m == methodName)
                    return true;
            }
            return false;
        }

        private static void AppendSerializedListener(SerializedProperty callsArray, UnityEngine.Object target, string assemblyTypeName, string methodName, int mode)
        {
            callsArray.arraySize++;
            var call = callsArray.GetArrayElementAtIndex(callsArray.arraySize - 1);

            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = assemblyTypeName;
            call.FindPropertyRelative("m_MethodName").stringValue = methodName;
            call.FindPropertyRelative("m_Mode").intValue = mode;
            call.FindPropertyRelative("m_CallState").intValue = 2; // EditorAndRuntime

            // Zero-out argument fields (avoid leftover junk from array expansion)
            var args = call.FindPropertyRelative("m_Arguments");
            if (args != null)
            {
                args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
                args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = "UnityEngine.Object, UnityEngine";
                args.FindPropertyRelative("m_IntArgument").intValue = 0;
                args.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
                args.FindPropertyRelative("m_StringArgument").stringValue = string.Empty;
                args.FindPropertyRelative("m_BoolArgument").boolValue = false;
            }
        }

        private static void AddPersistentListenerReflected<T>(UnityEngine.Events.UnityEventBase ev, UnityEngine.Events.UnityAction<T> action)
        {
            // UnityEventTools.AddPersistentListener(UnityEvent<T>, UnityAction<T>) is only accessible
            // via the concrete generic type. We call it through reflection to decouple from Rewired's
            // internal event subclass at compile time.
            var addMethod = typeof(UnityEditor.Events.UnityEventTools)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "AddPersistentListener" || !m.IsGenericMethodDefinition) return false;
                    var p = m.GetParameters();
                    return p.Length == 2 && p[0].ParameterType.IsGenericType && p[1].ParameterType.IsGenericType;
                });

            if (addMethod == null)
            {
                Debug.LogWarning("[RewiredHelper] Could not find UnityEventTools.AddPersistentListener<T> — check Unity version compatibility.");
                return;
            }

            var genericMethod = addMethod.MakeGenericMethod(typeof(T));
            genericMethod.Invoke(null, new object[] { ev, action });
        }

        static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        internal static Component FindInputManagerInScene()
        {
            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                var comp = go.GetComponent("InputManager");
                if (comp != null && comp.GetType().FullName == "Rewired.InputManager")
                    return comp;
            }
            return null;
        }
    }
}
