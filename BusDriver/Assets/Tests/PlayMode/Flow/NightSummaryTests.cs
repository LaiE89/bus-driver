using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Save;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Shift;
using BusDriver.Tests.PlayMode.Passengers;
using BusDriver.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Flow {
    // The shift's Summary and the step to the next night (T-M3-06, §2.1, §2.21, §4.4)
    public class NightSummaryTests {
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

        // Serve farm_gate (look01 → campground, look02 → church), then straight to the church: the
        // doors opening there end the night. The Summary shows the ledger (two fares, a tip for look02
        // if the church was reached Early, none for look01 whose stop was missed); Continue saves the
        // run and loads night 2 with the new wallet.
        [UnityTest, Timeout(900000)]
        public IEnumerator Night_CompletesToSummary() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.DebugNoMonsters = true;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            ShiftServices shift = night.Shift;
            ShiftDirector director = shift.Director;
            BalanceConfig balance = shift.Balance;

            Time.timeScale = TimeScale;
            yield return NightDrive.ServeStop(night, "farm_gate");
            yield return NightDrive.ArriveAt(night, "church");
            Assert.AreEqual(ShiftState.Driving, director.State, "the night isn't over before the end stop's doors open");
            Assert.IsTrue(shift.Doors.TryOpen());
            yield return FlowTestUtil.WaitFor(() => director.State == ShiftState.Summary, 20f, "the Summary");
            Time.timeScale = 1f;

            StopRecord church = shift.Progress.Find("church");
            int tip = church.Rating == ArrivalRating.Early ? EconomyMath.Tip(balance.fareCents, balance.tipPercent) : 0;
            int expected = 2 * balance.fareCents + tip;
            NightResult result = director.Result;
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.nightIndex);
            Assert.AreEqual(2 * balance.fareCents, result.totals.Cents(LedgerKind.Fare));
            Assert.AreEqual(tip, result.totals.Cents(LedgerKind.Tip), "only look02 can earn a tip; look01 was carried from a missed stop");
            Assert.AreEqual(expected, result.NetCents);
            Assert.AreEqual(2, result.stats.ridersDelivered);
            Assert.AreEqual(4, result.arrivals.Count, "night 1's four stops, farm_gate to church");
            Assert.AreEqual(ArrivalRating.Missed, result.arrivals[1].rating);
            Assert.AreEqual(ArrivalRating.Missed, result.arrivals[2].rating);
            Assert.AreNotEqual(ArrivalRating.None, result.arrivals[3].rating);

            // The screen is up, focused on Continue, and shows the same numbers
            SummaryScreen summary = Object.FindAnyObjectByType<SummaryScreen>();
            Assert.IsNotNull(summary, "Screens.prefab has no SummaryScreen");
            Assert.IsTrue(summary.IsOpen, "the Summary screen didn't open");
            StringAssert.Contains(Money.FormatDelta(expected), summary.LedgerAmountsText);
            StringAssert.Contains(Money.Format(0), summary.WalletText);
            StringAssert.Contains(Money.Format(expected), summary.WalletText);
            Assert.AreEqual(summary.ContinueButton.gameObject, UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, "Continue has focus");
            Assert.AreEqual(InputContext.Screen, game.Input.Context);
            Assert.IsFalse(game.Pause.IsPauseAllowed, "no pausing in the Summary (§4.11)");
            Assert.AreEqual(ShiftState.Summary, director.State);

            // RunFlow has already applied the night to the run (§4.4)
            RunState run = game.Flow.Run;
            Assert.AreEqual(expected, run.walletCents);
            Assert.AreEqual(2, run.nightIndex);
            Assert.AreEqual(1, run.nights.Count);
            Assert.AreEqual(2, run.stats.ridersDelivered);
            Assert.IsFalse(run.nightInProgress);
            CaptureUtil.CaptureWithCanvas("summary", shift.DriverCamera, summary.GetComponentInParent<Canvas>().rootCanvas);

            summary.ContinueButton.onClick.Invoke();
            yield return FlowTestUtil.WaitFor(() => game.Flow.State == RunFlowState.LoadingNight || game.Scenes.IsLoading, 10f, "night 2 loading");
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext next = Object.FindAnyObjectByType<ShiftContext>();
            Assert.AreEqual(2, next.Setup.NightIndex);
            Assert.AreEqual(expected, next.Shift.Ledger.WalletBeforeCents, "night 2 starts with the new wallet");
            Assert.IsTrue(game.Saves.TryLoad(SaveSlot.Run, out RunState saved), "Continue saves run.json");
            Assert.AreEqual(2, saved.nightIndex);
            Assert.AreEqual(expected, saved.walletCents);
        }
    }
}
