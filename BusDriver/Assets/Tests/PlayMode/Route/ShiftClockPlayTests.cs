using System.Collections;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Shift;
using BusDriver.Tests.PlayMode.Flow;
using BusDriver.UI.Dash;
using BusDriver.UI.Hud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Route {
    // The shift clock on the real night (T-M2-12, §2.5): it holds in Intro and while paused, runs
    // 6× in Driving, and the dash clock and the CCTV timestamp both show it
    public class ShiftClockPlayTests {
        string saveRoot;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        static IEnumerator WaitRealSeconds(float seconds) {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Clock_OnlyAdvancesInDriving() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(20260930);
            yield return FlowTestUtil.WaitForNight(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            ShiftClockDriver clock = night.Shift.Clock;
            Assert.IsNotNull(clock, "Night_Systems has no ShiftClockDriver");
            double start = night.Route.Route.schedule.shiftStartGameSeconds;

            // Intro: frozen at the shift start
            Assert.AreEqual(ShiftState.Intro, night.Director.State);
            yield return WaitRealSeconds(1f);
            Assert.AreEqual(ShiftState.Intro, night.Director.State);
            Assert.AreEqual(start, clock.NowGameSeconds, 1e-9, "the clock moved during the intro card");

            yield return FlowTestUtil.WaitForDriving(game);
            // Driving: 6 game-seconds per (scaled) second
            double clockBefore = clock.NowGameSeconds;
            float timeBefore = Time.time;
            yield return WaitRealSeconds(2f);
            double ran = clock.NowGameSeconds - clockBefore;
            float elapsed = Time.time - timeBefore;
            Assert.Greater(elapsed, 1f);
            Assert.AreEqual(6.0, ran / elapsed, 0.2, $"ran {ran:0.00} game-s in {elapsed:0.00} s");

            // Paused: frozen
            Assert.IsTrue(game.Pause.TrySetPaused(true));
            double atPause = clock.NowGameSeconds;
            yield return WaitRealSeconds(1f);
            Assert.AreEqual(atPause, clock.NowGameSeconds, 1e-9, "the clock moved while paused");
            Assert.IsTrue(game.Pause.TrySetPaused(false));
            yield return WaitRealSeconds(0.5f);
            Assert.Greater(clock.NowGameSeconds, atPause, "the clock runs again after the pause");

            // The dash clock and the CCTV timestamp read the same clock
            DashClockView dash = Object.FindAnyObjectByType<DashClockView>();
            Assert.IsNotNull(dash, "no dash clock");
            Assert.AreEqual(clock.Format(ClockFormat.Dash), dash.Text);
            Assert.AreSame(night.Shift.Bus.View.DashAnchor(Core.Data.DashScreen.Clock), dash.Canvas.parent, "the dash clock sits on its anchor");
            night.Shift.Cctv.Cycle();
            yield return null;
            yield return null;
            CctvOverlayView overlay = Object.FindAnyObjectByType<CctvOverlayView>();
            string expected = clock.Format(ClockFormat.Cctv);
            Assert.IsTrue(overlay.Timestamp == expected || overlay.Timestamp.Length == expected.Length,
                $"CCTV shows '{overlay.Timestamp}', the clock is '{expected}'");
            StringAssert.EndsWith("AM", overlay.Timestamp);
            StringAssert.StartsWith(expected.Substring(0, 5), overlay.Timestamp, "the CCTV timestamp follows the shift clock");
            night.Shift.Cctv.ShowHome();
        }
    }
}
