using NUnit.Framework;

namespace Wagenheimer.RewiredHelper.Tests
{
    public class PauseControllerTests
    {
        private float _timeScale;
        private PauseController _pause;
        private int _changedCount;

        [SetUp]
        public void SetUp()
        {
            _timeScale = 1f;
            _changedCount = 0;
            _pause = new PauseController(() => _timeScale, scale => _timeScale = scale);
            _pause.Changed += (_, _) => _changedCount++;
        }

        [Test]
        public void SilentPause_DoesNotTouchTimeScale()
        {
            _pause.Pause(PauseReason.AppBackground, freeze: false);

            Assert.IsTrue(_pause.IsPaused);
            Assert.IsFalse(_pause.IsFrozen);
            Assert.AreEqual(1f, _timeScale);
        }

        [Test]
        public void FrozenPause_ZeroesTimeScaleAndRestoresPreviousValue()
        {
            _timeScale = 0.5f;

            _pause.Pause(PauseReason.Manual, freeze: true);
            Assert.AreEqual(0f, _timeScale);

            _pause.Resume(PauseReason.Manual);
            Assert.AreEqual(0.5f, _timeScale);
            Assert.IsFalse(_pause.IsPaused);
        }

        [Test]
        public void MultipleReasons_DoNotReleaseEachOther()
        {
            _pause.Pause(PauseReason.ControllerDisconnected, freeze: true);
            _pause.Pause(PauseReason.SteamOverlay, freeze: true);

            _pause.Resume(PauseReason.SteamOverlay);
            Assert.IsTrue(_pause.IsFrozen);
            Assert.AreEqual(0f, _timeScale);

            _pause.Resume(PauseReason.ControllerDisconnected);
            Assert.IsFalse(_pause.IsFrozen);
            Assert.AreEqual(1f, _timeScale);
        }

        [Test]
        public void HostChangedTimeScaleWhilePaused_IsNotOverwrittenOnResume()
        {
            _pause.Pause(PauseReason.AppBackground, freeze: true);
            _timeScale = 0.25f; // host applies its own slow-mo

            _pause.Resume(PauseReason.AppBackground);

            Assert.AreEqual(0.25f, _timeScale);
        }

        [Test]
        public void HostAlreadyFrozeTime_ResumeLeavesTimeFrozen()
        {
            _timeScale = 0f; // the host's own pause menu is open

            _pause.Pause(PauseReason.SteamOverlay, freeze: true);
            _pause.Resume(PauseReason.SteamOverlay);

            Assert.AreEqual(0f, _timeScale);
            Assert.IsFalse(_pause.IsPaused);
        }

        [Test]
        public void ResumeFrozen_KeepsSilentReasons()
        {
            _pause.Pause(PauseReason.AppBackground, freeze: false);
            _pause.Pause(PauseReason.ControllerDisconnected, freeze: true);

            _pause.ResumeFrozen();

            Assert.IsFalse(_pause.IsFrozen);
            Assert.IsTrue(_pause.Has(PauseReason.AppBackground));
            Assert.IsFalse(_pause.Has(PauseReason.ControllerDisconnected));
        }

        [Test]
        public void ResumeAll_ReleasesEverything()
        {
            _pause.Pause(PauseReason.AppBackground, freeze: false);
            _pause.Pause(PauseReason.Manual, freeze: true);

            _pause.ResumeAll();

            Assert.IsFalse(_pause.IsPaused);
            Assert.AreEqual(1f, _timeScale);
        }

        [Test]
        public void Resume_OfInactiveReason_ReturnsFalseAndRaisesNothing()
        {
            Assert.IsFalse(_pause.Resume(PauseReason.Manual));
            Assert.AreEqual(0, _changedCount);
        }

        [Test]
        public void Changed_FiresOnlyOnStateTransitions()
        {
            _pause.Pause(PauseReason.Manual, freeze: true);
            _pause.Pause(PauseReason.Manual, freeze: true); // duplicate
            _pause.Resume(PauseReason.Manual);

            Assert.AreEqual(2, _changedCount);
        }
    }

    public class SystemUiTrackerTests
    {
        private float _now;
        private SystemUiTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
            _tracker = new SystemUiTracker(() => _now);
        }

        [Test]
        public void OpenScope_IsActiveUntilEnded_ThenGraceThenInactive()
        {
            int token = _tracker.Begin();
            Assert.IsTrue(_tracker.IsActive);

            _tracker.End(token, graceSeconds: 1f);
            Assert.IsTrue(_tracker.IsActive); // grace window

            _now = 1.01f;
            Assert.IsFalse(_tracker.IsActive);
        }

