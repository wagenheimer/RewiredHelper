using System.Linq;

using NUnit.Framework;

using UnityEditor;

using UnityEngine;

using Wagenheimer.RewiredHelper.Editor;

namespace Wagenheimer.RewiredHelper.Tests
{
    /// <summary>
    /// Covers the legacy <c>global::RewiredHelper</c> subclass shim: it must be detected by the Setup Audit and
    /// migrated in place (never destroyed/recreated, so nothing else's reference to the component breaks), and
    /// the customized Inspector must apply to it too, not only to the exact RewiredInputManager type.
    /// </summary>
    public class RewiredLegacyMigratorTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void PlainRewiredInputManager_IsNotLegacy()
        {
            _go = new GameObject("Manager");
            var manager = _go.AddComponent<RewiredInputManager>();

            Assert.IsFalse(RewiredLegacyMigrator.IsLegacyComponent(manager));
        }

        [Test]
        public void LegacyRewiredHelperComponent_IsDetected()
        {
            _go = new GameObject("LegacyManager");
            var legacy = _go.AddComponent<global::RewiredHelper>();

            Assert.IsTrue(RewiredLegacyMigrator.IsLegacyComponent(legacy));
            CollectionAssert.Contains(RewiredLegacyMigrator.FindLegacyInOpenScenes(), legacy);
        }

        [Test]
        public void MigrateOpenScenes_SwapsTheScriptWithoutRecreatingTheComponent()
        {
            _go = new GameObject("LegacyManager");
            var legacy = _go.AddComponent<global::RewiredHelper>();
            legacy.AutoConfigureOnStart = false; // a non-default value must survive the swap

            RewiredLegacyMigrator.MigrateOpenScenes();

            // Exactly one RewiredInputManager-family component remains (the script was swapped in place,
            // not destroyed and re-added as a second component).
            var remaining = _go.GetComponents<RewiredInputManager>();
            Assert.AreEqual(1, remaining.Length);

            var migrated = remaining[0];
            Assert.AreEqual(typeof(RewiredInputManager), migrated.GetType());
            Assert.IsFalse(migrated.AutoConfigureOnStart); // field data carried over
            Assert.IsFalse(RewiredLegacyMigrator.IsLegacyComponent(migrated));
        }

        [Test]
        public void SetupAudit_FlagsALegacyComponentAndOffersTheMigrationFix()
        {
            _go = new GameObject("LegacyManager");
            _go.AddComponent<global::RewiredHelper>();

            var results = new System.Collections.Generic.List<AuditResult>();
            RewiredSetupChecks.Run(results, _go.GetComponent<RewiredInputManager>());

            var finding = results.Single(r => r.Title.Contains("legacy RewiredHelper"));
            Assert.AreEqual(AuditSeverity.Warning, finding.Severity);
            Assert.IsNotNull(finding.Fix);
        }

        [Test]
        public void CustomEditor_AppliesToSubclassesToo()
        {
            _go = new GameObject("LegacyManager");
            var legacy = _go.AddComponent<global::RewiredHelper>();

            var editor = UnityEditor.Editor.CreateEditor(legacy);
            try
            {
                Assert.IsInstanceOf<RewiredInputManagerEditor>(editor,
                    "The legacy RewiredHelper component must get the custom Inspector too, not Unity's plain fallback.");
            }
            finally
            {
                Object.DestroyImmediate(editor);
            }
        }
    }
}
