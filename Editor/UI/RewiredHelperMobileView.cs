using System.Linq;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine.UIElements;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>Edits the pause policy of the scene's RewiredInputManager and explains the mobile best practices.</summary>
    internal sealed class RewiredHelperMobileView
    {
        private static readonly string[] PauseFields =
        {
            "PauseOnAppBackground", "PauseOnControllerDisconnect", "ResumeOnAnyInput", "PauseOnSteamOverlay", "GamePaused"
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
            foreach (var field in PauseFields)
                card.Add(new PropertyField(so.FindProperty(field)));
            card.Bind(so);

            var presetRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8 } };
            presetRow.Add(RewiredHelperUIStyle.CreateButton("📱 Apply Auto Preset (mobile-safe)", () =>
            {
                RewiredHelperAudit.ApplyAutoPreset(manager);
                so.Update();
            }, primary: true));
            presetRow.Add(RewiredHelperUIStyle.CreateButton("🛠 Generate Pause Screen + Resume button",
                () => DefaultSetupGenerator.CreatePauseScreenAndWire(manager, so)));
            card.Add(presetRow);

            return card;
        }

        private static VisualElement BuildBestPracticesCard()
        {
            var card = RewiredHelperUIStyle.CreateCard("✅ Mobile best practices");
            card.Add(RewiredHelperUIStyle.CreateCallout(
                "• Keep everything on Auto: Silent on Android/iOS, Overlay on desktop/console, resolved when the game runs (a scene saved under one build target stays correct in the other).\n" +
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