        [Test]
        public void NestedScopes_StayActiveUntilAllEnded()
        {
            int a = _tracker.Begin();
            int b = _tracker.Begin();

            _tracker.End(a, graceSeconds: 0f);
            Assert.IsTrue(_tracker.IsActive);

            _tracker.End(b, graceSeconds: 0f);
            Assert.IsFalse(_tracker.IsActive);
        }

        [Test]
        public void LostEnd_ExpiresByItself()
        {
            _tracker.Begin();

            _now = SystemUiTracker.MaxScopeSeconds + 1f;

            Assert.IsFalse(_tracker.IsActive);
        }

        [Test]
        public void DuplicateEnd_IsIgnored()
        {
            int token = _tracker.Begin();
            _tracker.End(token, graceSeconds: 0f);

            _tracker.End(token, graceSeconds: 100f);

            Assert.IsFalse(_tracker.IsActive);
        }

        [Test]
        public void Suppress_ActsForTheGivenSeconds()
        {
            _tracker.Suppress(2f);
            Assert.IsTrue(_tracker.IsActive);

            _now = 2.01f;
            Assert.IsFalse(_tracker.IsActive);
        }
    }

    public class PausePolicyTests
    {
        [Test]
        public void ForPlatform_Mobile_IsSilentWithoutDisconnectPauseOrTapResume()
        {
            var policy = PausePolicy.ForPlatform(isMobile: true);

            Assert.AreEqual(AppBackgroundPauseMode.Silent, policy.AppBackground);
            Assert.IsFalse(policy.PauseOnControllerDisconnect);
            Assert.IsFalse(policy.ResumeOnAnyInput);
        }

        [Test]
        public void ForPlatform_Desktop_KeepsClassicBehavior()
        {
            var policy = PausePolicy.ForPlatform(isMobile: false);

            Assert.AreEqual(AppBackgroundPauseMode.Overlay, policy.AppBackground);
            Assert.IsTrue(policy.PauseOnControllerDisconnect);
            Assert.IsTrue(policy.ResumeOnAnyInput);
        }

        [Test]
        public void Resolve_WithoutOverride_IgnoresStoredValues()
        {
            var policy = PausePolicy.Resolve(false, AppBackgroundPauseMode.Overlay, AutoToggle.On, AutoToggle.On, isMobile: true);

            Assert.AreEqual(AppBackgroundPauseMode.Silent, policy.AppBackground);
            Assert.IsFalse(policy.PauseOnControllerDisconnect);
            Assert.IsFalse(policy.ResumeOnAnyInput);
        }

        [Test]
        public void IsMobileDevice_MobileBuild_IsMobileExceptOnTelevisions()
        {
            Assert.IsTrue(PausePolicy.IsMobileDevice(isMobileBuild: true, isTelevision: false));
            Assert.IsFalse(PausePolicy.IsMobileDevice(isMobileBuild: true, isTelevision: true));
        }

        [Test]
        public void IsMobileDevice_NonMobileBuild_IsNeverMobile()
        {
            Assert.IsFalse(PausePolicy.IsMobileDevice(isMobileBuild: false, isTelevision: false));
            Assert.IsFalse(PausePolicy.IsMobileDevice(isMobileBuild: false, isTelevision: true));
        }

        [Test]
        public void AndroidTv_GetsTheDesktopPolicy()
        {
            bool isMobile = PausePolicy.IsMobileDevice(isMobileBuild: true, isTelevision: true);

            var policy = PausePolicy.Resolve(false, AppBackgroundPauseMode.Auto, AutoToggle.Auto, AutoToggle.Auto, isMobile);

            Assert.AreEqual(AppBackgroundPauseMode.Overlay, policy.AppBackground);
            Assert.IsTrue(policy.PauseOnControllerDisconnect);
            Assert.IsTrue(policy.ResumeOnAnyInput);
        }

        [Test]
        public void Resolve_WithOverride_AppliesExplicitValuesAndKeepsAutoAsPlatformDefault()
        {
            var policy = PausePolicy.Resolve(true, AppBackgroundPauseMode.Overlay, AutoToggle.Auto, AutoToggle.On, isMobile: true);

            Assert.AreEqual(AppBackgroundPauseMode.Overlay, policy.AppBackground);
            Assert.IsFalse(policy.PauseOnControllerDisconnect); // Auto on mobile
            Assert.IsTrue(policy.ResumeOnAnyInput);
        }
    }
}
