using UnityEditor;
using UnityEngine;

using Wagenheimer.RewiredHelper.UI;

namespace Wagenheimer.RewiredHelper.Editor
{
    [CustomEditor(typeof(Dialog))]
    [CanEditMultipleObjects]
    public class DialogEditor : UnityEditor.Editor
    {
        private static Color ColBg => EditorGUIUtility.isProSkin
            ? new(0.16f, 0.16f, 0.18f) : new(0.82f, 0.82f, 0.84f);
        private static Color ColCard => EditorGUIUtility.isProSkin
            ? new(0.20f, 0.20f, 0.22f) : new(0.90f, 0.90f, 0.92f);
        private static Color ColGreen => EditorGUIUtility.isProSkin
            ? new(0.20f, 0.75f, 0.35f) : new(0.10f, 0.55f, 0.20f);
        private static Color ColRed => EditorGUIUtility.isProSkin
            ? new(0.85f, 0.25f, 0.20f) : new(0.70f, 0.15f, 0.10f);
        private static Color ColDim = new(0.55f, 0.55f, 0.60f);

        private static readonly Color ColOverlay = new(0.55f, 0.35f, 0.95f);
        private static readonly Color ColButtons = new(0.22f, 0.60f, 1.00f);
        private static readonly Color ColTiming = new(1.00f, 0.60f, 0.10f);
        private static readonly Color ColFocus = new(0.20f, 0.75f, 0.35f);
        private static readonly Color ColSpriteFade = new(0.90f, 0.35f, 0.65f);
        private static readonly Color ColEffects = new(0.95f, 0.40f, 0.30f);
        private static readonly Color ColEvents = new(0.35f, 0.80f, 0.85f);

        private SerializedProperty _black, _black2, _blackAlpha;
        private SerializedProperty _escapeButton, _okButton;
        private SerializedProperty _showHideDialogTime, _fadeBlackTime;
        private SerializedProperty _focusOnShow;
        private SerializedProperty _useFadeSpriteRenderers;
        private SerializedProperty _showEffect, _moveDirection, _startScale;
        private SerializedProperty _afterShow, _afterHide;

        private bool _showEvents = true;

        private void OnEnable()
        {
            _black = serializedObject.FindProperty("Black");
            _black2 = serializedObject.FindProperty("Black2");
            _blackAlpha = serializedObject.FindProperty("BlackAlpha");
            _escapeButton = serializedObject.FindProperty("EscapeButton");
            _okButton = serializedObject.FindProperty("OkButton");
            _showHideDialogTime = serializedObject.FindProperty("ShowHideDialogTime");
            _fadeBlackTime = serializedObject.FindProperty("FadeBlackTime");
            _focusOnShow = serializedObject.FindProperty("FocusOnShow");
            _useFadeSpriteRenderers = serializedObject.FindProperty("UseFadeSpriteRenderers");
            _showEffect = serializedObject.FindProperty("ShowEffect");
            _moveDirection = serializedObject.FindProperty("MoveDirection");
            _startScale = serializedObject.FindProperty("StartScale");
            _afterShow = serializedObject.FindProperty("AfterShow");
            _afterHide = serializedObject.FindProperty("AfterHide");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawScriptField();
            EditorGUILayout.Space(6);

            DrawGroup("Overlay", "🎭", ColOverlay, () =>
            {
                EditorGUILayout.PropertyField(_black, new GUIContent("Black", "Primary backdrop image faded in behind the dialog."));
                EditorGUILayout.PropertyField(_black2, new GUIContent("Black 2", "Optional secondary backdrop, faded in sync with Black."));
                EditorGUILayout.Slider(_blackAlpha, 0f, 1f, new GUIContent("Black Alpha", "Target opacity of the backdrop overlay while the dialog is open."));
            });

            DrawGroup("Modal Buttons", "🔘", ColButtons, () =>
            {
                EditorGUILayout.PropertyField(_escapeButton, new GUIContent("Escape Button", "Triggered when Escape is pressed while this dialog is the top of the modal stack."));
                EditorGUILayout.PropertyField(_okButton, new GUIContent("Ok Button", "Triggered when Return/OK is pressed while this dialog is the top of the modal stack."));

                if (_escapeButton.objectReferenceValue == null && _okButton.objectReferenceValue == null)
                    DrawHintBox("No buttons wired — Escape/Return routing to this dialog will be skipped by ModalDialogStack.");
            });

            DrawGroup("Timing", "⏱️", ColTiming, () =>
            {
                EditorGUILayout.PropertyField(_showHideDialogTime, new GUIContent("Show/Hide Time", "Duration of the show/hide transition itself (fade, move, or scale)."));
                EditorGUILayout.PropertyField(_fadeBlackTime, new GUIContent("Fade Black Time", "Duration of the backdrop overlay fade in/out."));

                if (_showHideDialogTime.floatValue < 0f) _showHideDialogTime.floatValue = 0f;
                if (_fadeBlackTime.floatValue < 0f) _fadeBlackTime.floatValue = 0f;

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reset to Default (0.15s / 0.2s)", GUILayout.Width(220), GUILayout.Height(18)))
                {
                    _showHideDialogTime.floatValue = 0.15f;
                    _fadeBlackTime.floatValue = 0.2f;
                }
                EditorGUILayout.EndHorizontal();
            });

            DrawGroup("Focus", "🎯", ColFocus, () =>
            {
                EditorGUILayout.PropertyField(_focusOnShow, new GUIContent("Focus On Show", "Selectable that receives UI focus/selection as soon as the dialog is shown (controller/keyboard navigation)."));
            });

            DrawGroup("Sprite Fade", "✨", ColSpriteFade, () =>
            {
                EditorGUILayout.PropertyField(_useFadeSpriteRenderers, new GUIContent("Use Fade Sprite Renderers", "Also fades child SpriteRenderers and TMP_Text alongside the CanvasGroup, useful for world-space or mixed-content dialogs."));
            });

            DrawGroup("Extended Effects", "🎬", ColEffects, () =>
            {
                EditorGUILayout.PropertyField(_showEffect, new GUIContent("Show Effect", "Transition style played when the dialog is shown/hidden."));

                var effect = (ShowDialogEffect)_showEffect.enumValueIndex;
                bool usesMove = effect == ShowDialogEffect.Move || effect == ShowDialogEffect.FadeAndMove;
                bool usesScale = effect == ShowDialogEffect.Scale || effect == ShowDialogEffect.FadeAndScale;

                EditorGUI.BeginDisabledGroup(!usesMove);
                EditorGUILayout.PropertyField(_moveDirection, new GUIContent("Move Direction", "Direction the dialog slides in from (only used by Move / Fade & Move)."));
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(!usesScale);
                EditorGUILayout.Slider(_startScale, 0f, 1f, new GUIContent("Start Scale", "Initial scale the dialog pops in from (only used by Scale / Fade & Scale)."));
                EditorGUI.EndDisabledGroup();

                DrawEffectPreviewBadge(effect);
            });

            EditorGUILayout.Space(6);
            DrawFoldoutGroup("Runtime Events", "⚡", ColEvents, ref _showEvents, () =>
            {
                EditorGUILayout.PropertyField(_afterShow, new GUIContent("After Show", "Invoked after the show animation completes."));
                EditorGUILayout.PropertyField(_afterHide, new GUIContent("After Hide", "Invoked after the hide animation completes."));
            });

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            DrawTestControls();
        }

