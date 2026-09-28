using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using UnityEditor;

using UnityEngine;

using Wagenheimer.RewiredHelper.Editor;

namespace Wagenheimer.RewiredHelper.Tests
{
    /// <summary>The pause policy must follow the build platform with zero configuration, and only an explicit override may change it.</summary>
    public class RewiredPolicyEditorTests
    {
#if UNITY_ANDROID || UNITY_IOS
        private const bool BuildIsMobile = true;
#else
        private const bool BuildIsMobile = false;
#endif

        private GameObject _go;
        private RewiredInputManager _manager;
        private GameObject _pauseScreen;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("RewiredPolicyTest");
            _manager = _go.AddComponent<RewiredInputManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_pauseScreen != null) Object.DestroyImmediate(_pauseScreen);
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void NewManager_FollowsTheBuildPlatformWithoutAnyConfiguration()
        {
            var expected = PausePolicy.ForPlatform(BuildIsMobile);

            Assert.IsFalse(_manager.OverridePlatformDefaults);
            Assert.AreEqual(expected.AppBackground, _manager.EffectiveAppBackgroundMode);
            Assert.AreEqual(expected.PauseOnControllerDisconnect, _manager.ShouldPauseOnControllerDisconnect);
            Assert.AreEqual(expected.ResumeOnAnyInput, _manager.ShouldResumeOnAnyInput);
        }

        [Test]
        public void IsSteamDeck_WithoutSteamworksInstalled_IsFalseAndNeverThrows()
        {
            // No SteamManager type is loaded in this test environment, so IsSteamReady short-circuits IsSteamDeck
            // to false before ever calling Steamworks.SteamUtils.IsRunningOnSteamHardware.
            Assert.DoesNotThrow(() => Assert.IsFalse(RewiredInputManager.IsSteamDeck));
        }

        [Test]
        public void SupportsSteamDeckDetection_IsACompileTimeConstant()
        {
            // Whatever it resolves to in this build, reading it must never throw; it's a straight #if-compiled
            // const, not reflection, so there's nothing to probe at runtime.
            Assert.DoesNotThrow(() => { var _ = RewiredInputManager.SupportsSteamDeckDetection; });
        }

        [Test]
        public void MobilePolicyActive_MatchesTheBuildPlatformInTheEditor()
        {
            Assert.AreEqual(BuildIsMobile, RewiredInputManager.IsMobileBuild);
            Assert.AreEqual(BuildIsMobile, RewiredInputManager.IsMobilePolicyActive); // never a TV in the editor
        }

        [Test]
        public void StoredForcedValues_AreIgnoredUntilOverrideIsEnabled()
        {
            _manager.PauseOnAppBackground = AppBackgroundPauseMode.Overlay;
            _manager.PauseOnControllerDisconnect = AutoToggle.On;
            _manager.ResumeOnAnyInput = AutoToggle.On;

            var expected = PausePolicy.ForPlatform(BuildIsMobile);

            Assert.AreEqual(expected.AppBackground, _manager.EffectiveAppBackgroundMode);
            Assert.AreEqual(expected.PauseOnControllerDisconnect, _manager.ShouldPauseOnControllerDisconnect);
            Assert.AreEqual(expected.ResumeOnAnyInput, _manager.ShouldResumeOnAnyInput);
        }

        [Test]
        public void EnabledOverride_AppliesExplicitValues()
        {
            _manager.OverridePlatformDefaults = true;
            _manager.PauseOnAppBackground = AppBackgroundPauseMode.Off;
            _manager.PauseOnControllerDisconnect = AutoToggle.On;
            _manager.ResumeOnAnyInput = AutoToggle.Off;

            Assert.AreEqual(AppBackgroundPauseMode.Off, _manager.EffectiveAppBackgroundMode);
            Assert.IsTrue(_manager.ShouldPauseOnControllerDisconnect);
            Assert.IsFalse(_manager.ShouldResumeOnAnyInput);
        }

        [Test]
        public void SystemUiScope_VetoesPausesAndKeepsAGraceWindowAfterDispose()
        {
            var scope = RewiredInputManager.BeginSystemUi();
            Assert.IsTrue(RewiredInputManager.IsSystemUiActive);

            scope.Dispose();
            scope.Dispose(); // idempotent

            Assert.IsTrue(RewiredInputManager.IsSystemUiActive); // grace window
        }

