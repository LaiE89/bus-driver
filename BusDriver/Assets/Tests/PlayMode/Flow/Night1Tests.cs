using System.Collections;
using System.Text;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Shift;
using BusDriver.Tests.PlayMode.Passengers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Flow {
    // Night 1 end to end with AutoPilot (T-M3-09, §2.19, §2.7): every stop served, every rider
    // delivered, the Summary's money matching the manifest and the ratings
    public class Night1Tests {
        const int Seed = 20260930;
        static readonly string[] Stops = { "farm_gate", "gas_station", "campground" };
        const string EndStop = "church";

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

        // AutoPilot serves farm_gate, gas_station and campground, then opens the doors at the church
        static IEnumerator DriveNight1(ShiftContext night) {
            foreach (string stop in Stops) {
                yield return NightDrive.ServeStop(night, stop);
            }
            yield return NightDrive.ArriveAt(night, EndStop);
            Assert.IsTrue(night.Shift.Doors.TryOpen(), "the doors wouldn't open at the church");
            yield return FlowTestUtil.WaitFor(() => night.Shift.Director.State == ShiftState.Summary, 30f, "the Summary");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Night1_NoMonsters_AllDelivered_LedgerMatches() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.DebugNoMonsters = true;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            ShiftServices shift = night.Shift;
            Time.timeScale = 3f;
            yield return DriveNight1(night);
            Time.timeScale = 1f;

            // The expectation comes from the manifest and the ratings the drive produced, not from
            // hard-coded numbers
            BalanceConfig balance = shift.Balance;
            NightDefinition definition = shift.Night;
            int riders = 0;
            int expectedTips = 0;
            foreach (RiderSpec spec in definition.scripted) {
                if (spec.IsMonster) {
                    continue;
                }
                riders++;
                StopRecord destination = shift.Progress.Find(spec.destinationStopId);
                Assert.AreEqual(StopState.Served, destination.State, spec.destinationStopId + " was served");
                if (destination.Rating == ArrivalRating.Early) {
                    expectedTips += EconomyMath.Tip(balance.fareCents, balance.tipPercent);
                }
            }
            Assert.AreEqual(5, riders, "night 1 has five human riders (§2.19)");
            foreach (RiderRecord record in shift.Riders.All) {
                Assert.AreEqual(RiderStatus.Delivered, record.Status, record.Spec + " wasn't delivered");
                Assert.AreEqual(record.Spec.destinationStopId, record.ExitStopId, record.Spec + " got off at the wrong stop");
                Assert.IsFalse(record.Retargeted, "no stop was missed");
            }

            NightResult result = shift.Director.Result;
            int expected = riders * balance.fareCents + expectedTips;
            Assert.AreEqual(riders * balance.fareCents, result.totals.Cents(LedgerKind.Fare));
            Assert.AreEqual(riders, result.totals.Count(LedgerKind.Fare));
            Assert.AreEqual(expectedTips, result.totals.Cents(LedgerKind.Tip));
            Assert.AreEqual(0, result.totals.Cents(LedgerKind.Refund) + result.totals.Cents(LedgerKind.Lost) + result.totals.Cents(LedgerKind.Bounty));
            Assert.AreEqual(expected, result.NetCents);
            Assert.AreEqual(riders, result.stats.ridersDelivered);
            Assert.AreEqual(expected, result.WalletAfterCents);
            foreach (NightArrival arrival in result.arrivals) {
                Assert.AreNotEqual(ArrivalRating.Missed, arrival.rating, arrival.stopId + " was missed");
                Assert.AreNotEqual(ArrivalRating.None, arrival.rating, arrival.stopId + " has no rating");
            }
            Debug.Log($"[NIGHT1] no-monster ledger: {riders} fares, tips {expectedTips}, total {expected}");
        }

        // D43: night 1 aims at about 5 minutes. This reports its length at timeScale 1, with every
        // rider, and never fails on it. AutoPilot never looks at the Starer, which reaches Lethal
        // about 20 s after it sits down at gas_station (D106), so its kills are spared (as god mode
        // would) rather than ending the drive early; each spare sets it back to 60, so it
        // telegraphs again every few seconds.
        [UnityTest, Timeout(1200000)]
        public IEnumerator Night1_DurationReport() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForNight(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            ShiftDirector director = night.Shift.Director;
            yield return FlowTestUtil.WaitFor(() => director.State == ShiftState.Driving, 30f, "the Driving state");
            SparingPreventer spared = new SparingPreventer();
            night.Shift.Death.AddPreventer(spared);
            Time.timeScale = 1f;
            yield return DriveNight1(night);

            float seconds = director.NightSeconds;
            Debug.Log($"[NIGHT1] duration={seconds:0.0}s (intro to summary, timeScale 1, AutoPilot serving every stop; Starer kills spared: {spared.Count})");
            StringBuilder overlay = new StringBuilder();
            foreach (IDebugSection section in night.Shift.Debug.Sections) {
                if (section.Title == "Run") {
                    section.Write(overlay);
                }
            }
            StringAssert.Contains("night time", overlay.ToString(), "the F1 overlay shows the night's length");
            Assert.Greater(seconds, 0f);
        }
    }
}
