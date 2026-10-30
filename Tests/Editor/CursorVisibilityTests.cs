using NUnit.Framework;

namespace Wagenheimer.RewiredHelper.Tests
{
    /// <summary>
    /// The OS pointer must never disappear in Play Mode: a hidden pointer makes the game impossible to
    /// test with the mouse. The Editor override wins over every per-device hide request, but only in
    /// the Editor and only while it is on, so a build keeps the real per-device behavior.
    /// </summary>
    public class CursorVisibilityTests
    {
        [TestCase(false, true, true, true)]   // Editor override on: a hide request is ignored
        [TestCase(true, true, true, true)]    // already visible: stays visible
        [TestCase(false, false, true, false)] // override off: the Editor behaves like a build
        [TestCase(false, true, false, false)] // a build ignores the Editor-only flag completely
        [TestCase(false, false, false, false)]
        [TestCase(true, false, false, true)]
        public void ResolveOsCursorVisible_OnlyOverridesInsideTheEditor(
            bool requestedVisible, bool keepVisibleInEditor, bool isEditor, bool expected)
        {
            Assert.AreEqual(expected,
                RewiredInputManager.ResolveOsCursorVisible(requestedVisible, keepVisibleInEditor, isEditor));
        }

        [Test]
        public void Manager_DefaultsToKeepingTheCursorVisibleInTheEditor()
        {
            var go = new UnityEngine.GameObject("Manager");
            try
            {
                var manager = go.AddComponent<RewiredInputManager>();
                Assert.IsTrue(manager.KeepOsCursorVisibleInEditor);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