        [Test]
        public void BuildPreprocessor_TreatsOnlyAndroidAndIosAsMobile()
        {
            Assert.IsTrue(RewiredBuildPreprocessor.IsMobile(BuildTarget.Android));
            Assert.IsTrue(RewiredBuildPreprocessor.IsMobile(BuildTarget.iOS));
            Assert.IsFalse(RewiredBuildPreprocessor.IsMobile(BuildTarget.StandaloneWindows64));
            Assert.IsFalse(RewiredBuildPreprocessor.IsMobile(BuildTarget.Switch));
            Assert.IsFalse(RewiredBuildPreprocessor.IsMobile(BuildTarget.WebGL));
        }

        [Test]
        public void Audit_WithAutomaticPolicyAndAPauseScreen_HasNoPauseWarnings()
        {
            AssignPauseScreen();

            var results = RunMobileAudit();

            Assert.IsFalse(results.Any(r => r.Severity == AuditSeverity.Fail || r.Severity == AuditSeverity.Warning));
            Assert.IsTrue(results.Any(r => r.Title.Contains("automatic") && r.Severity == AuditSeverity.Pass));
        }

        [Test]
        public void Audit_WithoutAPauseScreen_FailsAndOffersToCreateOne()
        {
            var failure = RunMobileAudit().Single(r => r.Title.Contains("Pause screen assigned"));

            Assert.AreEqual(AuditSeverity.Fail, failure.Severity);
            Assert.IsNotNull(failure.Fix);
        }

        [Test]
        public void Audit_WhenNothingCanFreeze_DoesNotRequireAPauseScreen()
        {
            _manager.OverridePlatformDefaults = true;
            _manager.PauseOnAppBackground = AppBackgroundPauseMode.Off;
            _manager.PauseOnControllerDisconnect = AutoToggle.Off;
            _manager.PauseOnSteamOverlay = false;

            var results = RunMobileAudit();

            Assert.IsFalse(results.Any(r => r.Severity == AuditSeverity.Fail));
        }

        [Test]
        public void Audit_PauseScreenWithoutAWayOut_Fails()
        {
            AssignPauseScreen();
            _manager.OverridePlatformDefaults = true;
            _manager.ResumeOnAnyInput = AutoToggle.Off; // no tap-to-resume anywhere and the screen has no Resume button

            var failure = RunMobileAudit().Single(r => r.Title.Contains("resumed"));

            Assert.AreEqual(AuditSeverity.Fail, failure.Severity);
        }

        [Test]
        public void Audit_WithForcedOverlayOverride_WarnsButOffersNoFix()
        {
            AssignPauseScreen();
            _manager.OverridePlatformDefaults = true;
            _manager.PauseOnAppBackground = AppBackgroundPauseMode.Overlay;

            var warning = RunMobileAudit().Single(r => r.Title.Contains("app-background"));

            Assert.AreEqual(AuditSeverity.Warning, warning.Severity);
            Assert.IsNull(warning.Fix); // the policy is automatic: nothing to "fix", the user chose the override
        }

        [Test]
        public void Audit_WithForcedDisconnectPause_WarnsButOffersNoFix()
        {
            AssignPauseScreen();
            _manager.OverridePlatformDefaults = true;
            _manager.PauseOnControllerDisconnect = AutoToggle.On;

            var warning = RunMobileAudit().Single(r => r.Title.Contains("controller-disconnect"));

            Assert.AreEqual(AuditSeverity.Warning, warning.Severity);
            Assert.IsNull(warning.Fix);
        }

        [Test]
        public void ToMarkdown_ListsCountsAndFindings()
        {
            var results = new List<AuditResult>
            {
                new AuditResult { Category = "Mobile & Pause", Title = "A", Severity = AuditSeverity.Pass, Detail = "ok" },
                new AuditResult { Category = "Mobile & Pause", Title = "B", Severity = AuditSeverity.Warning, Detail = "bad", FixHint = "do x" }
            };

            var markdown = RewiredHelperAudit.ToMarkdown(results);

            StringAssert.Contains("Warnings: 1", markdown);
            StringAssert.Contains("Passed: 1", markdown);
            StringAssert.Contains("Fix: do x", markdown);
        }

        private void AssignPauseScreen()
        {
            _pauseScreen = new GameObject("PauseScreen");
            _manager.GamePaused = _pauseScreen;
        }

        private List<AuditResult> RunMobileAudit()
        {
            var results = new List<AuditResult>();
            RewiredHelperAudit.AuditMobilePause(results, _manager);
            return results;
        }
    }
}
