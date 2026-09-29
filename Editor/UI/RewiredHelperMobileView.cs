using System.Linq;

using Rewired;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine.UIElements;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// The "Automatic" tab. Leads with what the package decides by itself per platform; the only manual controls
    /// (an explicit override and the pause-screen reference) live in a collapsed Advanced section.
    /// </summary>
    internal sealed class RewiredHelperMobileView
    {
        private static readonly string[] OverrideFields =
        {
            "PauseOnAppBackground", "PauseOnControllerDisconnect", "ResumeOnAnyInput"
        };

        public VisualElement Root { get; }

        public RewiredHelperMobileView()
        {
            Root = new VisualElement();
            RewiredHelperUIStyle.Apply(Root);
            BuildUI();
        }

        private void Rebuild()
        {
            Root.Clear();
            BuildUI();
        }

        private void BuildUI()
        {
            Root.Add(BuildAutomaticPolicyCard());

            var manager = RewiredHelperAudit.FindAll<RewiredInputManager>().FirstOrDefault();
            Root.Add(BuildSceneStatusCard(manager));
            if (manager != null)
                BuildAdvancedSection(manager);

            BuildHowItWorksSection();
        }

        #region Automatic policy

        private static VisualElement BuildAutomaticPolicyCard()
        {
            var card = RewiredHelperUIStyle.CreateCard("⚙️ Everything here is automatic",
                "The policy is chosen from the platform of each build (Unity Build Pipeline, Build Settings or CLI) and the device it runs on. Nothing to configure.");

            bool activeIsMobile = RewiredHelperAudit.IsMobileTarget;
            var mobile = PausePolicy.ForPlatform(true);
            var desktop = PausePolicy.ForPlatform(false);

            card.Add(CreatePolicyRow("", activeIsMobile ? "Android / iOS  ●" : "Android / iOS",
                activeIsMobile ? "Desktop / Console / TV" : "Desktop / Console / TV  ●", header: true));
            card.Add(CreatePolicyRow("App goes to background", Describe(mobile.AppBackground), Describe(desktop.AppBackground)));
            card.Add(CreatePolicyRow("Pause on controller disconnect", OnOff(mobile.PauseOnControllerDisconnect), OnOff(desktop.PauseOnControllerDisconnect)));
            card.Add(CreatePolicyRow("Resume on any input", OnOff(mobile.ResumeOnAnyInput), OnOff(desktop.ResumeOnAnyInput)));
            card.Add(CreatePolicyRow("Steam overlay", "n/a", "pauses, resumes on close (auto-detected)"));
            card.Add(CreatePolicyRow("Android TV / Fire TV", "-", "desktop policy, detected at runtime"));
            card.Add(CreatePolicyRow("Pause screen", "not shown (silent)", "yours: required, the audit fails without it"));
            card.Add(CreatePolicyRow("Steam Deck suspend/resume", "n/a", "detected automatically, pauses/resumes like app-background"));
            card.Add(CreatePolicyRow("Gamepad text input", "native OS keyboard (touch)", "RewiredOnScreenKeyboard, shown automatically"));

            card.Add(RewiredHelperUIStyle.CreateCallout(
                $"● Active build target: {EditorUserBuildSettings.activeBuildTarget}. Every build logs the policy it uses and warns about a manual override that would break it on mobile.",
                AuditSeverity.Info));
            return card;
        }

        private static string Describe(AppBackgroundPauseMode mode) => mode == AppBackgroundPauseMode.Silent
            ? "silent (no overlay, time untouched)"
            : "freeze + pause overlay";

        private static string OnOff(bool value) => value ? "on" : "off";

        private static VisualElement CreatePolicyRow(string setting, string mobile, string desktop, bool header = false)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingTop = 3, paddingBottom = 3 } };
            foreach (var (text, grow) in new[] { (setting, 2f), (mobile, 1.4f), (desktop, 1.4f) })
            {
                var label = new Label(text) { style = { flexGrow = grow, flexBasis = 0, whiteSpace = WhiteSpace.Normal } };
                if (header) label.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                row.Add(label);
            }
            return row;
        }

        #endregion

        #region Scene status

        private VisualElement BuildSceneStatusCard(RewiredInputManager manager)
        {
            var card = RewiredHelperUIStyle.CreateCard("✅ This project", "Read-only status of the open scene. Nothing to press unless a row says so.");

            if (manager == null)
            {
                card.Add(RewiredHelperUIStyle.CreateCallout(
                    "No RewiredInputManager in the open scene, so there is nothing to check here. Add one from the Setup Audit tab.",
                    AuditSeverity.Warning));
                return card;
            }

            var results = new System.Collections.Generic.List<AuditResult>();
            RewiredHelperAudit.AuditMobilePause(results, manager);

            var list = new VisualElement();
            RewiredInspectorWidgets.FillChecks(list, results, Rebuild);
            card.Add(list);
            return card;
        }

        #endregion

        #region Advanced

        /// <summary>The only manual controls: an explicit override of the automatic policy and the pause-screen reference.</summary>
        private void BuildAdvancedSection(RewiredInputManager manager)
        {
            var section = RewiredInspectorWidgets.CreateSection(Root, "dashboard.advanced", "🔧 Advanced (rarely needed)", false,
                "Leave everything here alone unless you need behavior that differs from the automatic policy.");

            var so = new SerializedObject(manager);
            var overrideField = new PropertyField(so.FindProperty("OverridePlatformDefaults"));
            section.Add(overrideField);

            var overrideBox = new VisualElement();
            foreach (var field in OverrideFields)
                overrideBox.Add(new PropertyField(so.FindProperty(field)));
            section.Add(overrideBox);
            overrideBox.style.display = manager.OverridePlatformDefaults ? DisplayStyle.Flex : DisplayStyle.None;

            section.Add(new PropertyField(so.FindProperty("PauseOnSteamOverlay")));
            section.Add(new PropertyField(so.FindProperty("GamePaused")));

            overrideField.RegisterValueChangeCallback(_ =>
            {
                overrideBox.style.display = so.FindProperty("OverridePlatformDefaults").boolValue ? DisplayStyle.Flex : DisplayStyle.None;
                section.schedule.Execute(Rebuild).ExecuteLater(200);
            });

            if (EditorApplication.isPlaying)
            {
                var simRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, alignItems = Align.Center, marginTop = 10 } };
                var current = RewiredInputManager.DebugForceControllerType;
                simRow.Add(new Label(current.HasValue ? $"🧪 Simulating: {current.Value}" : "🧪 Simulate text-input device:")
                    { style = { marginRight = 6, unityTextAlign = TextAnchor.MiddleLeft } });
                simRow.Add(RewiredHelperUIStyle.CreateButton("Gamepad (shows on-screen keyboard)",
                    () => { RewiredInputManager.DebugForceControllerType = ControllerType.Joystick; Rebuild(); }));
                simRow.Add(RewiredHelperUIStyle.CreateButton("Touch",
                    () => { RewiredInputManager.DebugForceControllerType = ControllerType.Custom; Rebuild(); }));
                simRow.Add(RewiredHelperUIStyle.CreateButton("Stop simulating (auto-detect)",
                    () => { RewiredInputManager.DebugForceControllerType = null; Rebuild(); }));
                section.Add(simRow);
                section.Add(RewiredHelperUIStyle.CreateCallout(
                    "Forces CurrentControllerType so you can preview gamepad-only behavior — like the RewiredOnScreenKeyboard popping up when a TMP_InputField is selected — without a physical controller plugged in. " +
                    "Only affects this Editor session while in Play Mode; it is compiled out of builds entirely and resets when you stop Play Mode.",
                    AuditSeverity.Info));
            }

            if (RewiredHelperAudit.CanFreezeOnSomePlatform(manager) && (manager.GamePaused == null || !RewiredHelperAudit.HasResumeButton(manager)))
            {
                var create = RewiredHelperUIStyle.CreateButton("🛠 Create / complete Pause Screen",
                    () => DefaultSetupGenerator.CreatePauseScreenAndWire(manager, new SerializedObject(manager)));
                create.style.marginLeft = 0;
                create.style.marginTop = 6;
                create.style.alignSelf = Align.FlexStart;
                create.tooltip = "Creates a pause screen with a Resume button, or adds the Resume button to the one assigned to Game Paused.";
                section.Add(create);
            }

            section.Bind(so);
        }

        #endregion

        #region How it works

        private void BuildHowItWorksSection()
        {
            var section = RewiredInspectorWidgets.CreateSection(Root, "dashboard.how", "📖 How it works", false);
            section.Add(RewiredHelperUIStyle.CreateCallout(
                "• Mobile: the OS already suspends the app, and notification shade / ads / IAP sheets also fire OnApplicationPause, so pausing with an overlay would pop up constantly. The app-background pause is silent instead.\n" +
                "• Bluetooth pads and remotes connect and disconnect often, so a controller disconnect does not pause a touch game.\n" +
                "• 'Tap anywhere to resume' leaks taps into gameplay, so it is off on mobile; use a Resume button wired to RewiredInputManager.Resume().\n" +
                "• The manager only touches Time.timeScale while it froze the game, restores the value it found, and never overwrites a value your game set meanwhile.\n" +
                "• Ads / IAP / share sheets: using (RewiredInputManager.BeginSystemUi()) { ... } vetoes automatic pauses (only needed for Overlay mode or if you react to OnPauseChanged). Custom rules: pass an IPauseGate to SetGlobalConfiguration(...).",
                AuditSeverity.Info));
        }

        #endregion
    }
}
