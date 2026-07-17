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
                    string partText = GetTagForAction(part);
                    // Both halves of a grouped action (e.g. "MouseX+MouseY") commonly resolve to
                    // the same static Mouse label — avoid rendering it twice.
                    if (partText == MouseFallbackLabel && formattedText.Contains(MouseFallbackLabel))
                        continue;

                    if (formattedText.Length > 0) formattedText += " ";
                    formattedText += partText;
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

        private string GetTagForAction(string actionName)
        {
            if (RewiredInputManager.Instance == null)
            {
                return $"<rewiredElement playerId=0 actionName=\"{actionName}\">";
            }

            var currentType = RewiredInputManager.Instance.CurrentControllerType;
            if (currentType == ControllerType.Joystick)
            {
                return $"<rewiredElement playerId=0 controllerType=\"Joystick\" actionName=\"{actionName}\">";
            }
            else
            {
                // Mouse/Scroll named actions always use Mouse binding. Rendered as a plain static
                // label (see MouseFallbackLabel) rather than a live tag — without an icon glyph
                // theme, Rewired's text fallback describes whatever device last produced input,
                // ignoring this controllerType, so a dynamic tag would flicker on any keypress.
                if (actionName.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    actionName.IndexOf("Scroll", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return MouseFallbackLabel;
                }

                // For other PC actions, only force Keyboard if the action actually has a keyboard binding.
                if (ReInput.isReady && HasKeyboardBinding(actionName))
                {
                    return $"<rewiredElement playerId=0 controllerType=\"Keyboard\" actionName=\"{actionName}\">";
                }

                // No keyboard binding — same static-Mouse-label reasoning as above.
                return MouseFallbackLabel;
            }
        }

        private bool HasKeyboardBinding(string actionName)
        {
            try
            {
                var action = ReInput.mapping.GetAction(actionName);
                if (action == null) return false;

                var player = ReInput.players.GetPlayer(0);
                if (player == null) return false;

                // GetMaps overload that accepts a Controller object (not ControllerType enum).
                var keyboard = ReInput.controllers.GetController(ControllerType.Keyboard, 0);
                if (keyboard == null) return false;

                foreach (var map in player.controllers.maps.GetMaps(keyboard))
                {
                    if (map.GetFirstElementMapWithAction(action.id, false) != null)
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
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
            var glyphHelperType = FindGlyphHelperType();
            var textProp = glyphHelperType?.GetProperty("text");

            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var txt in texts)
            {
                // The glyph helper (when present) owns the authoritative tag text and re-renders
                // it into the TMP component every frame, so writing to txt.text directly gets
                // silently clobbered on the next refresh. Update the helper's own text instead.
                var glyphHelper = glyphHelperType != null ? txt.GetComponent(glyphHelperType) : null;
                string originalText = glyphHelper != null && textProp != null
                    ? textProp.GetValue(glyphHelper) as string
                    : txt.text;

                if (string.IsNullOrEmpty(originalText) || !originalText.Contains("<rewiredElement")) continue;

                string updatedText = UpdateControllerTypeInTags(originalText);
                if (updatedText != originalText)
                {
                    if (glyphHelper != null && textProp != null)
                        textProp.SetValue(glyphHelper, updatedText);
                    else
                        txt.text = updatedText;
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

        private const string MouseFallbackLabel = "Mouse";

        private string UpdateControllerTypeInTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (RewiredInputManager.Instance == null) return text;

            var currentType = RewiredInputManager.Instance.CurrentControllerType;
            string targetType = currentType == ControllerType.Joystick ? "Joystick" : "";

            string replaced = System.Text.RegularExpressions.Regex.Replace(text, @"<rewiredElement\b[^>]*>", match =>
            {
                string tag = match.Value;

                string actionName = "";
                var actionMatch = System.Text.RegularExpressions.Regex.Match(tag, @"\bactionName\s*=\s*""([^""]*)""");
                if (actionMatch.Success)
                {
                    actionName = actionMatch.Groups[1].Value;
                }

                string typeToUse = targetType;
                if (string.IsNullOrEmpty(typeToUse))
                {
                    if (!string.IsNullOrEmpty(actionName) &&
                        (actionName.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         actionName.IndexOf("Scroll", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        typeToUse = "Mouse";
                    }
                    else if (!string.IsNullOrEmpty(actionName) && ReInput.isReady && HasKeyboardBinding(actionName))
                    {
                        typeToUse = "Keyboard";
                    }
                    else
                    {
                        // No keyboard binding for this action on PC.
                        typeToUse = "Mouse";
                    }
                }

                // Without an icon glyph theme installed, Rewired's own text fallback for a
                // <rewiredElement> tag describes whatever physical device most recently produced
                // input, ignoring the controllerType attribute we set here — so a Mouse-forced row
                // still flickers to a keyboard/joystick-flavored label the instant a key is
                // pressed elsewhere. Render Mouse as a plain, static label instead of a live tag so
                // it can't drift. Keyboard/Joystick keep the dynamic tag since those correctly
                // reflect the actual bound key/button in practice.
                if (typeToUse == "Mouse")
                {
                    return MouseFallbackLabel;
                }

                if (System.Text.RegularExpressions.Regex.IsMatch(tag, @"\bcontrollerType\s*=\s*""[^""]*"""))
                {
                    tag = System.Text.RegularExpressions.Regex.Replace(tag, @"\bcontrollerType\s*=\s*""[^""]*""", $"controllerType=\"{typeToUse}\"");
                }
                else
                {
                    tag = tag.Insert("<rewiredElement".Length, $" controllerType=\"{typeToUse}\"");
                }

                return tag;
            });

            // A grouped row (e.g. "MouseX+MouseY") produces two tags joined by a space; if both
            // resolve to the static Mouse label, collapse the duplicate into one.
            return System.Text.RegularExpressions.Regex.Replace(
                replaced,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(MouseFallbackLabel)}(\s+{System.Text.RegularExpressions.Regex.Escape(MouseFallbackLabel)}\b)+",
                MouseFallbackLabel);
        }
    }
}
