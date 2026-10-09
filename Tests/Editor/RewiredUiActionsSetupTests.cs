using NUnit.Framework;

using Wagenheimer.RewiredHelper.Editor;

namespace Wagenheimer.RewiredHelper.Tests
{
    /// <summary>The pure parts of the UI-actions setup: Rewired's hex id encoding and the default action names.</summary>
    public class RewiredUiActionsSetupTests
    {
        [TestCase(0, new byte[] { 0, 0, 0, 0 })]
        [TestCase(1, new byte[] { 1, 0, 0, 0 })]
        [TestCase(10, new byte[] { 10, 0, 0, 0 })]
        [TestCase(300, new byte[] { 44, 1, 0, 0 })]
        public void EncodeId_WritesFourLittleEndianBytes(int id, byte[] expected)
        {
            CollectionAssert.AreEqual(expected, RewiredUiActionsSetup.EncodeId(id));
        }

        [Test]
        public void DefaultNames_CoverEveryRoleWithTheModuleDefaults()
        {
            var names = RewiredUiActionsSetup.DefaultNames();

            Assert.AreEqual(4, names.Count);
            Assert.AreEqual("UIHorizontal", names[RewiredUiActionsSetup.Role.Horizontal]);
            Assert.AreEqual("UIVertical", names[RewiredUiActionsSetup.Role.Vertical]);
            Assert.AreEqual("UISubmit", names[RewiredUiActionsSetup.Role.Submit]);
            Assert.AreEqual("UICancel", names[RewiredUiActionsSetup.Role.Cancel]);
        }
    }
}
