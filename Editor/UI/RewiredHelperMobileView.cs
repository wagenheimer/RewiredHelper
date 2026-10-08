using System.Linq;

using Rewired;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// The "Automatic" tab. Leads with what the package decides by itself per platform; the only manual controls
    /// (an explicit override and the pause-screen reference) live in a collapsed Advanced section.
    /// </summary>
    internal sealed class RewiredHelperMobileView
    {
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
            Root.Add(BuildOnScreenKeyboardCard(manager));
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

        #region On-Screen Keyboard

        /// <summary>Same panel as the RewiredInputManager Inspector, so gamepad text input is visible from the Dashboard too.</summary>
        private VisualElement BuildOnScreenKeyboardCard(RewiredInputManager manager)
        {
            var card = RewiredHelperUIStyle.CreateCard("⌨ On-Screen Keyboard",
                "Gamepad / Steam Deck text input. Shown automatically for any TMP_InputField selected with a joystick; touch uses the OS keyboard.");

            if (manager != null)
            {
                var so = new SerializedObject(manager);
                foreach (var field in new[] { "ShowOnScreenKeyboardOnGamepadTextInput", "OnScreenKeyboardOnlyOnSteamDeck" })
                {
                    var toggle = new PropertyField(so.FindProperty(field));
                    toggle.Bind(so);
                    card.Add(toggle);
                }
            }

            card.Add(new RewiredOnScreenKeyboardPanel(Rebuild).Root);
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
            section.Add(new RewiredPauseOverridePanel(so, manager, () => section.schedule.Execute(Rebuild).ExecuteLater(200)).Root);

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
