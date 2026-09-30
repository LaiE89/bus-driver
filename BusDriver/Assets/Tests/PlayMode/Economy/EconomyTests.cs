using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Economy;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Tests.PlayMode.Flow;
using BusDriver.Tests.PlayMode.Passengers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Economy {
    // Fares and tips (T-M3-05, §2.7) on night 1
    public class EconomyTests {
        const float TimeScale = 3f;
        const int Seed = 20260930;

        string saveRoot;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            Time.timeScale = 1f;
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        // Straight to gas_station (farm_gate is missed), where look03 (→ church) and look04
        // (→ campground) board: a fare each, credited the moment they board. AutoPilot is well
        // ahead of the timetable there (it skipped farm_gate's dwell), so campground is reached
        // Early and look04 earns the 50 % tip.
        [UnityTest, Timeout(900000)]
        public IEnumerator Economy_FareOnBoard_TipOnEarly() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.DebugNoMonsters = true;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            ShiftServices shift = night.Shift;
            ShiftLedger ledger = shift.Ledger;
            BalanceConfig balance = shift.Balance;
            Assert.AreEqual(0, ledger.Entries.Count);

            List<string> fareAtBoarding = new List<string>();
            shift.Cabin.OnPassengerBoarded += passenger => {
                RiderRecord record = shift.Riders.For(passenger);
                bool paid = false;
                foreach (LedgerEntry entry in ledger.Entries) {
                    paid |= entry.Kind == LedgerKind.Fare && entry.RiderId == record.RiderId;
                }
                fareAtBoarding.Add(record.Spec.lookId + (paid ? " paid" : " unpaid"));
            };

            Time.timeScale = TimeScale;
            yield return NightDrive.ServeStop(night, "gas_station");
            CollectionAssert.AreEquivalent(new[] { "look03 paid", "look04 paid" }, fareAtBoarding, "a fare is credited as each rider boards");
            Assert.AreEqual(2 * balance.fareCents, ledger.Totals.Cents(LedgerKind.Fare));
            Assert.AreEqual(2, ledger.Totals.Count(LedgerKind.Fare));
            Assert.AreEqual(0, ledger.Totals.Count(LedgerKind.Tip));

            RiderRecord look04 = NightDrive.Rider(shift, "look04");
            yield return NightDrive.ArriveAt(night, "campground");
            Assert.IsTrue(shift.Doors.TryOpen());
            yield return FlowTestUtil.WaitFor(() => look04.Status == RiderStatus.Delivered, 60f, "look04 getting off at campground");
            StopRecord campground = shift.Progress.Find("campground");
            Assert.AreEqual(ArrivalRating.Early, campground.Rating,
                $"campground should be reached early: arrived {campground.ArrivalGameSeconds:0}, scheduled {campground.ScheduledGameSeconds:0}");
            yield return FlowTestUtil.WaitFor(() => ledger.Totals.Count(LedgerKind.Tip) == 1, 10f, "the tip");
            LedgerEntry tip = default;
            foreach (LedgerEntry entry in ledger.Entries) {
                if (entry.Kind == LedgerKind.Tip) {
                    tip = entry;
                }
            }
            Assert.AreEqual(look04.RiderId, tip.RiderId);
            Assert.AreEqual(EconomyMath.Tip(balance.fareCents, balance.tipPercent), tip.AmountCents);
            Assert.AreEqual(175, tip.AmountCents, "a 50 % tip on a $3.50 fare");
            Assert.AreEqual("campground", tip.StopId);
            // The night's money is in the ledger; the run's wallet takes it when the night completes
            Assert.AreEqual(0, night.Setup.Run.walletCents);
            Assert.AreEqual(ledger.Totals.NetCents, ledger.WalletNowCents);
        }
    }
}
