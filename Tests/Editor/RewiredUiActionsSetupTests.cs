using NUnit.Framework;

using Wagenheimer.RewiredHelper.Editor;

namespace Wagenheimer.RewiredHelper.Tests
{
    /// <summary>The pure parts of the UI-actions setup: Rewired's hex id encoding and the default action names.</summary>
    public class RewiredUiActionsSetupTests
    {
        [TestCase(0, "00000000")]
        [TestCase(1, "01000000")]
        [TestCase(10, "0a000000")]
        [TestCase(300, "2c010000")]
        public void EncodeId_WritesLittleEndianHex(int id, string expected)
        {
            Assert.AreEqual(expected, RewiredUiActionsSetup.EncodeId(id));
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
