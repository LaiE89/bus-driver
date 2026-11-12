using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Tests.PlayMode.Flow;
using BusDriver.Tests.PlayMode.Passengers;
using BusDriver.UI.Dash;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Economy {
    // The fare box on the dash (T-M3-07, §2.7)
    public class FareBoxTests {
        const float TimeScale = 3f;

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

        [UnityTest, Timeout(600000)]
        public IEnumerator FareBox_DropOffPopsTheFare() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.DebugNoMonsters = true;
            game.Flow.NewRun(20260930);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            ShiftServices shift = night.Shift;
            FareBoxView fareBox = Object.FindAnyObjectByType<FareBoxView>();
            Assert.IsNotNull(fareBox, "Dash.prefab has no FareBoxView");
            Assert.AreEqual(shift.Bus.View.DashAnchor(fareBox.Screen), fareBox.Canvas.parent, "the fare box sits on Anchor_Dash_FareBox");
            Assert.AreEqual("$0.00", fareBox.TotalText);
            StringAssert.Contains("QUOTA", fareBox.QuotaText);

            Time.timeScale = TimeScale;
            yield return NightDrive.ServeStop(night, "farm_gate");
            Assert.AreEqual(0, shift.Ledger.Totals.Cents(LedgerKind.Fare), "boarding does not charge a fare");
            Assert.AreEqual("$0.00", fareBox.TotalText);

            float droppedAt = -1f;
            shift.Cabin.OnPassengerDropOff += passenger => {
                if (droppedAt < 0f) {
                    droppedAt = Time.unscaledTime;
                }
            };
            yield return NightDrive.ArriveAt(night, "campground");
            Assert.IsTrue(shift.Doors.TryOpen());
            yield return FlowTestUtil.WaitFor(() => droppedAt >= 0f, 60f, "the first rider stepping off");
            yield return null;
            Assert.LessOrEqual(fareBox.LastPopTime - droppedAt, 0.2f, "the pop shows within 0.2 s of the drop-off");
            Assert.GreaterOrEqual(fareBox.LastPopTime, droppedAt);
            Assert.AreEqual(Money.FormatDelta(shift.Balance.fareCents), fareBox.DeltaText);
            Assert.AreEqual("+$3.50", fareBox.DeltaText);
            Assert.IsTrue(fareBox.IsPopping);
            Assert.AreEqual(Money.Format(shift.Ledger.Totals.NetCents), fareBox.TotalText);
            StringAssert.Contains("QUOTA", fareBox.QuotaText);
        }
    }
}
