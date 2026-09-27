using System.Linq;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine.UIElements;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>Edits the pause policy of the scene's RewiredInputManager and explains the mobile best practices.</summary>
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

        private void BuildUI()
        {
            Root.Add(BuildAutomaticPolicyCard());
            Root.Add(BuildPolicyCard());
            Root.Add(BuildBestPracticesCard());
        }

        private VisualElement BuildPolicyCard()
        {
            var card = RewiredHelperUIStyle.CreateCard("📱 Pause Policy (open scene)",
                "What makes the game pause, and how the player gets out of it.");

            var manager = RewiredHelperAudit.FindAll<RewiredInputManager>().FirstOrDefault();
            if (manager == null)
            {
                card.Add(RewiredHelperUIStyle.CreateCallout("No RewiredInputManager in the open scene. Create it from the Setup Audit tab.", AuditSeverity.Warning));
                return card;
            }

            var so = new SerializedObject(manager);
            card.Add(new PropertyField(so.FindProperty("GamePaused")));
            card.Add(new PropertyField(so.FindProperty("PauseOnSteamOverlay")));

            var overrideField = new PropertyField(so.FindProperty("OverridePlatformDefaults"));
            card.Add(overrideField);

            var overrideBox = new VisualElement();
            foreach (var field in OverrideFields)
                overrideBox.Add(new PropertyField(so.FindProperty(field)));
            card.Add(overrideBox);

            overrideBox.style.display = manager.OverridePlatformDefaults ? DisplayStyle.Flex : DisplayStyle.None;
            overrideField.RegisterValueChangeCallback(_ =>
                overrideBox.style.display = so.FindProperty("OverridePlatformDefaults").boolValue ? DisplayStyle.Flex : DisplayStyle.None);

            card.Bind(so);

            if (!RewiredHelperAudit.HasResumeButton(manager))
            {
                var create = RewiredHelperUIStyle.CreateButton("🛠 Create Pause Screen (with Resume button)",
                    () => DefaultSetupGenerator.CreatePauseScreenAndWire(manager, so));
                create.style.marginLeft = 0;
                create.style.marginTop = 6;
                create.style.alignSelf = UnityEngine.UIElements.Align.FlexStart;
                card.Add(create);
            }

            return card;
        }

        private static VisualElement BuildAutomaticPolicyCard()
        {
            var card = RewiredHelperUIStyle.CreateCard("⚙️ Automatic policy per platform",
                "Chosen from the platform of each build (Unity Build Pipeline, Build Settings or CLI): no setup needed.");

            var mobile = PausePolicy.ForPlatform(true);
            var desktop = PausePolicy.ForPlatform(false);
            card.Add(CreatePolicyRow("Setting", "Android / iOS", "Desktop / Console", header: true));
            card.Add(CreatePolicyRow("App goes to background", mobile.AppBackground.ToString(), desktop.AppBackground.ToString()));
            card.Add(CreatePolicyRow("Pause on controller disconnect", OnOff(mobile.PauseOnControllerDisconnect), OnOff(desktop.PauseOnControllerDisconnect)));
            card.Add(CreatePolicyRow("Resume on any input", OnOff(mobile.ResumeOnAnyInput), OnOff(desktop.ResumeOnAnyInput)));
            card.Add(CreatePolicyRow("Steam overlay", "n/a", "pauses, resumes on close (auto-detected)"));
            card.Add(CreatePolicyRow("Android TV / Fire TV", "-", "uses the desktop / console policy (detected at runtime)"));

            var activeMobile = RewiredHelperAudit.IsMobileTarget;
            card.Add(RewiredHelperUIStyle.CreateCallout(
                $"Active build target: {UnityEditor.EditorUserBuildSettings.activeBuildTarget} -> {(activeMobile ? "mobile" : "desktop / console")} policy. " +
                "Every build also logs the policy it uses and warns about overrides that break it on mobile.",
                AuditSeverity.Info));
            return card;
        }

        private static string OnOff(bool value) => value ? "on" : "off";

        private static VisualElement CreatePolicyRow(string setting, string mobile, string desktop, bool header = false)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingTop = 3, paddingBottom = 3 } };
            foreach (var (text, grow) in new[] { (setting, 2f), (mobile, 1f), (desktop, 1.4f) })
            {
                var label = new Label(text) { style = { flexGrow = grow, flexBasis = 0, whiteSpace = WhiteSpace.Normal } };
                if (header) label.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                row.Add(label);
            }
            return row;
        }

        private static VisualElement BuildBestPracticesCard()
        {
            var card = RewiredHelperUIStyle.CreateCard("✅ Mobile best practices");
            card.Add(RewiredHelperUIStyle.CreateCallout(
                "• Leave Override Platform Defaults off: the right policy is applied per build platform automatically.\n" +
                "• App background: Silent. The OS already suspends the app; notification shade, ads and IAP sheets also fire OnApplicationPause, so an overlay pops up constantly.\n" +
                "• Never rely on the manager to own Time.timeScale: it restores the value it found and never overwrites a value your game set while paused.\n" +
                "• Controller disconnect: turn it off on touch platforms. Bluetooth pads and remotes come and go.\n" +
                "• Resume: use an explicit Resume button (RewiredInputManager.Instance.Resume) instead of 'tap anywhere', which leaks taps into gameplay.\n" +
                "• Ads / IAP / share sheets: wrap them in using (RewiredInputManager.BeginSystemUi()) { ... } (or call RewiredInputManager.SuppressPauseFor(seconds)) to veto automatic pauses; for custom rules pass an IPauseGate to SetGlobalConfiguration(...). Listen to RewiredInputManager.OnPauseChanged to pause your own music/gameplay.",
                AuditSeverity.Info));
            return card;
        }
    }
}
