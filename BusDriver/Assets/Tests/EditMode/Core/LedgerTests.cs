using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    // The night ledger (§2.7, T-M3-05)
    public class LedgerTests {
        [Test]
        public void EntriesKeepTheirOrderAndFields() {
            Ledger ledger = new Ledger();
            ledger.Record(new LedgerEntry(LedgerKind.Fare, 350, "r01", "farm_gate", 1950.0));
            ledger.Record(new LedgerEntry(LedgerKind.Tip, 175, "r01", "church", 3786.0));
            Assert.AreEqual(2, ledger.Entries.Count);
            LedgerEntry tip = ledger.Entries[1];
            Assert.AreEqual(LedgerKind.Tip, tip.Kind);
            Assert.AreEqual(175, tip.AmountCents);
            Assert.AreEqual("r01", tip.RiderId);
            Assert.AreEqual("church", tip.StopId);
            Assert.AreEqual(3786.0, tip.GameTime);
        }

        [Test]
        public void TotalsSumByKindAndCount() {
            Ledger ledger = new Ledger();
            ledger.Record(new LedgerEntry(LedgerKind.Fare, 350, "r01", "farm_gate", 0));
            ledger.Record(new LedgerEntry(LedgerKind.Fare, 350, "r02", "farm_gate", 0));
            ledger.Record(new LedgerEntry(LedgerKind.Fare, 350, "r06", "campground", 0));
            ledger.Record(new LedgerEntry(LedgerKind.Tip, 175, "r02", "church", 0));
            ledger.Record(new LedgerEntry(LedgerKind.Refund, -350, "r01", "", 0));
            ledger.Record(new LedgerEntry(LedgerKind.Bounty, 500, "r06", "", 0));
            ledger.Record(new LedgerEntry(LedgerKind.Lost, -350, "r03", "", 0));
            LedgerTotals totals = ledger.Totals;
            Assert.AreEqual(1050, totals.Cents(LedgerKind.Fare));
            Assert.AreEqual(3, totals.Count(LedgerKind.Fare));
            Assert.AreEqual(175, totals.Cents(LedgerKind.Tip));
            Assert.AreEqual(-350, totals.Cents(LedgerKind.Refund));
            Assert.AreEqual(500, totals.Cents(LedgerKind.Bounty));
            Assert.AreEqual(-350, totals.Cents(LedgerKind.Lost));
            Assert.AreEqual(1, totals.Count(LedgerKind.Lost));
            Assert.AreEqual(1050 + 175 - 350 + 500 - 350, totals.NetCents);
        }

        [Test]
        public void AnEmptyLedgerIsZero() {
            Ledger ledger = new Ledger();
            Assert.AreEqual(0, ledger.Totals.NetCents);
            for (int i = 0; i < LedgerTotals.KindCount; i++) {
                Assert.AreEqual(0, ledger.Totals.Count((LedgerKind)i));
            }
        }

        [Test]
        public void CloneIsASnapshot() {
            Ledger ledger = new Ledger();
            ledger.Record(new LedgerEntry(LedgerKind.Fare, 350, "r01", "", 0));
            LedgerTotals snapshot = ledger.Totals.Clone();
            ledger.Record(new LedgerEntry(LedgerKind.Fare, 350, "r02", "", 0));
            Assert.AreEqual(350, snapshot.NetCents);
            Assert.AreEqual(700, ledger.Totals.NetCents);
        }

        // Every LedgerKind value is a valid index into the totals
        [Test]
        public void KindCountCoversEveryKind() {
            foreach (LedgerKind kind in System.Enum.GetValues(typeof(LedgerKind))) {
                Assert.Less((int)kind, LedgerTotals.KindCount, kind.ToString());
                Assert.GreaterOrEqual((int)kind, 0);
            }
        }
    }
}
