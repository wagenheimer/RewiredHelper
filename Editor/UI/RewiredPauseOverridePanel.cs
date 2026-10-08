using System;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// The "Advanced (rarely needed)" pause controls shared by the RewiredInputManager Inspector and the Dashboard:
    /// the explicit override of the automatic policy, the Steam overlay toggle, the pause-screen reference and the
    /// "Create / complete Pause Screen" action. The host binds <see cref="Root"/> to the same SerializedObject.
    /// </summary>
    internal sealed class RewiredPauseOverridePanel
    {
        private static readonly string[] OverrideFields =
        {
            "PauseOnAppBackground", "PauseOnControllerDisconnect", "ResumeOnAnyInput"
        };

        private readonly RewiredInputManager _manager;
        private readonly VisualElement _overrideBox = new VisualElement();
        private readonly Button _pauseScreenButton;

        public VisualElement Root { get; } = new VisualElement();

        /// <param name="onOverrideChanged">Called after the override toggle changes, so the host can refresh the policy rows.</param>
        public RewiredPauseOverridePanel(SerializedObject serializedObject, RewiredInputManager manager, Action onOverrideChanged = null)
        {
            _manager = manager;

            Root.Add(new Label("Leave these alone unless you need behavior that differs from the automatic policy.")
            {
                style = { fontSize = 10, whiteSpace = WhiteSpace.Normal, marginBottom = 4 }
            });

            var overrideField = new PropertyField(serializedObject.FindProperty("OverridePlatformDefaults"));
            var lastOverride = manager != null && manager.OverridePlatformDefaults;
            overrideField.RegisterValueChangeCallback(_ =>
            {
                // The callback also fires once on bind; only notify the host when the value really changed
                // (the Dashboard rebuilds itself from this, which would otherwise loop).
                var current = _manager != null && _manager.OverridePlatformDefaults;
                Refresh();
                if (current == lastOverride) return;

                lastOverride = current;
                onOverrideChanged?.Invoke();
            });
            Root.Add(overrideField);

            foreach (var name in OverrideFields)
                _overrideBox.Add(new PropertyField(serializedObject.FindProperty(name)));
            Root.Add(_overrideBox);

            Root.Add(new PropertyField(serializedObject.FindProperty("PauseOnSteamOverlay")));
            Root.Add(new PropertyField(serializedObject.FindProperty("GamePaused")));

            _pauseScreenButton = RewiredHelperUIStyle.CreateButton("🛠 Create / complete Pause Screen",
                () => DefaultSetupGenerator.CreatePauseScreenAndWire(_manager, new SerializedObject(_manager)));
            _pauseScreenButton.style.marginLeft = 0;
            _pauseScreenButton.style.marginTop = 4;
            _pauseScreenButton.style.alignSelf = Align.FlexStart;
            _pauseScreenButton.tooltip = "Creates a pause screen with a Resume button, or adds the Resume button to the one assigned to Game Paused.";
            Root.Add(_pauseScreenButton);

            Refresh();
        }

        /// <summary>Only offered while there is no pause screen with a Resume button.</summary>
        public static bool NeedsPauseScreenFix(RewiredInputManager manager) =>
            RewiredHelperAudit.CanFreezeOnSomePlatform(manager) && (manager.GamePaused == null || !RewiredHelperAudit.HasResumeButton(manager));

        public void Refresh()
        {
            if (_manager == null) return;

            _overrideBox.style.display = _manager.OverridePlatformDefaults ? DisplayStyle.Flex : DisplayStyle.None;
            _pauseScreenButton.style.display = NeedsPauseScreenFix(_manager) ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
