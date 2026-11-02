using NUnit.Framework;

using Wagenheimer.RewiredHelper.Editor;

namespace Wagenheimer.RewiredHelper.Tests
{
    /// <summary>
    /// Pure rules behind the "legacy input a gamepad cannot reach" and "custom cursor option follows the
    /// input device" audit findings (the AssetDatabase walk itself is not unit-tested).
    /// </summary>
    public class ProjectInputScannerTests
    {
        [TestCase("if (Input.GetMouseButtonDown(0)) Skip();")]
        [TestCase("if (Input.GetMouseButtonUp(0) && !LoadingScreen.LoadingNow) Completed();")]
        [TestCase("if (Input.anyKey) Turbo = 5;")]
        [TestCase("if (Input.anyKeyDown) Turbo = 5;")]
        [TestCase("if (Input.GetKeyDown(KeyCode.F)) Toggle();")]
        [TestCase("if (Input.GetKey(KeyCode.LeftShift)) PowerUp();")]
        [TestCase("ray = Camera.main.ScreenPointToRay(Input.mousePosition);")]
        [TestCase("if (Input.GetAxis(\"Mouse X\") != 0) Show();")]
        [TestCase("if (Input.GetButtonDown(\"Confirm\")) Continue();")]
        public void MouseOrKeyboardOnlyReads_AreReported(string line)
        {
            Assert.IsTrue(ProjectInputScanner.IsLegacyInputLine(line, out string api));
            Assert.IsNotEmpty(api);
        }

        [TestCase("if (Input.GetKeyDown(KeyCode.Escape)) Close();")]
        [TestCase("if (Input.GetKeyDown(KeyCode.Return)) Confirm();")]
        [TestCase("if (Input.GetButtonDown(\"Submit\")) Confirm();")]
        [TestCase("if (Input.GetButtonDown(\"Cancel\")) Back();")]
        public void EscapeAndReturn_AreLeftToTheRawInputCheck(string line)
        {
            Assert.IsFalse(ProjectInputScanner.IsLegacyInputLine(line, out _));
        }

        [TestCase("// if (Input.GetMouseButtonDown(0)) Skip();")]
        [TestCase(" * Input.anyKey inside a doc comment")]
        [TestCase("if (Input.touchCount > 0) CheckTouch(0);")]
        [TestCase("var phase = Input.GetTouch(0).phase;")]
        [TestCase("Cursor.visible = false;")]
        [TestCase("")]
        public void CommentsTouchAndUnrelatedCode_AreIgnored(string line)
        {
            Assert.IsFalse(ProjectInputScanner.IsLegacyInputLine(line, out _));
        }

        [Test]
        public void NullLine_IsIgnored()
        {
            Assert.IsFalse(ProjectInputScanner.IsLegacyInputLine(null, out _));
        }

        [Test]
        public void OptionsScriptWithToggleAndNoDeviceAwareness_IsReported()
        {
            const string text =
                "public class interfaceOptions : MonoBehaviour {\n" +
                "    public Toggle cbCustomCursor;\n" +
                "    public void CustomCursor(bool value) { Main.main.SetCustomCursor(value); }\n" +
                "}";

            Assert.IsTrue(ProjectInputScanner.NeedsDeviceAwareCustomCursor(text));
        }

        [Test]
        public void OptionsScriptWithInputVisibilityController_IsAccepted()
        {
            const string text =
                "public class interfaceOptions : MonoBehaviour {\n" +
                "    public Toggle cbCustomCursor;\n" +
                "    void Awake() { cbCustomCursor.gameObject.AddComponent<InputVisibilityController>(); }\n" +
                "}";

            Assert.IsFalse(ProjectInputScanner.NeedsDeviceAwareCustomCursor(text));
        }

        [Test]
        public void OptionsScriptListeningToInputChanges_IsAccepted()
        {
            const string text =
                "public class interfaceOptions : MonoBehaviour {\n" +
                "    public Toggle cbCustomCursor;\n" +
                "    void OnEnable() { RewiredInputManager.OnInputSpecializationChanged += Refresh; }\n" +
                "}";

            Assert.IsFalse(ProjectInputScanner.NeedsDeviceAwareCustomCursor(text));
        }

        [Test]
        public void SaveDataWithoutUiControl_IsNotReported()
        {
            const string text =
                "public class BaseSaveData { public bool CustomCursor = true; }\n" +
                "void SetCustomCursor(bool value) { input.CustomCursorEnabled = value; }";

            Assert.IsFalse(ProjectInputScanner.NeedsDeviceAwareCustomCursor(text));
        }

        [Test]
        public void EditorFolders_AreAlwaysIgnored()
        {
            Assert.IsTrue(ProjectInputScanner.IsIgnoredPath("Assets/ThirdParty/Editor/Tool.cs", null));
        }

        [Test]
        public void ProjectIgnorePrefixes_AreCaseInsensitiveAndDirectoryBound()
        {
            var prefixes = new[] { "Assets/EasyTouchBundle/" };

            Assert.IsTrue(ProjectInputScanner.IsIgnoredPath("Assets/EasyTouchBundle/Plugins/ETCJoystick.cs", prefixes));
            Assert.IsFalse(ProjectInputScanner.IsIgnoredPath("Assets/EasyTouchBundleExtra/Foo.cs", prefixes));
        }

        [Test]
        public void GameScriptsAreNotIgnored()
        {
            Assert.IsFalse(ProjectInputScanner.IsIgnoredPath("Assets/_Game/Scripts/Board.cs", new[] { "Assets/EasyTouchBundle/" }));
            Assert.IsFalse(ProjectInputScanner.IsIgnoredPath(null, null));
        }
    }
}
