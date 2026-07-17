using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Rewired;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// Rebuilds the controller-help row list from the live Rewired action map at runtime, instead
    /// of relying on rows baked once at edit time. The Editor generator
    /// (DefaultSetupGenerator.GenerateRowBasedHelpForm) can only read ReInput.mapping.Actions when
    /// Rewired is initialized, which normally means Play mode — so whatever it bakes into a prefab
    /// at edit time is either stale or (if generated with an empty mapping) a handful of placeholder
    /// rows with no real actions or glyphs. Attach this to the row container ("Content") and it
    /// clears and rebuilds with the actual current actions every time the game starts.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ControllerHelpRowBuilder : MonoBehaviour
    {
        const string GlyphHelperTypeName = "Rewired.Glyphs.UnityUI.UnityUITextMeshProGlyphHelper";

        [Tooltip("Rewired action names to show as rows, in this order. Leave empty to show every " +
            "Button-type action in the current Rewired mapping (the default, zero-config behavior).")]
        [SerializeField] private List<string> actionNames = new List<string>();

        [Tooltip("If true, the rows will be cleared and rebuilt automatically on Awake. Disable this if you want to customize rows in Design Time.")]
        [SerializeField] private bool rebuildOnAwake = true;

        public bool RebuildOnAwake
        {
            get => rebuildOnAwake;
            set => rebuildOnAwake = value;
        }

        public List<string> ActionNames => actionNames;

        private struct ActionInfo
        {
            public string Name;
            public string DescriptiveName;
        }

        private void Awake()
        {
            if (rebuildOnAwake)
            {
                Rebuild();
            }
            // The rebuildOnAwake=false path used to start its refresh coroutine here, but this
            // component commonly lives on a modal popup that starts inactive in the scene.
            // StartCoroutine() silently no-ops on an inactive GameObject (Unity logs a warning
            // and never runs it), so that refresh would never happen for a popup opened later via
            // SetActive(true). Moved to OnEnable, which only fires once the object is truly active.
        }

        private System.Collections.IEnumerator UpdateExistingRowsNextFrame()
        {
            yield return null;
            UpdateExistingRows();
        }

        private void OnEnable()
        {
            RewiredInputManager.OnInputSpecializationChanged += OnInputSpecializationChanged;

            if (!rebuildOnAwake)
            {
                // Delay 1 frame: RewiredInputManager.Start() sets the initial controller (Keyboard)
                // during Start(), but this may run before that on the very first activation.
                // Waiting ensures we read the correct CurrentControllerType when generating tags.
                // Also re-runs on every reopen, so a stale controllerType from a prior session
                // doesn't linger.
                StartCoroutine(UpdateExistingRowsNextFrame());
            }
        }

        private void OnDisable()
        {
            RewiredInputManager.OnInputSpecializationChanged -= OnInputSpecializationChanged;
        }

        private void OnInputSpecializationChanged()
        {
            if (Application.isPlaying)
            {
                if (rebuildOnAwake)
                {
                    Rebuild();
                }
                else
                {
                    UpdateExistingRows();
                }
            }
        }

        public void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.DestroyObjectImmediate(transform.GetChild(i).gameObject);
                else
#endif
                    Destroy(transform.GetChild(i).gameObject);
            }

            var glyphHelperType = FindGlyphHelperType();
            var actions = ResolveActions();

            for (int i = 0; i < actions.Count; i++)
                CreateRow(actions[i].Name, actions[i].DescriptiveName, glyphHelperType, i % 2 == 1);

            UpdateTitle();
        }

        /// <summary>
        /// Uses the curated <see cref="actionNames"/> list, in that order, when set — letting a
        /// consumer hand-pick and reorder which actions show (e.g. to drop keyboard/mouse actions
        /// like "MouseLeftButton" out of a gamepad-only help screen). Falls back to every Button-type
        /// action in the live mapping when the list is empty, so the component still works with zero
        /// configuration.
        /// </summary>
        private List<ActionInfo> ResolveActions()
        {
            var rawList = new List<ActionInfo>();
            var managerActions = GetActionsFromManagerInScene();

            if (actionNames != null && actionNames.Count > 0)
            {
                foreach (var name in actionNames)
                {
                    string desc = name;
                    if (ReInput.isReady && ReInput.mapping != null)
                    {
                        var action = ReInput.mapping.GetAction(name);
                        if (action != null)
                            desc = action.descriptiveName;
                    }
                    else
                    {
                        var found = managerActions.FirstOrDefault(a => a.Name == name);
                        if (!string.IsNullOrEmpty(found.DescriptiveName))
                            desc = found.DescriptiveName;
                    }
                    rawList.Add(new ActionInfo { Name = name, DescriptiveName = desc });
                }
            }
            else if (ReInput.isReady && ReInput.mapping != null)
            {
                var actions = ReInput.mapping.Actions
                    .Where(a => a.type == InputActionType.Button)
                    .OrderBy(a => a.categoryId)
                    .ThenBy(a => a.name);

                foreach (var a in actions)
                {
                    rawList.Add(new ActionInfo { Name = a.name, DescriptiveName = a.descriptiveName });
                }
            }
            else
            {
                foreach (var action in managerActions)
                {
                    rawList.Add(action);
                }
            }

            var groupedList = new List<ActionInfo>();
            var processedNames = new HashSet<string>();

            for (int i = 0; i < rawList.Count; i++)
            {
                var current = rawList[i];
                if (processedNames.Contains(current.Name)) continue;

                if (current.Name.EndsWith("X", StringComparison.OrdinalIgnoreCase))
                {
                    string baseName = current.Name.Substring(0, current.Name.Length - 1);
                    string yName = baseName + "Y";

                    var matchingY = rawList.FirstOrDefault(a => string.Equals(a.Name, yName, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(matchingY.Name))
                    {
                        processedNames.Add(current.Name);
                        processedNames.Add(matchingY.Name);

                        string groupName = current.Name + "+" + matchingY.Name;
                        string groupDesc = !string.IsNullOrEmpty(baseName) ? baseName : "MOVEMENT";

                        if (groupDesc.Equals("Mouse", StringComparison.OrdinalIgnoreCase))
                            groupDesc = "MOVIMENTO";
                        else if (groupDesc.Equals("Move", StringComparison.OrdinalIgnoreCase))
                            groupDesc = "MOVER";

                        groupedList.Add(new ActionInfo { Name = groupName, DescriptiveName = groupDesc.ToUpper() });
                        continue;
                    }
                }

                processedNames.Add(current.Name);
                groupedList.Add(current);
            }

            return groupedList;
        }

        private List<ActionInfo> GetActionsFromManagerInScene()
        {
            var list = new List<ActionInfo>();

#if UNITY_EDITOR
            UnityEngine.Component manager = null;
            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                var comp = go.GetComponent("InputManager");
                if (comp != null && comp.GetType().FullName == "Rewired.InputManager")
                {
                    manager = comp;
                    break;
                }
            }

            if (manager != null)
            {
                var so = new UnityEditor.SerializedObject(manager);
                var actionsProp = so.FindProperty("_userData.actions");
                if (actionsProp == null)
                    actionsProp = so.FindProperty("actions");

                if (actionsProp != null && actionsProp.isArray)
                {
                    for (int i = 0; i < actionsProp.arraySize; i++)
                    {
                        var element = actionsProp.GetArrayElementAtIndex(i);
                        var nameProp = element.FindPropertyRelative("_name");
                        var descProp = element.FindPropertyRelative("_descriptiveName");
                        if (nameProp != null)
                        {
                            list.Add(new ActionInfo
                            {
                                Name = nameProp.stringValue,
                                DescriptiveName = descProp != null && !string.IsNullOrEmpty(descProp.stringValue) 
                                    ? descProp.stringValue 
                                    : nameProp.stringValue
                            });
                        }
                    }
                }
            }
#endif
            return list;
        }

        private void CreateRow(string actionName, string actionDesc, Type glyphHelperType, bool isAlt)
        {
            var rowGo = new GameObject($"Row_{actionName}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.Undo.RegisterCreatedObjectUndo(rowGo, "Create Help Row");
#endif

            var rowRect = (RectTransform)rowGo.transform;
            rowRect.SetParent(transform, false);
            rowRect.sizeDelta = new Vector2(265, 44);

            var bgImage = rowGo.GetComponent<Image>();
            bgImage.color = isAlt
                ? new Color(0.12f, 0.12f, 0.14f, 0.7f)
                : new Color(0.15f, 0.15f, 0.17f, 0.8f);

            var accentBarGo = new GameObject("AccentTag", typeof(RectTransform), typeof(Image));
            var accentBarRect = (RectTransform)accentBarGo.transform;
            accentBarRect.SetParent(rowRect, false);
            accentBarRect.anchorMin = new Vector2(0, 0.5f);
            accentBarRect.anchorMax = new Vector2(0, 0.5f);
            accentBarRect.pivot = new Vector2(0, 0.5f);
            accentBarRect.sizeDelta = new Vector2(3, 26);
            accentBarRect.anchoredPosition = new Vector2(4, 0);
            accentBarGo.GetComponent<Image>().color = new Color(0.22f, 0.60f, 1.00f);

            var accentLayoutElement = accentBarGo.AddComponent<LayoutElement>();
            accentLayoutElement.ignoreLayout = true;

            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(10, 10, 3, 3);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(rowRect, false);
            iconRect.sizeDelta = new Vector2(110, 36);

            var iconText = iconGo.AddComponent<TextMeshProUGUI>();
            string formattedText = "";
            if (actionName.Contains("+"))
            {
                var parts = actionName.Split('+');
                foreach (var part in parts)
                {
                    if (formattedText.Length > 0) formattedText += " ";
                    formattedText += GetTagForAction(part);
                }
            }
            else
            {
                formattedText = GetTagForAction(actionName);
            }

            iconText.fontSize = 24;
            iconText.color = Color.white;
            iconText.alignment = TextAlignmentOptions.Right;

            if (glyphHelperType != null)
            {
                var glyphHelper = iconGo.AddComponent(glyphHelperType);
                ApplyElementFilter(glyphHelper, glyphHelperType);
                var textProp = glyphHelperType.GetProperty("text");
                if (textProp != null)
                    textProp.SetValue(glyphHelper, formattedText);
            }
            else
            {
                iconText.text = formattedText;
            }

            var divGo = new GameObject("Divider", typeof(RectTransform));
            var divRect = (RectTransform)divGo.transform;
            divRect.SetParent(rowRect, false);
            divRect.sizeDelta = new Vector2(10, 36);
            var divText = divGo.AddComponent<TextMeshProUGUI>();
            divText.text = "—";
            divText.fontSize = 14;
            divText.color = new Color(0.4f, 0.4f, 0.45f);
            divText.alignment = TextAlignmentOptions.Center;

            var descGo = new GameObject("Description", typeof(RectTransform));
            var descRect = (RectTransform)descGo.transform;
            descRect.SetParent(rowRect, false);
            descRect.sizeDelta = new Vector2(115, 36);

            var descText = descGo.AddComponent<TextMeshProUGUI>();
            descText.text = !string.IsNullOrEmpty(actionDesc) ? actionDesc.ToUpper() : NicifyActionName(actionName);
            descText.fontSize = 13;
            descText.fontStyle = FontStyles.Bold;
            descText.color = new Color(0.75f, 0.75f, 0.8f);
            descText.alignment = TextAlignmentOptions.Left;
        }

        // Rewired's <rewiredElement> tag has no "controllerType" attribute (its real attributes
        // are type/playerId/playerName/actionId/actionName/actionRange/resultIndex) — an explicit
        // controllerType here is silently ignored. Resolution is instead steered by excluding
        // unwanted controller-map bindings via isRewiredElementAllowedHandler (see
        // ApplyElementFilter/IsRewiredElementAllowed), so a plain tag is all that's needed here.
        private string GetTagForAction(string actionName)
        {
            return $"<rewiredElement playerId=0 actionName=\"{actionName}\">";
        }

        private static string NicifyActionName(string actionName)
        {
            if (string.IsNullOrEmpty(actionName)) return "";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < actionName.Length; i++)
            {
                if (i > 0 && char.IsUpper(actionName[i]) && !char.IsUpper(actionName[i - 1]))
                    sb.Append(' ');
                sb.Append(actionName[i]);
            }
            return sb.ToString().ToUpper();
        }

        private static Type FindGlyphHelperType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(GlyphHelperTypeName);
                if (type != null)
                    return type;
            }
            return null;
        }

        private void UpdateExistingRows()
        {
            // Rows baked at design time (e.g. a hand-curated ControllerHelpForm prefab with
            // rebuildOnAwake off) never had ApplyElementFilter run on their glyph helper — that
            // only happens in CreateRow, for freshly generated rows. Attach it here too so
            // pre-existing rows also exclude Joystick bindings on PC and resolve correctly.
            var glyphHelperType = FindGlyphHelperType();
            if (glyphHelperType != null)
            {
                foreach (var txt in GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    var glyphHelper = txt.GetComponent(glyphHelperType);
                    if (glyphHelper != null)
                        ApplyElementFilter(glyphHelper, glyphHelperType);
                }
            }

            UpdateTitle();
        }

        private void UpdateTitle()
        {
            if (RewiredInputManager.Instance == null) return;

            var currentType = RewiredInputManager.Instance.CurrentControllerType;
            bool isGamepad = currentType == ControllerType.Joystick;

            Transform current = transform.parent;
            while (current != null)
            {
                var texts = current.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var txt in texts)
                {
                    var localizer = txt.GetComponent("Localize");
                    bool isTitleLocalizer = false;
                    if (localizer != null)
                    {
                        var termProp = localizer.GetType().GetField("mTerm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (termProp != null)
                        {
                            string termValue = termProp.GetValue(localizer) as string;
                            if (termValue == "GAMEPAD CONTROLS" || termValue == "KEYBOARD CONTROLS" || termValue == "KEYBOARD_CONTROLS")
                            {
                                isTitleLocalizer = true;
                                if (isGamepad)
                                {
                                    termProp.SetValue(localizer, "GAMEPAD CONTROLS");
                                }
                                else
                                {
                                    termProp.SetValue(localizer, "KEYBOARD_CONTROLS");
                                }

                                var localizeMethod = localizer.GetType().GetMethod("OnLocalize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (localizeMethod != null)
                                {
                                    localizeMethod.Invoke(localizer, new object[] { true });
                                }
                            }
                        }
                    }

                    if (txt.text.Contains("Controles") || txt.text.Contains("Controls") || isTitleLocalizer || txt.gameObject.name.Contains("Title") || txt.gameObject.name.Contains("Header"))
                    {
                        if (txt.gameObject.name == "Title" || txt.gameObject.name == "Header" || txt.text.Contains("Gamepad") || txt.text.Contains("Mando") || txt.text.Contains("Manette") || txt.text.Contains("Keyboard"))
                        {
                            string lang = "English";
                            try
                            {
                                var locMgrType = Type.GetType("I2.Loc.LocalizationManager, Assembly-CSharp");
                                if (locMgrType == null) locMgrType = Type.GetType("I2.Loc.LocalizationManager, Assembly-CSharp-firstpass");
                                if (locMgrType != null)
                                {
                                    var currentLangProp = locMgrType.GetProperty("CurrentLanguage", BindingFlags.Public | BindingFlags.Static);
                                    if (currentLangProp != null)
                                    {
                                        lang = currentLangProp.GetValue(null) as string;
                                    }
                                }
                            }
                            catch {}

                            if (isGamepad)
                            {
                                if (localizer != null)
                                {
                                    var behaviour = localizer as MonoBehaviour;
                                    if (behaviour != null) behaviour.enabled = true;

                                    var termProp = localizer.GetType().GetField("mTerm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (termProp != null) termProp.SetValue(localizer, "GAMEPAD CONTROLS");

                                    var localizeMethod = localizer.GetType().GetMethod("OnLocalize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (localizeMethod != null) localizeMethod.Invoke(localizer, new object[] { true });
                                }
                                else
                                {
                                    txt.text = GetGamepadControlsTranslation(lang);
                                }
                            }
                            else
                            {
                                if (localizer != null)
                                {
                                    var behaviour = localizer as MonoBehaviour;
                                    if (behaviour != null) behaviour.enabled = false;
                                }
                                txt.text = GetKeyboardControlsTranslation(lang);
                            }
                        }
                    }
                }
                current = current.parent;
            }
        }

        private string GetGamepadControlsTranslation(string lang)
        {
            switch (lang)
            {
                case "Portuguese": return "Controles De Gamepad";
                case "Spanish": return "Controles Del Mando";
                case "French": return "Commandes De La Manette";
                case "German": return "Gamepad-Steuerung";
                case "Italian": return "Comandi Del Gamepad";
                default: return "Gamepad Controls";
            }
        }

        private string GetKeyboardControlsTranslation(string lang)
        {
            switch (lang)
            {
                case "Portuguese": return "Controles De Teclado & Mouse";
                case "Spanish": return "Controles De Teclado Y Ratón";
                case "French": return "Commandes Clavier & Souris";
                case "German": return "Tastatur & Maus Steuerung";
                case "Italian": return "Comandi Tastiera E Mouse";
                default: return "Keyboard & Mouse Controls";
            }
        }

        /// <summary>
        /// Rewired's own "last active controller" tracking updates the instant ANY device
        /// produces input — including a keyboard key press while the player is really using the
        /// mouse — and its glyph resolution falls back to whichever OTHER controller has a
        /// binding for the action once the tracked device doesn't. Since our Joystick bindings
        /// exist alongside Mouse/Keyboard ones on the same actions (for real gamepad play), that
        /// fallback wanders to the Joystick binding on PC the moment a key is touched. Excluding
        /// Joystick-controller bindings whenever we're not actually in Joystick mode leaves only
        /// the intended Mouse/Keyboard binding, so Rewired can't help but render it.
        /// </summary>
        private static bool IsRewiredElementAllowed(int tagIndex, ActionElementMap aem)
        {
            if (RewiredInputManager.Instance == null) return true;
            if (aem?.controllerMap == null) return true;

            bool isJoystickMap = aem.controllerMap.controllerType == ControllerType.Joystick;
            bool weAreOnJoystick = RewiredInputManager.Instance.CurrentControllerType == ControllerType.Joystick;

            return weAreOnJoystick || !isJoystickMap;
        }

        private static readonly MethodInfo IsRewiredElementAllowedMethod =
            typeof(ControllerHelpRowBuilder).GetMethod(nameof(IsRewiredElementAllowed), BindingFlags.NonPublic | BindingFlags.Static);

        private static void ApplyElementFilter(object glyphHelper, Type glyphHelperType)
        {
            var filterProp = glyphHelperType.GetProperty("isRewiredElementAllowedHandler");
            if (filterProp == null || IsRewiredElementAllowedMethod == null) return;

            var handler = Delegate.CreateDelegate(filterProp.PropertyType, IsRewiredElementAllowedMethod);
            filterProp.SetValue(glyphHelper, handler);
        }
    }
}
