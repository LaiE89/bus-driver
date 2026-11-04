using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Save;
using BusDriver.Gameplay.Economy;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    // Carried wallet minus a depot purchase is what the next night starts with
    public class ItemWalletTests {
        [Test]
        public void SpendComesOffTheCarriedWallet() {
            ShiftLedger ledger = new ShiftLedger(1000);
            Assert.IsFalse(ledger.TrySpend(1001));
            Assert.AreEqual(0, ledger.SpentCents);
            Assert.IsTrue(ledger.TrySpend(600));
            ledger.Record(new LedgerEntry(LedgerKind.Fare, 350, "r", "farm_gate", 0));
            Assert.AreEqual(750, ledger.WalletNowCents);

            NightResult result = new NightResult {
                walletBeforeCents = ledger.WalletBeforeCents,
                spentCents = ledger.SpentCents,
                totals = ledger.Totals.Clone(),
            };
            Assert.AreEqual(750, result.WalletAfterCents);
        }

        [Test]
        public void SlotsHoldThreeCoffees() {
            RunState run = RunState.NewRun(1);
            Assert.AreEqual(3, run.slots.Length);
            run.walletCents = 1800;
            Assert.AreEqual(1800, run.walletCents);
        }
    }
}
