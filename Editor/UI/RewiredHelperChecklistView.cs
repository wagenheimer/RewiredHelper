using System.Linq;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>Persistent manual release checklist (items that cannot be verified from source). Mirrors REWIRED-CHECKLIST.md.</summary>
    internal sealed class RewiredHelperChecklistView
    {
        private const string PrefKeyPrefix = "rewiredhelper_chk_";

        private static readonly (string Category, string Id, string Label, string Description)[] Items =
        {
            ("Setup", "setup_persistent", "Manager lives in a persistent scene", "One RewiredInputManager for the whole session; duplicates destroy themselves but hide setup mistakes."),
            ("Setup", "setup_actions", "Rewired actions exist: MenuButton, BackButton, UISubmit, MouseLeftButton", "Escape routing, submit bridge and tap-to-resume read these action names."),
            ("Setup", "setup_configure", "SetGlobalConfiguration called before first use", "Wire IUiBlocker / IModalStackProvider / IControllerHelpGate / IPauseGate from your bootstrap."),
            ("Mobile", "mob_bg", "Backgrounding the app shows no 'GAME PAUSED' overlay", "PauseOnAppBackground = Auto (Silent on mobile). Test: home button, notification shade, app switcher."),
            ("Mobile", "mob_timescale", "Time.timeScale is correct after ads, IAP sheets and the notification shade", "The manager restores the scale it found and never overwrites a value the game set while paused."),
            ("Mobile", "mob_sysui", "Ads / IAP / share sheets wrapped in RewiredInputManager.BeginSystemUi()", "Only needed if the game reacts to OnPauseChanged or forces Overlay mode."),
            ("Mobile", "mob_touch", "Touch works with no controller attached; cursor hidden on touch", "IsUsingTouch true, GameCursor disabled."),
            ("Mobile", "mob_btpad", "Connecting/disconnecting a Bluetooth pad does not pause the game", "PauseOnControllerDisconnect = Auto (off on Android/iOS)."),
            ("Mobile", "mob_back", "Android Back button closes the top modal / opens the pause menu", "Back routes to the modal stack, then EscapeButton, then ReturnEscapeEvent."),
            ("Mobile", "mob_tv", "Android TV / remote: AndroidRemote glyphs and D-pad cursor work", "Run Ensure Android Remote Glyphs; test with a real remote."),
            ("Desktop / Console", "pc_steam", "Steam overlay pauses and resumes; SteamIsInitialized assigned", "Requires the Steamworks.NET package."),
            ("Desktop / Console", "pc_disconnect", "Unplugging the active pad pauses; replugging resumes", "Consoles wait a 2 s grace period before pausing."),
            ("Desktop / Console", "pc_cursor", "Custom cursor texture is Read/Write, uncompressed, no mipmaps", "Otherwise the OS cannot read the pixels."),
            ("UI / Modal", "ui_stack", "Escape/Return hit the top-most Dialog", "DefaultModalStackProvider passed to Configure()."),
            ("UI / Modal", "ui_visibility", "InputVisibilityController shows the right UI per input type", "Touch, mouse/keyboard and joystick variants."),
            ("UI / Modal", "ui_help", "Controller Help form appears once, not in ControllerHelpBlockedScenes", "Test with a real gamepad in an allowed scene."),
            ("Release", "rel_logs", "No input-switch log spam in the release build", "Logs in per-switch paths only in editor/development builds."),
            ("Release", "rel_version", "Package version bumped and CHANGELOG generated", "Automated by bump-version.yml from Conventional Commits.")
        };

        public VisualElement Root { get; }

        private Label _progressLabel;

        public RewiredHelperChecklistView()
        {
            Root = new VisualElement();
            RewiredHelperUIStyle.Apply(Root);
            BuildUI();
        }

        private void BuildUI()
        {
            var headerCard = RewiredHelperUIStyle.CreateCard("📋 Release Review Checklist",
                "Manual checks that cannot be verified from source. Saved per machine.");

            var topRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center, marginBottom = 8 } };
            _progressLabel = new Label();
            _progressLabel.style.fontSize = 11;
            _progressLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _progressLabel.style.color = new Color(0.35f, 0.85f, 0.45f);
            topRow.Add(_progressLabel);
            topRow.Add(RewiredHelperUIStyle.CreateButton("↺ Reset All", ResetChecklist));
            headerCard.Add(topRow);
            Root.Add(headerCard);

            foreach (var group in Items.GroupBy(i => i.Category))
            {
                var card = RewiredHelperUIStyle.CreateCard(group.Key);
                foreach (var item in group)
                    card.Add(CreateItem(item.Id, item.Label, item.Description));
                Root.Add(card);
            }

            UpdateProgress();
        }

        private VisualElement CreateItem(string id, string label, string description)
        {
            var element = new VisualElement();
            element.AddToClassList("rh-checklist-item");

            bool isChecked = EditorPrefs.GetBool(PrefKeyPrefix + id, false);
            if (isChecked) element.AddToClassList("checked");

            var toggle = new Toggle { value = isChecked };
            toggle.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(PrefKeyPrefix + id, evt.newValue);
                element.EnableInClassList("checked", evt.newValue);
                UpdateProgress();
            });
            element.Add(toggle);

            var textCol = new VisualElement();
            textCol.AddToClassList("rh-checklist-text");

            var title = new Label(label);
            title.AddToClassList("rh-checklist-title");
            textCol.Add(title);

            var desc = new Label(description);
            desc.AddToClassList("rh-checklist-desc");
            desc.style.whiteSpace = WhiteSpace.Normal;
            textCol.Add(desc);

            element.Add(textCol);
            return element;
        }

        private void UpdateProgress()
        {
            int total = Items.Length;
            int done = Items.Count(i => EditorPrefs.GetBool(PrefKeyPrefix + i.Id, false));
            _progressLabel.text = $"Progress: {done} / {total} verified ({done * 100 / total}%)";
        }

        private void ResetChecklist()
        {
            if (!EditorUtility.DisplayDialog("Reset Checklist", "Reset all checklist items to unchecked?", "RESET", "CANCEL"))
                return;

            foreach (var item in Items)
                EditorPrefs.DeleteKey(PrefKeyPrefix + item.Id);

            Root.Clear();
            BuildUI();
        }
    }
}
