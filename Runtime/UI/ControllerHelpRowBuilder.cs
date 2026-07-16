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

        private void Awake()
        {
            Rebuild();
        }

        public void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            var glyphHelperType = FindGlyphHelperType();
            var actions = ResolveActions();

            for (int i = 0; i < actions.Count; i++)
                CreateRow(actions[i].name, actions[i].descriptiveName, glyphHelperType, i % 2 == 1);
        }

        /// <summary>
        /// Uses the curated <see cref="actionNames"/> list, in that order, when set — letting a
        /// consumer hand-pick and reorder which actions show (e.g. to drop keyboard/mouse actions
        /// like "MouseLeftButton" out of a gamepad-only help screen). Falls back to every Button-type
        /// action in the live mapping when the list is empty, so the component still works with zero
        /// configuration.
        /// </summary>
        private List<InputAction> ResolveActions()
        {
            if (ReInput.mapping == null) return new List<InputAction>();

            if (actionNames != null && actionNames.Count > 0)
            {
                var resolved = new List<InputAction>(actionNames.Count);
                foreach (var name in actionNames)
                {
                    var action = ReInput.mapping.GetAction(name);
                    if (action != null)
                        resolved.Add(action);
                    else
                        Debug.LogWarning($"[RewiredHelper] ControllerHelpRowBuilder: no Rewired action named \"{name}\" — skipping row.");
                }
                return resolved;
            }

            return ReInput.mapping.Actions
                .Where(a => a.type == InputActionType.Button)
                .OrderBy(a => a.categoryId)
                .ThenBy(a => a.name)
                .ToList();
        }

        private void CreateRow(string actionName, string actionDesc, Type glyphHelperType, bool isAlt)
        {
            var rowGo = new GameObject($"Row_{actionName}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            var rowRect = (RectTransform)rowGo.transform;
            rowRect.SetParent(transform, false);
            rowRect.sizeDelta = new Vector2(0, 38);

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
            hlg.spacing = 15f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(20, 20, 3, 3);

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(rowRect, false);
            iconRect.sizeDelta = new Vector2(140, 32);

            var iconText = iconGo.AddComponent<TextMeshProUGUI>();
            iconText.text = $"<rewiredElement playerId=0 actionName=\"{actionName}\">";
            iconText.fontSize = 18;
            iconText.color = Color.white;
            iconText.alignment = TextAlignmentOptions.Right;

            if (glyphHelperType != null)
            {
                var glyphHelper = iconGo.AddComponent(glyphHelperType);
                var textProp = glyphHelperType.GetProperty("text");
                if (textProp != null)
                    textProp.SetValue(glyphHelper, $"<rewiredElement playerId=0 actionName=\"{actionName}\">");
            }

            var divGo = new GameObject("Divider", typeof(RectTransform));
            var divRect = (RectTransform)divGo.transform;
            divRect.SetParent(rowRect, false);
            divRect.sizeDelta = new Vector2(15, 32);
            var divText = divGo.AddComponent<TextMeshProUGUI>();
            divText.text = "—";
            divText.fontSize = 14;
            divText.color = new Color(0.4f, 0.4f, 0.45f);
            divText.alignment = TextAlignmentOptions.Center;

            var descGo = new GameObject("Description", typeof(RectTransform));
            var descRect = (RectTransform)descGo.transform;
            descRect.SetParent(rowRect, false);
            descRect.sizeDelta = new Vector2(320, 32);

            var descText = descGo.AddComponent<TextMeshProUGUI>();
            descText.text = !string.IsNullOrEmpty(actionDesc) ? actionDesc.ToUpper() : NicifyActionName(actionName);
            descText.fontSize = 13;
            descText.fontStyle = FontStyles.Bold;
            descText.color = new Color(0.75f, 0.75f, 0.8f);
            descText.alignment = TextAlignmentOptions.Left;
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
    }
}
