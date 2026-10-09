using NUnit.Framework;

using Wagenheimer.RewiredHelper.Editor;

namespace Wagenheimer.RewiredHelper.Tests
{
    /// <summary>Every finding explains itself: a known title gets its description and code; an unknown one still gets a sensible default.</summary>
    public class RewiredAuditInfoTests
    {
        [TearDown]
        public void TearDown() => RewiredAuditHistory.Clear();

        [Test]
        public void Enrich_AddsTheDescriptionAndCodeOfAKnownFinding()
        {
            var result = new AuditResult { Category = "Scene Setup", Title = "Event System uses Rewired's input module", Severity = AuditSeverity.Pass };

            var enriched = RewiredAuditInfo.Enrich(result);

            Assert.IsNotEmpty(enriched.About);
            StringAssert.Contains("RewiredStandaloneInputModule", enriched.Code);
        }

        [Test]
        public void Enrich_GivesAnUnknownFindingADefaultDescription()
        {
            var enriched = RewiredAuditInfo.Enrich(new AuditResult { Category = "Project", Title = "Something new" });

            Assert.IsNotEmpty(enriched.About);
            Assert.IsNull(enriched.Code);
        }

        [Test]
        public void Enrich_KeepsADescriptionThatWasAlreadySet()
        {
            var enriched = RewiredAuditInfo.Enrich(new AuditResult { Title = "UI Canvas", About = "custom" });

            Assert.AreEqual("custom", enriched.About);
        }

        [Test]
        public void History_RemembersTheLatestFixOfAFinding()
        {
            RewiredAuditHistory.Record("UI Canvas", "Create Canvas");

            var note = RewiredAuditInfo.Enrich(new AuditResult { Title = "UI Canvas" }).DoneNote;

            StringAssert.Contains("Create Canvas", note);
        }

        [Test]
        public void History_ReturnsNewestFirstAndCanBeCleared()
        {
            RewiredAuditHistory.Record("A", "fix A");
            RewiredAuditHistory.Record("B", "fix B");

            var entries = RewiredAuditHistory.Entries();

            Assert.AreEqual("B", entries[0].Title);
            Assert.AreEqual("A", entries[1].Title);

            RewiredAuditHistory.Clear();
            Assert.IsEmpty(RewiredAuditHistory.Entries());
        }
    }
}
