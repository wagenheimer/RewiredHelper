using NUnit.Framework;

using Wagenheimer.RewiredHelper.Editor;

namespace Wagenheimer.RewiredHelper.Tests
{
    public class OverrideScannerTests
    {
        private const string ForcedOverlayOverride = "MonoBehaviour:\n  OverridePlatformDefaults: 1\n  PauseOnAppBackground: 3\n" +
                                                     "  PauseOnControllerDisconnect: 0\n  ResumeOnAnyInput: 0\n";

        [Test]
        public void ForcedOverlayWithOverrideOn_IsReported()
        {
            var issues = RewiredOverrideScanner.FindMobileBreakingOverrides(ForcedOverlayOverride);

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(RewiredOverrideScanner.OverlayIssue, issues[0]);
        }

        [Test]
        public void ForcedValuesWithOverrideOff_AreIgnored()
        {
            var yaml = ForcedOverlayOverride.Replace("OverridePlatformDefaults: 1", "OverridePlatformDefaults: 0");

            Assert.IsEmpty(RewiredOverrideScanner.FindMobileBreakingOverrides(yaml));
        }

        [Test]
        public void ForcedDisconnectPauseWithOverrideOn_IsReported()
        {
            var yaml = "  OverridePlatformDefaults: 1\n  PauseOnAppBackground: 0\n  PauseOnControllerDisconnect: 1\n";

            var issues = RewiredOverrideScanner.FindMobileBreakingOverrides(yaml);

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(RewiredOverrideScanner.DisconnectIssue, issues[0]);
        }

        [Test]
        public void BothProblems_AreBothReported()
        {
            var yaml = "  OverridePlatformDefaults: 1\n  PauseOnAppBackground: 3\n  PauseOnControllerDisconnect: 1\n";

            Assert.AreEqual(2, RewiredOverrideScanner.FindMobileBreakingOverrides(yaml).Count);
        }

        [Test]
        public void SafeOverrideValues_AreNotReported()
        {
            var yaml = "  OverridePlatformDefaults: 1\n  PauseOnAppBackground: 2\n  PauseOnControllerDisconnect: 2\n";

            Assert.IsEmpty(RewiredOverrideScanner.FindMobileBreakingOverrides(yaml));
        }

        [Test]
        public void EmptyOrNullYaml_ReportsNothing()
        {
            Assert.IsEmpty(RewiredOverrideScanner.FindMobileBreakingOverrides(null));
            Assert.IsEmpty(RewiredOverrideScanner.FindMobileBreakingOverrides(string.Empty));
        }

        [Test]
        public void FieldsWithSimilarNames_DoNotFalselyMatch()
        {
            var yaml = "  OverridePlatformDefaults: 1\n  PauseOnAppBackgroundExtra: 3\n";

            Assert.IsEmpty(RewiredOverrideScanner.FindMobileBreakingOverrides(yaml));
        }

        [Test]
        public void PrefabInstanceModifications_AreDetected()
        {
            var yaml = "    - target: {fileID: 1}\n      propertyPath: OverridePlatformDefaults\n      value: 1\n" +
                       "    - target: {fileID: 1}\n      propertyPath: PauseOnAppBackground\n      value: 3\n";

            var issues = RewiredOverrideScanner.FindMobileBreakingOverrides(yaml);

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(RewiredOverrideScanner.OverlayIssue, issues[0]);
        }

        [Test]
        public void PrefabInstanceModifications_WithSafeValues_AreIgnored()
        {
            var yaml = "    - target: {fileID: 1}\n      propertyPath: OverridePlatformDefaults\n      value: 1\n" +
                       "    - target: {fileID: 1}\n      propertyPath: PauseOnAppBackground\n      value: 2\n";

            Assert.IsEmpty(RewiredOverrideScanner.FindMobileBreakingOverrides(yaml));
        }

        [Test]
        public void EmptyGamePausedReference_IsReportedAsMissingPauseScreen()
        {
            Assert.IsTrue(RewiredOverrideScanner.HasMissingPauseScreen("MonoBehaviour:\n  GamePaused: {fileID: 0}\n  PauseOnSteamOverlay: 1\n"));
        }

        [Test]
        public void AssignedGamePausedReference_IsNotReported()
        {
            Assert.IsFalse(RewiredOverrideScanner.HasMissingPauseScreen("MonoBehaviour:\n  GamePaused: {fileID: 1234567}\n"));
            Assert.IsFalse(RewiredOverrideScanner.HasMissingPauseScreen(null));
        }
    }
}
