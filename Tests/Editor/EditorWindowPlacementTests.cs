using NUnit.Framework;

using UnityEngine;

using Wagenheimer.PackageHub.Editor;

namespace Wagenheimer.RewiredHelper.Tests
{
    /// <summary>A dashboard whose saved position is off-screen must be pulled back; a visible one must be left alone.</summary>
    public class EditorWindowPlacementTests
    {
        private static readonly Rect Host = new Rect(0, 0, 1920, 1080);
        private static readonly Vector2 Min = new Vector2(620, 520);
        private static readonly Vector2 Default = new Vector2(820, 640);

        [Test]
        public void ReturnsNull_WhenWindowIsFullyVisible()
        {
            var result = EditorWindowPlacement.ComputeVisibleRect(new Rect(300, 200, 800, 600), Host, Min, Default);

            Assert.IsFalse(result.HasValue);
        }

        [Test]
        public void CentersWithSameSize_WhenWindowIsOnDisconnectedMonitor()
        {
            var result = EditorWindowPlacement.ComputeVisibleRect(new Rect(-3000, 200, 800, 600), Host, Min, Default);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(new Rect(560, 240, 800, 600), result.Value);
        }

        [Test]
        public void UsesDefaultSize_WhenWindowIsDegenerate()
        {
            var result = EditorWindowPlacement.ComputeVisibleRect(new Rect(0, 0, 0, 0), Host, Min, Default);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(new Rect(550, 220, 820, 640), result.Value);
        }

        [Test]
        public void UsesDefaultSize_WhenPositionIsNaN()
        {
            var result = EditorWindowPlacement.ComputeVisibleRect(new Rect(float.NaN, 100, 800, 600), Host, Min, Default);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(Default.x, result.Value.width);
        }

        [Test]
        public void ClampsToHost_WhenWindowIsLargerThanTheScreen()
        {
            var result = EditorWindowPlacement.ComputeVisibleRect(new Rect(5000, 0, 4000, 3000), Host, Min, Default);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(1880f, result.Value.width);
            Assert.AreEqual(1040f, result.Value.height);
        }

        [Test]
        public void ReturnsNull_WhenHostIsUnknown()
        {
            var result = EditorWindowPlacement.ComputeVisibleRect(new Rect(-3000, 0, 800, 600), new Rect(0, 0, 0, 0), Min, Default);

            Assert.IsFalse(result.HasValue);
        }
    }
}
