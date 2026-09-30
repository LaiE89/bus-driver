using System.Collections;
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
        public IEnumerator FareBox_BoardingPopsTheFare() {
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

            float boardedAt = -1f;
            shift.Cabin.OnPassengerBoarded += passenger => {
                if (boardedAt < 0f) {
                    boardedAt = Time.unscaledTime;
                }
            };
            Time.timeScale = TimeScale;
            yield return NightDrive.ArriveAt(night, "farm_gate");
            Assert.IsTrue(shift.Doors.TryOpen());
            yield return FlowTestUtil.WaitFor(() => boardedAt >= 0f, 60f, "the first rider boarding");
            yield return null;
            Assert.LessOrEqual(fareBox.LastPopTime - boardedAt, 0.2f, "the pop shows within 0.2 s of boarding");
            Assert.GreaterOrEqual(fareBox.LastPopTime, boardedAt);
            Assert.AreEqual(Money.FormatDelta(shift.Balance.fareCents), fareBox.DeltaText);
            Assert.AreEqual("+$3.50", fareBox.DeltaText);
            Assert.IsTrue(fareBox.IsPopping);
            Assert.AreEqual(Money.Format(shift.Ledger.Totals.NetCents), fareBox.TotalText);
        }
    }
}
