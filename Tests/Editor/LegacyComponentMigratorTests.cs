using NUnit.Framework;
using Wagenheimer.RewiredHelper.Editor;

namespace Wagenheimer.RewiredHelper.Tests
{
    public class LegacyComponentMigratorTests
    {
        [Test]
        public void IsLegacyComponentSource_GlobalNamespaceCopy_IsDetected()
        {
            const string src = "using UnityEngine;\npublic class ReturnEscapeEvent : MonoBehaviour\n{\n}\n";
            Assert.IsTrue(LegacyComponentMigrator.IsLegacyComponentSource(src, "ReturnEscapeEvent"));
        }

        [Test]
        public void IsLegacyComponentSource_NamespacedPackageVersion_IsIgnored()
        {
            const string src = "namespace Wagenheimer.RewiredHelper\n{\n    public class ReturnEscapeEvent : MonoBehaviour { }\n}\n";
            Assert.IsFalse(LegacyComponentMigrator.IsLegacyComponentSource(src, "ReturnEscapeEvent"));
        }

        [Test]
        public void IsLegacyComponentSource_OtherClass_IsIgnored()
        {
            const string src = "public class ReturnEscapeEventDefaults : MonoBehaviour { }";
            Assert.IsFalse(LegacyComponentMigrator.IsLegacyComponentSource(src, "ReturnEscapeEvent"));
        }

        [Test]
        public void InsertUsing_AddsAfterLastUsing()
        {
            var result = LegacyComponentMigrator.InsertUsing("using System;\nusing UnityEngine;\n\npublic class A { }\n");
            StringAssert.Contains("using UnityEngine;\nusing Wagenheimer.RewiredHelper;", result);
        }

        [Test]
        public void InsertUsing_NoUsings_AddsAtTop()
        {
            var result = LegacyComponentMigrator.InsertUsing("public class A { }\n");
            StringAssert.StartsWith("using Wagenheimer.RewiredHelper;", result);
        }
    }
}
