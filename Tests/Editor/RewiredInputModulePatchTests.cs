using System.Text.RegularExpressions;

using NUnit.Framework;

using Wagenheimer.RewiredHelper.Editor;

namespace Wagenheimer.RewiredHelper.Tests
{
    public class RewiredInputModulePatchTests
    {
        private const string PointerModule =
            "            bool ITouchInputSource.touchSupported {\n" +
            "                get {\n" +
            "                    TryUpdate();\n" +
            "#if UNITY_5_PLUS\n" +
            "                    return Rewired.ReInput.touch.touchSupported;\n" +
            "#else\n" +
            "                    return false;\n" +
            "#endif\n" +
            "                }\n" +
            "            }\n" +
            "\n" +
            "            int ITouchInputSource.touchCount {\n" +
            "                get {\n" +
            "                    TryUpdate();\n" +
            "                    return Rewired.ReInput.touch.touchCount;\n" +
            "                }\n" +
            "            }\n" +
            "\n" +
            "            Rewired.UnityTouch ITouchInputSource.GetTouch(int index) {\n" +
            "                TryUpdate();\n" +
            "                return Rewired.ReInput.touch.GetTouch(index);\n" +
            "            }\n";

        private const string StandaloneModule =
            "        [NonSerialized]\n" +
            "        private bool isTouchSupported;\n" +
            "\n" +
            "        protected override void Awake() {\n" +
            "            base.Awake();\n" +
            "\n" +
            "            isTouchSupported = defaultTouchInputSource.touchSupported;\n" +
            "        }\n" +
            "\n" +
            "        public override void UpdateModule() {\n" +
            "            CheckEditorRecompile();\n" +
            "            if (recompiling) return;\n" +
            "            if (!Rewired.ReInput.isReady) return;\n" +
            "\n" +
            "            if (!m_HasFocus && ShouldIgnoreEventsOnNoFocus()) return;\n" +
            "        }\n" +
            "\n" +
            "        private void InitializeRewired() {\n" +
            "            if (!Rewired.ReInput.isReady) {\n" +
            "                Debug.LogError(\"Rewired is not initialized! Are you missing a Rewired Input Manager in your scene?\");\n" +
            "                return;\n" +
            "            }\n" +
            "            Rewired.ReInput.ShutDownEvent -= OnRewiredShutDown;\n" +
            "            SetupRewiredVars();\n" +
            "        }\n" +
            "\n" +
            "        private void OnRewiredInitialized() {\n" +
            "            InitializeRewired();\n" +
            "        }\n" +
            "\n" +
            "        private void OnRewiredShutDown() {\n" +
            "            ClearRewiredVars();\n" +
            "        }\n";

        [Test]
        public void PointerModule_TouchReads_AreGuarded()
        {
            var patched = RewiredInputModulePatch.PatchPointerModule(PointerModule);

            StringAssert.Contains("return Rewired.ReInput.touch != null && Rewired.ReInput.touch.touchSupported;", patched);
            StringAssert.Contains("var touch = Rewired.ReInput.touch;", patched);
            StringAssert.Contains("if(touch == null) return 0;", patched);
            StringAssert.Contains("if(touch == null) return default(Rewired.UnityTouch);", patched);
            StringAssert.Contains(RewiredInputModulePatch.Marker, patched);
            Assert.IsFalse(RewiredInputModulePatch.NeedsPatch(patched));
        }

        [Test]
        public void PointerModule_IsIdempotent()
        {
            var once = RewiredInputModulePatch.PatchPointerModule(PointerModule);
            var twice = RewiredInputModulePatch.PatchPointerModule(once);

            Assert.AreEqual(once, twice);
        }

        [Test]
        public void PointerModule_KeepsCrlfLineEndings()
        {
            var crlf = PointerModule.Replace("\n", "\r\n");

            var patched = RewiredInputModulePatch.PatchPointerModule(crlf);

            StringAssert.Contains("\r\n", patched);
            Assert.IsFalse(Regex.IsMatch(patched, "(?<!\r)\n"), "A bare LF was introduced into a CRLF file.");
        }

        [Test]
        public void PointerModule_WithoutTheKnownStatements_IsUntouched()
        {
            const string unrelated = "class SomethingElse {\n    void M() { return; }\n}\n";

            Assert.AreEqual(unrelated, RewiredInputModulePatch.PatchPointerModule(unrelated));
            Assert.IsFalse(RewiredInputModulePatch.NeedsPatch(unrelated));
        }

        [Test]
        public void StandaloneModule_ReadsTouchSupportAgainWhenRewiredIsReady()
        {
            var patched = RewiredInputModulePatch.PatchStandaloneModule(StandaloneModule);

            StringAssert.Contains("Touch support is only readable once Rewired is initialized", patched);
            Assert.IsFalse(RewiredInputModulePatch.NeedsStandalonePatch(patched));
            Assert.AreEqual(patched, RewiredInputModulePatch.PatchStandaloneModule(patched));
        }

        [Test]
        public void StandaloneModule_DefersTheFirstInitInsteadOfReportingAnError()
        {
            var patched = RewiredInputModulePatch.PatchStandaloneModule(StandaloneModule);

            Assert.IsTrue(RewiredInputModulePatch.HasDeferredInitGuard(patched));
            StringAssert.Contains("VerifyRewiredInitialization();", patched);
            StringAssert.Contains("MissingInputManagerReportDelay", patched);

            // The error now lives in the watchdog only: it is logged once, from the frame loop.
            Assert.AreEqual(1, CountOccurrences(patched, "Rewired is not initialized!"),
                "The false-positive error should be logged from the watchdog only.");
            StringAssert.Contains("private void VerifyRewiredInitialization()", patched);
            StringAssert.Contains("private void InitializeRewired() {", patched);
        }

        [Test]
        public void StandaloneModule_DefersIdempotently()
        {
            var once = RewiredInputModulePatch.PatchStandaloneModule(StandaloneModule);
            var twice = RewiredInputModulePatch.PatchStandaloneModule(once);

            Assert.AreEqual(once, twice);
        }

        [Test]
        public void StandaloneModule_DeferredGuardKeepsCrlfLineEndings()
        {
            var crlf = StandaloneModule.Replace("\n", "\r\n");

            var patched = RewiredInputModulePatch.PatchStandaloneModule(crlf);

            StringAssert.Contains("\r\n", patched);
            Assert.IsFalse(Regex.IsMatch(patched, "(?<!\r)\n"), "A bare LF was introduced into a CRLF file.");
        }

        [Test]
        public void StandaloneModule_WithUnexpectedLayout_IsUntouched()
        {
            // Do not guess when Rewired's private method moved: the audit then reports the missing guard.
            var moved = StandaloneModule.Replace("private void InitializeRewired()", "private void InitializeRewiredRenamed()");

            Assert.AreEqual(moved, RewiredInputModulePatch.PatchStandaloneModule(moved));
        }

        [Test]
        public void StandaloneModule_WithAnIncompleteLayout_AppliesNothing()
        {
            // One anchor is gone (Rewired update renamed the shutdown handler): never leave a half-applied guard behind.
            var incomplete = StandaloneModule.Replace("private void OnRewiredShutDown()", "private void OnRewiredStopped()");

            var patched = RewiredInputModulePatch.PatchStandaloneModule(incomplete);

            Assert.IsFalse(RewiredInputModulePatch.HasDeferredInitGuard(patched));
            StringAssert.Contains("Debug.LogError(\"Rewired is not initialized!", patched);
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var index = 0;

            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