        // ── Layout helpers ──────────────────────────────────────────────

        private void DrawScriptField()
        {
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MonoBehaviour)target), typeof(MonoScript), false);
            GUI.enabled = true;
        }

        private void DrawGroup(string title, string icon, Color accent, System.Action drawFields)
        {
            GUILayout.Label($"{icon}  {title.ToUpper()}", EditorStyles.boldLabel);
            var r = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), ColCard);
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, 3, r.height + 4), accent);
            GUILayout.Space(5);

            EditorGUI.indentLevel++;
            drawFields();
            EditorGUI.indentLevel--;

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);
        }

        private void DrawFoldoutGroup(string title, string icon, Color accent, ref bool expanded, System.Action drawFields)
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = accent } };
            expanded = EditorGUILayout.Foldout(expanded, $"{icon}  {title.ToUpper()}", true, headerStyle);
            if (!expanded) return;

            var r = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), ColCard);
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, 3, r.height + 4), accent);
            GUILayout.Space(5);

            drawFields();

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);
        }

        private void DrawEffectPreviewBadge(ShowDialogEffect effect)
        {
            string desc = effect switch
            {
                ShowDialogEffect.Fade => "Classic alpha fade in/out.",
                ShowDialogEffect.Move => "Slides in/out from the chosen edge.",
                ShowDialogEffect.Scale => "Pops in/out from Start Scale.",
                ShowDialogEffect.FadeAndScale => "Combines alpha fade with a scale pop.",
                ShowDialogEffect.FadeAndMove => "Combines alpha fade with a slide.",
                _ => ""
            };

            EditorGUILayout.Space(2);
            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColDim }, wordWrap = true, fontStyle = FontStyle.Italic };
            EditorGUILayout.LabelField($"ℹ  {desc}", style);
        }

        private void DrawHintBox(string msg)
        {
            EditorGUILayout.Space(2);
            var r = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), ColBg);
            GUILayout.Space(3);
            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColDim }, wordWrap = true };
            EditorGUILayout.LabelField($"💡  {msg}", style);
            GUILayout.Space(3);
            EditorGUILayout.EndVertical();
        }

        private void DrawTestControls()
        {
            var dialog = (Dialog)target;

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = ColGreen;
            if (GUILayout.Button("▶  Show", GUILayout.Height(24)))
                dialog.Show();

            GUI.backgroundColor = ColRed;
            if (GUILayout.Button("■  Hide", GUILayout.Height(24)))
                dialog.Hide();

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            if (!Application.isPlaying)
            {
                var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true };
                EditorGUILayout.LabelField("Enter Play Mode to test Show/Hide directly from the Inspector.", style);
            }
        }
    }
}
