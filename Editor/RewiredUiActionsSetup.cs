using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Creates the four Rewired actions the Event System's <c>RewiredStandaloneInputModule</c> reads for menu navigation
    /// (horizontal / vertical / submit / cancel) and maps them: gamepad (left stick, D-pad, A, B) and keyboard (arrows,
    /// Enter, Escape). Without them a gamepad cannot move the selection or press a button in any menu.
    /// <para>
    /// Everything is written through <see cref="SerializedObject"/> on the Rewired Input Manager's serialized user data
    /// (the same layout <see cref="DefaultSetupGenerator"/> already reads), so it is undoable and never touches
    /// anything that already exists: actions that are present are left alone and only the missing ones are added.
    /// </para>
    /// </summary>
    internal static class RewiredUiActionsSetup
    {
        internal const string DefaultHorizontalName = "UIHorizontal";
        internal const string DefaultVerticalName = "UIVertical";
        internal const string DefaultSubmitName = "UISubmit";
        internal const string DefaultCancelName = "UICancel";

        /// <summary>Rewired's Gamepad Template: one map that every recognised gamepad (Xbox, PlayStation, Switch, Deck...) shares.</summary>
        internal const string GamepadTemplateGuid = "83b427e4-086f-47f3-bb06-be266abd1ca5";

        // Gamepad Template element identifier ids (verified against the maps shipped in Assets/Rewired/Examples).
        private const int PadLeftStickX = 0;
        private const int PadLeftStickY = 1;
        private const int PadButtonA = 4;        // Action Bottom Row 1
        private const int PadButtonB = 5;        // Action Bottom Row 2
        private const int PadDPadUp = 19;
        private const int PadDPadRight = 20;
        private const int PadDPadDown = 21;
        private const int PadDPadLeft = 22;

        private const int ActionTypeAxis = 0;
        private const int ActionTypeButton = 1;
        private const int ElementTypeAxis = 0;
        private const int ElementTypeButton = 1;
        private const int AxisRangeFull = 0;
        private const int ContributionPositive = 0;
        private const int ContributionNegative = 1;
        private const int KeyboardElementId = -1;

        /// <summary>The role each action plays for the Event System, and the name the module expects for it.</summary>
        internal enum Role
        {
            Horizontal,
            Vertical,
            Submit,
            Cancel
        }

        internal static Dictionary<Role, string> DefaultNames() => new Dictionary<Role, string>
        {
            { Role.Horizontal, DefaultHorizontalName },
            { Role.Vertical, DefaultVerticalName },
            { Role.Submit, DefaultSubmitName },
            { Role.Cancel, DefaultCancelName },
        };

        /// <summary>The action names the scene's input module reads, falling back to the defaults for any it leaves empty.</summary>
        internal static Dictionary<Role, string> NamesFromModule(Component module)
        {
            var names = DefaultNames();
            if (module == null) return names;

            var so = new SerializedObject(module);
            Override(names, Role.Horizontal, so.FindProperty("m_HorizontalAxis"));
            Override(names, Role.Vertical, so.FindProperty("m_VerticalAxis"));
            Override(names, Role.Submit, so.FindProperty("m_SubmitButton"));
            Override(names, Role.Cancel, so.FindProperty("m_CancelButton"));
            return names;
        }

        private static void Override(Dictionary<Role, string> names, Role role, SerializedProperty property)
        {
            if (property != null && !string.IsNullOrEmpty(property.stringValue))
                names[role] = property.stringValue;
        }

        /// <summary>Lists the action names defined in the Input Manager, or null if its serialized layout is not recognised.</summary>
        internal static HashSet<string> ReadActionNames(Component inputManager)
        {
            var actions = new SerializedObject(inputManager).FindProperty("_userData.actions");
            if (actions == null) return null;

            var names = new HashSet<string>();
            for (var i = 0; i < actions.arraySize; i++)
            {
                var name = actions.GetArrayElementAtIndex(i).FindPropertyRelative("_name")?.stringValue;
                if (!string.IsNullOrEmpty(name)) names.Add(name);
            }

            return names;
        }

        /// <summary>Menu entry: creates the missing UI actions for the scene's Rewired Input Manager.</summary>
        [MenuItem(RewiredHelperMenu.CreateUiActions, priority = RewiredHelperMenu.CreateUiActionsPriority)]
        internal static void CreateUiActionsMenu()
        {
            var inputManager = DefaultSetupGenerator.FindInputManagerInScene();
            if (inputManager == null)
            {
                EditorUtility.DisplayDialog("Rewired UI Actions", "No Rewired Input Manager in the open scene. Create one first (Setup > Create Rewired Input Manager).", "OK");
                return;
            }

            var moduleType = DefaultSetupGenerator.FindRewiredInputModuleType();
            var module = moduleType != null ? UnityEngine.Object.FindObjectOfType(moduleType) as Component : null;
            EnsureUiActions(inputManager, NamesFromModule(module));
        }

        /// <summary>
        /// Adds whichever of the four UI actions are missing and maps them. Safe to run repeatedly. Returns the names created.
        /// </summary>
        internal static List<string> EnsureUiActions(Component inputManager, IReadOnlyDictionary<Role, string> names)
        {
            var created = new List<string>();
            var existing = ReadActionNames(inputManager);
            if (existing == null)
            {
                Debug.LogWarning("[RewiredHelper] Could not read the Rewired Input Manager's actions: its serialized layout is not the one this was built against.");
                return created;
            }

            var so = new SerializedObject(inputManager);
            var userData = so.FindProperty("_userData");
            var categoryId = FirstId(userData.FindPropertyRelative("actionCategories"), "_id");
            var behaviorId = FirstId(userData.FindPropertyRelative("inputBehaviors"), "_id");

            var ids = new Dictionary<Role, int>();
            foreach (var role in new[] { Role.Horizontal, Role.Vertical, Role.Submit, Role.Cancel })
            {
                var name = names[role];
                if (existing.Contains(name)) continue;

                ids[role] = AddAction(so, userData, name, role, categoryId, behaviorId);
                created.Add(name);
            }

            if (created.Count == 0) return created;

            AddGamepadMappings(userData, ids, categoryId);
            AddKeyboardMappings(userData, ids, categoryId);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(inputManager);
            ApplyToPrefabIfNeeded(inputManager);
            if (inputManager.gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(inputManager.gameObject.scene);

            Debug.Log($"[RewiredHelper] Created Rewired UI action(s) {string.Join(", ", created)} and mapped them: " +
                      "gamepad left stick / D-pad / A (submit) / B (cancel), keyboard arrows / Enter / Escape.");
            return created;
        }

        #region Actions

        private static int AddAction(SerializedObject so, SerializedProperty userData, string name, Role role, int categoryId, int behaviorId)
        {
            var counter = userData.FindPropertyRelative("actionIdCounter");
            var id = counter.intValue;
            counter.intValue = id + 1;

            var actions = userData.FindPropertyRelative("actions");
            actions.arraySize++;
            var action = actions.GetArrayElementAtIndex(actions.arraySize - 1);

            var isAxis = role == Role.Horizontal || role == Role.Vertical;
            action.FindPropertyRelative("_id").intValue = id;
            action.FindPropertyRelative("_name").stringValue = name;
            action.FindPropertyRelative("_descriptiveName").stringValue = name;
            action.FindPropertyRelative("_positiveDescriptiveName").stringValue = isAxis ? (role == Role.Horizontal ? "Right" : "Up") : string.Empty;
            action.FindPropertyRelative("_negativeDescriptiveName").stringValue = isAxis ? (role == Role.Horizontal ? "Left" : "Down") : string.Empty;
            action.FindPropertyRelative("_type").intValue = isAxis ? ActionTypeAxis : ActionTypeButton;
            action.FindPropertyRelative("_behaviorId").intValue = behaviorId;
            action.FindPropertyRelative("_userAssignable").boolValue = true;
            action.FindPropertyRelative("_categoryId").intValue = categoryId;

            RegisterInCategoryMap(userData, categoryId, id);
            return id;
        }

        /// <summary>Rewired keeps a per-category list of action ids as a hex string of little-endian int32 values.</summary>
        private static void RegisterInCategoryMap(SerializedProperty userData, int categoryId, int actionId)
        {
            var list = userData.FindPropertyRelative("actionCategoryMap")?.FindPropertyRelative("list");
            if (list == null) return;

            for (var i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("categoryId").intValue != categoryId) continue;

                var actionIds = entry.FindPropertyRelative("actionIds");
                actionIds.stringValue += EncodeId(actionId);
                return;
            }
        }

        /// <summary>One id as 8 lowercase hex digits, little-endian (id 10 -> "0a000000").</summary>
        internal static string EncodeId(int id) =>
            BitConverter.ToString(BitConverter.GetBytes(id)).Replace("-", string.Empty).ToLowerInvariant();

        #endregion

        #region Mappings

        private static void AddGamepadMappings(SerializedProperty userData, IReadOnlyDictionary<Role, int> ids, int categoryId)
        {
            var elements = FindOrCreateMap(userData.FindPropertyRelative("joystickMaps"), categoryId, GamepadTemplateGuid,
                FirstId(userData.FindPropertyRelative("joystickLayouts"), "_id"));
            if (elements == null) return;

            if (ids.TryGetValue(Role.Horizontal, out var horizontal))
            {
                AddElement(elements, categoryId, horizontal, ElementTypeAxis, PadLeftStickX, AxisRangeFull, ContributionPositive, 0);
                AddElement(elements, categoryId, horizontal, ElementTypeButton, PadDPadRight, AxisRangeFull, ContributionPositive, 0);
                AddElement(elements, categoryId, horizontal, ElementTypeButton, PadDPadLeft, AxisRangeFull, ContributionNegative, 0);
            }

            if (ids.TryGetValue(Role.Vertical, out var vertical))
            {
                AddElement(elements, categoryId, vertical, ElementTypeAxis, PadLeftStickY, AxisRangeFull, ContributionPositive, 0);
                AddElement(elements, categoryId, vertical, ElementTypeButton, PadDPadUp, AxisRangeFull, ContributionPositive, 0);
                AddElement(elements, categoryId, vertical, ElementTypeButton, PadDPadDown, AxisRangeFull, ContributionNegative, 0);
            }

            if (ids.TryGetValue(Role.Submit, out var submit))
                AddElement(elements, categoryId, submit, ElementTypeButton, PadButtonA, AxisRangeFull, ContributionPositive, 0);

            if (ids.TryGetValue(Role.Cancel, out var cancel))
                AddElement(elements, categoryId, cancel, ElementTypeButton, PadButtonB, AxisRangeFull, ContributionPositive, 0);
        }

        private static void AddKeyboardMappings(SerializedProperty userData, IReadOnlyDictionary<Role, int> ids, int categoryId)
        {
            var layoutId = FirstId(userData.FindPropertyRelative("keyboardLayouts"), "_id");
            var elements = FindOrCreateMap(userData.FindPropertyRelative("keyboardMaps"), categoryId, string.Empty, layoutId);
            if (elements == null) return;

            if (ids.TryGetValue(Role.Horizontal, out var horizontal))
            {
                AddKey(elements, categoryId, horizontal, KeyCode.RightArrow, ContributionPositive);
                AddKey(elements, categoryId, horizontal, KeyCode.LeftArrow, ContributionNegative);
            }

            if (ids.TryGetValue(Role.Vertical, out var vertical))
            {
                AddKey(elements, categoryId, vertical, KeyCode.UpArrow, ContributionPositive);
                AddKey(elements, categoryId, vertical, KeyCode.DownArrow, ContributionNegative);
            }

            if (ids.TryGetValue(Role.Submit, out var submit))
            {
                AddKey(elements, categoryId, submit, KeyCode.Return, ContributionPositive);
                AddKey(elements, categoryId, submit, KeyCode.KeypadEnter, ContributionPositive);
            }

            if (ids.TryGetValue(Role.Cancel, out var cancel))
                AddKey(elements, categoryId, cancel, KeyCode.Escape, ContributionPositive);

            EnableKeyboardMapForPlayers(userData, categoryId, layoutId);
        }

        /// <summary>A keyboard map only applies to players that list it among their default keyboard maps.</summary>
        private static void EnableKeyboardMapForPlayers(SerializedProperty userData, int categoryId, int layoutId)
        {
            var players = userData.FindPropertyRelative("players");
            if (players == null) return;

            for (var i = 0; i < players.arraySize; i++)
            {
                var player = players.GetArrayElementAtIndex(i);
                if (!player.FindPropertyRelative("_assignKeyboardOnStart").boolValue) continue;

                var defaults = player.FindPropertyRelative("_defaultKeyboardMaps");
                if (defaults == null || ContainsDefaultMap(defaults, categoryId, layoutId)) continue;

                defaults.arraySize++;
                var entry = defaults.GetArrayElementAtIndex(defaults.arraySize - 1);
                entry.FindPropertyRelative("_enabled").boolValue = true;
                entry.FindPropertyRelative("_categoryId").intValue = categoryId;
                entry.FindPropertyRelative("_layoutId").intValue = layoutId;
            }
        }

        private static bool ContainsDefaultMap(SerializedProperty defaults, int categoryId, int layoutId)
        {
            for (var i = 0; i < defaults.arraySize; i++)
            {
                var entry = defaults.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("_categoryId").intValue == categoryId &&
                    entry.FindPropertyRelative("_layoutId").intValue == layoutId)
                    return true;
            }

            return false;
        }

        /// <summary>Returns the <c>actionElementMaps</c> of the matching map, creating the map when the project has none yet.</summary>
        private static SerializedProperty FindOrCreateMap(SerializedProperty maps, int categoryId, string hardwareGuid, int layoutId)
        {
            if (maps == null) return null;

            var nextId = 0;
            for (var i = 0; i < maps.arraySize; i++)
            {
                var map = maps.GetArrayElementAtIndex(i);
                nextId = Math.Max(nextId, map.FindPropertyRelative("id").intValue + 1);

                if (map.FindPropertyRelative("categoryId").intValue == categoryId &&
                    string.Equals(map.FindPropertyRelative("hardwareGuidString").stringValue ?? string.Empty, hardwareGuid, StringComparison.OrdinalIgnoreCase))
                    return map.FindPropertyRelative("actionElementMaps");
            }

            maps.arraySize++;
            var created = maps.GetArrayElementAtIndex(maps.arraySize - 1);
            created.FindPropertyRelative("id").intValue = nextId;
            created.FindPropertyRelative("categoryId").intValue = categoryId;
            created.FindPropertyRelative("layoutId").intValue = layoutId;
            created.FindPropertyRelative("name").stringValue = string.Empty;
            created.FindPropertyRelative("hardwareGuidString").stringValue = hardwareGuid;
            created.FindPropertyRelative("customControllerUid").intValue = 0;
            var elements = created.FindPropertyRelative("actionElementMaps");
            elements.arraySize = 0;
            return elements;
        }

        private static void AddKey(SerializedProperty elements, int categoryId, int actionId, KeyCode key, int contribution) =>
            AddElement(elements, categoryId, actionId, ElementTypeButton, KeyboardElementId, AxisRangeFull, contribution, (int)key);

        private static void AddElement(SerializedProperty elements, int categoryId, int actionId, int elementType, int elementId,
            int axisRange, int contribution, int keyCode)
        {
            elements.arraySize++;
            var element = elements.GetArrayElementAtIndex(elements.arraySize - 1);
            element.FindPropertyRelative("_actionCategoryId").intValue = categoryId;
            element.FindPropertyRelative("_actionId").intValue = actionId;
            element.FindPropertyRelative("_elementType").intValue = elementType;
            element.FindPropertyRelative("_elementIdentifierId").intValue = elementId;
            element.FindPropertyRelative("_axisRange").intValue = axisRange;
            element.FindPropertyRelative("_invert").boolValue = false;
            element.FindPropertyRelative("_axisContribution").intValue = contribution;
            element.FindPropertyRelative("_keyboardKeyCode").intValue = keyCode;
            element.FindPropertyRelative("_modifierKey1").intValue = 0;
            element.FindPropertyRelative("_modifierKey2").intValue = 0;
            element.FindPropertyRelative("_modifierKey3").intValue = 0;
        }

        #endregion

        private static int FirstId(SerializedProperty list, string idField)
        {
            if (list == null || list.arraySize == 0) return 0;
            return list.GetArrayElementAtIndex(0).FindPropertyRelative(idField).intValue;
        }

        /// <summary>
        /// The Input Manager normally lives in a prefab shared by every scene. Writing only the scene override would leave the
        /// other scenes without the actions, so push the change into the prefab asset.
        /// </summary>
        private static void ApplyToPrefabIfNeeded(Component inputManager)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(inputManager)) return;

            try
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(inputManager);
                var path = source != null ? AssetDatabase.GetAssetPath(source) : null;
                if (string.IsNullOrEmpty(path)) return;

                PrefabUtility.ApplyObjectOverride(inputManager, path, InteractionMode.UserAction);
                Debug.Log($"[RewiredHelper] Applied the new Rewired actions to the prefab '{path}'.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RewiredHelper] Created the UI actions in the scene but could not apply them to the prefab: {ex.Message}");
            }
        }
    }
}
