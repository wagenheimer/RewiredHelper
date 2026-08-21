using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Automates glyph support for the AndroidRemote Custom Controller so users of the package
    /// don't have to configure anything by hand. Performs, in order:
    ///
    /// 1. Ensures every Element Identifier of the "AndroidController" Custom Controller has a
    ///    unique key set (required by the Rewired glyph system — without keys, glyph lookup
    ///    always fails and UI falls back to raw text like "Left", "Escape").
    /// 2. Creates (or updates) a SpriteGlyphSet asset for the controller using Rewired's own
    ///    "Generate from Custom Controller" logic, invoked via reflection because the Rewired
    ///    glyph scripts live in Assembly-CSharp, which this assembly cannot reference.
    /// 3. Copies sprites onto the generated entries from any other SpriteGlyphSets already
    ///    installed in the project (e.g. Rewired's keyboard/xbox packs) by matching entry keys,
    ///    so no manual art assignment is needed for common elements.
    /// 4. Adds the generated set to the project's GlyphSetCollection and reloads the provider.
    /// </summary>
    internal static class AndroidRemoteGlyphSetup
    {
        const string ControllerName = "AndroidController";
        const string DefaultControllerKey = "android_controller";

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Ensure Android Remote Glyphs", priority = 16)]
        internal static void EnsureAndroidRemoteGlyphsMenu()
        {
            int steps = EnsureAndroidRemoteGlyphs();
            Debug.Log($"[RewiredHelper] Android Remote glyphs: {steps} step(s) applied.");
        }

        /// <summary>Returns how many corrective steps were applied.</summary>
        internal static int EnsureAndroidRemoteGlyphs()
        {
            int applied = 0;

            var inputManager = DefaultSetupGenerator.FindInputManagerInScene();
            if (inputManager == null)
            {
                Debug.LogWarning("[RewiredHelper] No Rewired Input Manager found in the scene — run 'Create Rewired Input Manager' first.");
                return 0;
            }

            // ---- Step 1: ensure element identifier keys exist ----
            if (EnsureElementIdentifierKeys(inputManager)) applied++;

            // ---- Step 2: generate / update the SpriteGlyphSet asset ----
            var setResult = EnsureSpriteGlyphSet(inputManager);
            if (setResult.createdOrUpdateApplied) applied++;

            if (setResult.setAsset == null)
                return applied;

            // ---- Step 3: copy sprites from existing glyph sets ----
            if (CopySpritesFromExistingSets(setResult.setAsset)) applied++;

            // ---- Step 4: add to collection and reload ----
            if (AddToCollectionAndReload(setResult.setAsset, inputManager)) applied++;

            return applied;
        }

        // ------------------------------------------------------------------

        static bool EnsureElementIdentifierKeys(Component inputManager)
        {
            var so = new SerializedObject(inputManager);
            var controllers = so.FindProperty("_userData.customControllers");
            if (controllers == null)
            {
                Debug.LogWarning("[RewiredHelper] Could not find '_userData.customControllers' on the Input Manager — Rewired version may differ.");
                return false;
            }

            bool changed = false;
            for (int i = 0; i < controllers.arraySize; i++)
            {
                var cc = controllers.GetArrayElementAtIndex(i);
                if (cc.FindPropertyRelative("_name")?.stringValue != ControllerName)
                    continue;

                var ccKey = cc.FindPropertyRelative("_key");
                if (ccKey != null && string.IsNullOrEmpty(ccKey.stringValue))
                {
                    ccKey.stringValue = DefaultControllerKey;
                    changed = true;
                }

                var elements = cc.FindPropertyRelative("_elementIdentifiers");
                if (elements == null) break;

                for (int j = 0; j < elements.arraySize; j++)
                {
                    var el = elements.GetArrayElementAtIndex(j);
                    var nameProp = el.FindPropertyRelative("_name");
                    var keyProp = el.FindPropertyRelative("_key");
                    if (nameProp == null || keyProp == null) continue;
                    if (!string.IsNullOrEmpty(keyProp.stringValue)) continue;

                    keyProp.stringValue = ToKey(nameProp.stringValue);
                    changed = true;
                }
                break;
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(inputManager);
                Debug.Log("[RewiredHelper] Filled in missing keys on the AndroidController element identifiers.");
            }
            return changed;
        }

        static string ToKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                else if (c == ' ' || c == '-' || c == '_') sb.Append('_');
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------

        struct SetResult
        {
            public UnityEngine.Object setAsset;
            public bool createdOrUpdateApplied;
        }

        static SetResult EnsureSpriteGlyphSet(Component inputManager)
        {
            var result = new SetResult();

            var spriteGlyphSetType = FindType("Rewired.Glyphs.SpriteGlyphSet");
            if (spriteGlyphSetType == null)
            {
                Debug.LogWarning("[RewiredHelper] Rewired.Glyphs.SpriteGlyphType not found — is the Rewired Glyphs addon installed?");
                return result;
            }

            // Resolve the controller key first (needed for baseKeys)
            string controllerKey = GetControllerKey(inputManager);
            if (string.IsNullOrEmpty(controllerKey))
            {
                Debug.LogWarning("[RewiredHelper] AndroidController has no key and none could be assigned automatically.");
                return result;
            }

            string expectedBaseKey = "controller/custom/" + controllerKey;

            // Reuse an existing set with the same baseKey if present
            var existing = FindSetWithBaseKey(spriteGlyphSetType, expectedBaseKey);
            if (existing == null)
            {
                const string dir = "Assets/RewiredHelper/Generated";
                if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "RewiredHelper");
                if (!AssetDatabase.IsValidFolder(dir)) return result;

                existing = ScriptableObject.CreateInstance(spriteGlyphSetType);
                AssetDatabase.CreateAsset(existing, dir + "/AndroidRemoteGlyphSet.asset");
                result.createdOrUpdateApplied = true;
            }

            // Invoke Rewired's own generator to (re)build entries from the element identifiers.
            // Signature: void GenerateFromCustomController(UserData userData, int customControllerId)
            var userDataField = inputManager.GetType().GetField("_userData", BindingFlags.NonPublic | BindingFlags.Instance);
            var userData = userDataField?.GetValue(inputManager);
            var controllerId = GetCustomControllerId(inputManager);

            if (userData != null && controllerId >= 0)
            {
                var editorType = FindType("Rewired.Glyphs.Editor.SpriteGlyphSetInspector");
                var genMethod = editorType?.GetMethod("GenerateFromCustomController",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (genMethod != null)
                {
                    var editor = UnityEditor.Editor.CreateEditor(existing);
                    try
                    {
                        genMethod.Invoke(editor, new[] { userData, (object)controllerId });
                        EditorUtility.SetDirty(existing);
                        result.createdOrUpdateApplied = true;
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(editor);
                    }
                }
            }

            result.setAsset = existing;
            return result;
        }

        static string GetControllerKey(Component inputManager)
        {
            var so = new SerializedObject(inputManager);
            var controllers = so.FindProperty("_userData.customControllers");
            if (controllers == null) return null;

            for (int i = 0; i < controllers.arraySize; i++)
            {
                var cc = controllers.GetArrayElementAtIndex(i);
                if (cc.FindPropertyRelative("_name")?.stringValue != ControllerName) continue;
                var key = cc.FindPropertyRelative("_key")?.stringValue;
                return string.IsNullOrEmpty(key) ? null : key;
            }
            return null;
        }

        static int GetCustomControllerId(Component inputManager)
        {
            var so = new SerializedObject(inputManager);
            var controllers = so.FindProperty("_userData.customControllers");
            if (controllers == null) return -1;

            for (int i = 0; i < controllers.arraySize; i++)
            {
                var cc = controllers.GetArrayElementAtIndex(i);
                if (cc.FindPropertyRelative("_name")?.stringValue != ControllerName) continue;
                var id = cc.FindPropertyRelative("_id");
                return id != null ? id.intValue : -1;
            }
            return -1;
        }

        static UnityEngine.Object FindSetWithBaseKey(Type setType, string baseKey)
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{setType.Name}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, setType);
                if (asset == null) continue;

                var baseKeysProp = new SerializedObject(asset).FindProperty("_baseKeys");
                if (baseKeysProp == null) continue;

                for (int i = 0; i < baseKeysProp.arraySize; i++)
                    if (baseKeysProp.GetArrayElementAtIndex(i).stringValue == baseKey)
                        return asset;
            }
            return null;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Copies sprites from other installed SpriteGlyphSets onto entries whose key matches
        /// (directly, e.g. "escape", or via known aliases such as up → up_arrow).
        /// </summary>
        static bool CopySpritesFromExistingSets(UnityEngine.Object targetSet)
        {
            string targetPath = AssetDatabase.GetAssetPath(targetSet);
            var targetSo = new SerializedObject(targetSet);
            var glyphs = targetSo.FindProperty("_glyphs");
            if (glyphs == null) return false;

            bool changed = false;
            for (int i = 0; i < glyphs.arraySize; i++)
            {
                var entry = glyphs.GetArrayElementAtIndex(i);
                var keyProp = entry.FindPropertyRelative("_key");
                var valueProp = entry.FindPropertyRelative("_value");
                if (keyProp == null || valueProp == null) continue;
                if (valueProp.objectReferenceValue != null) continue; // already assigned

                var sprite = FindSpriteForKey(keyProp.stringValue, targetPath);
                if (sprite == null) continue;

                valueProp.objectReferenceValue = sprite;
                changed = true;
            }

            if (changed)
            {
                targetSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(targetSet);
                Debug.Log("[RewiredHelper] Copied matching sprites from installed glyph sets onto the AndroidRemote glyph set.");
            }
            return changed;
        }

        static readonly string[][] KeyAliases =
        {
            new[] { "up", "up_arrow", "dpad/up", "move/up" },
            new[] { "down", "down_arrow", "dpad/down", "move/down" },
            new[] { "left", "left_arrow", "dpad/left", "move/left" },
            new[] { "right", "right_arrow", "dpad/right", "move/right" },
            new[] { "joystick_button_0", "a", "enter", "submit" },
            new[] { "escape", "escape", "back" },
            new[] { "menu", "menu", "start" },
            new[] { "keypad_enter", "keypad_enter", "enter", "a" },
        };

        static UnityEngine.Sprite FindSpriteForKey(string entryKey, string excludeAssetPath)
        {
            var setType = FindType("Rewired.Glyphs.SpriteGlyphSet");
            if (setType == null) return null;

            foreach (var guid in AssetDatabase.FindAssets($"t:{setType.Name}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == excludeAssetPath) continue;
                var asset = AssetDatabase.LoadAssetAtPath(path, setType);
                if (asset == null) continue;

                var glyphs = new SerializedObject(asset).FindProperty("_glyphs");
                if (glyphs == null) continue;

                for (int i = 0; i < glyphs.arraySize; i++)
                {
                    var entry = glyphs.GetArrayElementAtIndex(i);
                    var keyProp = entry.FindPropertyRelative("_key");
                    var valueProp = entry.FindPropertyRelative("_value");
                    if (keyProp == null || valueProp == null) continue;
                    if (valueProp.objectReferenceValue == null) continue;

                    foreach (var aliasGroup in KeyAliases)
                    {
                        if (aliasGroup[0] != entryKey) continue;
                        for (int a = 1; a < aliasGroup.Length; a++)
                            if (aliasGroup[a] == keyProp.stringValue)
                                return valueProp.objectReferenceValue as UnityEngine.Sprite;
                    }
                }
            }
            return null;
        }

        // ------------------------------------------------------------------

        static bool AddToCollectionAndReload(UnityEngine.Object setAsset, Component inputManager)
        {
            var collectionType = FindType("Rewired.Glyphs.GlyphSetCollection");
            var providerType = FindType("Rewired.Glyphs.GlyphProvider");
            if (collectionType == null || providerType == null) return false;

            // Find the provider on the input manager and read its collection list
            var provider = inputManager.GetComponent(providerType);
            if (provider == null)
            {
                Debug.LogWarning("[RewiredHelper] No GlyphProvider found on the Input Manager — run 'Create Rewired Input Manager' first.");
                return false;
            }

            var providerSo = new SerializedObject(provider);
            var collections = providerSo.FindProperty("_glyphSetCollections");
            if (collections == null || collections.arraySize == 0)
            {
                Debug.LogWarning("[RewiredHelper] GlyphProvider has no GlyphSetCollection assigned.");
                return false;
            }

            var collection = collections.GetArrayElementAtIndex(0).objectReferenceValue;
            var collectionSo = new SerializedObject(collection);
            var sets = collectionSo.FindProperty("_sets");
            if (sets == null)
            {
                Debug.LogWarning("[RewiredHelper] GlyphSetCollection has no '_sets' property — Rewired version may differ.");
                return false;
            }

            for (int i = 0; i < sets.arraySize; i++)
                if (sets.GetArrayElementAtIndex(i).objectReferenceValue == setAsset)
                    return false; // already added

            sets.arraySize++;
            sets.GetArrayElementAtIndex(sets.arraySize - 1).objectReferenceValue = setAsset;
            collectionSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(collection);

            // Trigger GlyphProvider.Reload() via reflection so the new glyphs are cached immediately
            var reloadMethod = providerType.GetMethod("Reload");
            reloadMethod?.Invoke(provider, null);

            Debug.Log($"[RewiredHelper] Added '{setAsset.name}' to Glyph Set Collection '{collection.name}' and reloaded the Glyph Provider.");
            return true;
        }

        static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try { type = assembly.GetType(typeName); }
                catch { continue; }
                if (type != null) return type;
            }
            return null;
        }
    }
}
