using System;
using System.Collections.Generic;
using System.Linq;
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
            else
            {
                UpdateExistingRows();
            }
        }

        private void OnEnable()
        {
            RewiredInputManager.OnInputSpecializationChanged += OnInputSpecializationChanged;
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
                if (actionName.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    actionName.IndexOf("Scroll", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return $"<rewiredElement playerId=0 controllerType=\"Mouse\" actionName=\"{actionName}\">";
                }
                return $"<rewiredElement playerId=0 controllerType=\"Keyboard\" actionName=\"{actionName}\">";
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
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var txt in texts)
            {
                string originalText = txt.text;
                if (string.IsNullOrEmpty(originalText) || !originalText.Contains("<rewiredElement")) continue;

                string updatedText = UpdateControllerTypeInTags(originalText);
                if (updatedText != originalText)
                {
                    txt.text = updatedText;
                }
            }
        }

        private string UpdateControllerTypeInTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (RewiredInputManager.Instance == null) return text;

            var currentType = RewiredInputManager.Instance.CurrentControllerType;
            string targetType = currentType == ControllerType.Joystick ? "Joystick" : "";

            return System.Text.RegularExpressions.Regex.Replace(text, @"<rewiredElement\b[^>]*>", match =>
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
                    else
                    {
                        typeToUse = "Keyboard";
                    }
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
        }
    }
}
