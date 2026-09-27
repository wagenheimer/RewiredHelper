using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.RewiredHelper.Editor
{
    internal sealed class RewiredHelperDocsView
    {
        private const string ReadmeUrl = "https://github.com/wagenheimer/RewiredHelper/blob/main/README.md";
        private const string IssuesUrl = "https://github.com/wagenheimer/RewiredHelper/issues/new";

        public VisualElement Root { get; }

        public RewiredHelperDocsView()
        {
            Root = new VisualElement();
            RewiredHelperUIStyle.Apply(Root);
            BuildUI();
        }

        private void BuildUI()
        {
            var apiCard = RewiredHelperUIStyle.CreateCard("📚 Runtime API", "Everything is reachable from the RewiredInputManager singleton.");
            apiCard.Add(RewiredHelperUIStyle.CreateCallout(
                "• RewiredInputManager.IsUsingTouch / CurrentControllerType: active input source.\n" +
                "• OnInputTypeChanged / OnInputSpecializationChanged: static events when the source changes.\n" +
                "• OnPauseChanged(isPaused, isFrozen): static event for every pause transition (silent pauses have isFrozen = false).\n" +
                "• RequestPause() / Resume(): manual pause and the Resume button hook. IsPaused / IsFrozen: current state.\n" +
                "• CustomCursorEnabled: never call Cursor.SetCursor directly."));
            Root.Add(apiCard);

            var codeCard = RewiredHelperUIStyle.CreateCard("💻 Bootstrap", "Call once from your persistent bootstrap, before relying on the hooks.");
            codeCard.Add(CreateCodeBox(
                "RewiredInputManager.SetGlobalConfiguration(\n" +
                "    uiBlocker: myBlocker,                       // suppress Escape/Return during cutscenes\n" +
                "    modalStack: new DefaultModalStackProvider(),  // route Escape/Return to the top Dialog\n" +
                "    controllerHelpGate: myGate,                  // when the first-time help may appear\n" +
                "    pauseGate: myPauseGate);                     // veto pauses during ads/IAP/loading\n\n" +
                "RewiredInputManager.OnPauseChanged += (paused, frozen) => myMusic.SetPaused(paused);"));
            Root.Add(codeCard);

            var linksCard = RewiredHelperUIStyle.CreateCard("🔄 Package Maintenance & Links");
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            row.Add(RewiredHelperUIStyle.CreateButton("Check for Package Updates", () => UpdateChecker.CheckForUpdate(force: true), primary: true));
            row.Add(RewiredHelperUIStyle.CreateButton("📖 Integration Guide", () => Application.OpenURL(ReadmeUrl)));
            row.Add(RewiredHelperUIStyle.CreateButton("🐞 Report Issue", () => Application.OpenURL(IssuesUrl)));
            row.Add(RewiredHelperUIStyle.CreateButton("📝 Create Controller Help Form", DefaultSetupGenerator.CreateControllerHelpForm));
            linksCard.Add(row);
            Root.Add(linksCard);
        }

        private static VisualElement CreateCodeBox(string code)
        {
            var box = new VisualElement();
            box.AddToClassList("rh-code-box");

            var label = new Label(code);
            label.AddToClassList("rh-code-text");
            box.Add(label);
            return box;
        }
    }
}
