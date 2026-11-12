using NUnit.Framework;
using Wagenheimer.RewiredHelper.Editor;

namespace Wagenheimer.RewiredHelper.Tests
{
    public class RewiredLegacyInputMigratorTests
    {
        private const string SampleMainBaseBlock = @"
    void Update()
    {
        player?.Update();

        if (!Main.main.IsUiBlocked)
        {
            if (Input.GetKeyDown(KeyCode.Escape)
                || RewiredHelper.instance.Player.GetButtonDown(""MenuButton"")
                || RewiredHelper.instance.Player.GetButtonDown(""BackButton""))
            {
                if (Dialogs.Modals.Count > 0 && Dialogs.Modals[Dialogs.Modals.Count - 1].EscapeButton != null &&
                    Dialogs.Modals[Dialogs.Modals.Count - 1].EscapeButton.interactable &&
                    Dialogs.Modals[Dialogs.Modals.Count - 1].EscapeButton.onClick.GetPersistentEventCount() > 0)
                    Dialogs.Modals[Dialogs.Modals.Count - 1].EscapeButton.onClick.Invoke();
                else
                    ReturnEscapeEvent.EscapePressed = true;
            }

            if (Input.GetKeyDown(KeyCode.Return))
            {
                if (Dialogs.Modals.Count > 0 && Dialogs.Modals[Dialogs.Modals.Count - 1].OkButton != null &&
                    Dialogs.Modals[Dialogs.Modals.Count - 1].OkButton.interactable & Dialogs.Modals[Dialogs.Modals.Count - 1].OkButton.onClick.GetPersistentEventCount() > 0)
                    Dialogs.Modals[Dialogs.Modals.Count - 1].OkButton.onClick.Invoke();
                else
                    ReturnEscapeEvent.OkPressed = true;
            }
        }

        deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
    }
";

        [Test]
        public void TryMigrateScriptText_ReplacesFullMainBaseBlock()
        {
            bool changed = RewiredLegacyInputMigrator.TryMigrateScriptText(SampleMainBaseBlock, out string migrated);

            Assert.IsTrue(changed);
            StringAssert.Contains("[RewiredHelper Migration]", migrated);
            StringAssert.Contains("O tratamento manual de Escape e Return foi desativado aqui", migrated);
            StringAssert.Contains("/*", migrated);
            StringAssert.Contains("*/", migrated);
            StringAssert.Contains("deltaTime +=", migrated);
        }

        [Test]
        public void TryMigrateScriptText_OnCleanScript_ReturnsFalse()
        {
            string cleanScript = @"
    void Update()
    {
        deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
    }
";
            bool changed = RewiredLegacyInputMigrator.TryMigrateScriptText(cleanScript, out string migrated);

            Assert.IsFalse(changed);
            Assert.AreEqual(cleanScript, migrated);
        }

        [Test]
        public void TryMigrateScriptText_IsIdempotent()
        {
            RewiredLegacyInputMigrator.TryMigrateScriptText(SampleMainBaseBlock, out string firstMigration);
            bool secondRun = RewiredLegacyInputMigrator.TryMigrateScriptText(firstMigration, out string secondMigration);

            Assert.IsFalse(secondRun);
            Assert.AreEqual(firstMigration, secondMigration);
        }
    }
}

