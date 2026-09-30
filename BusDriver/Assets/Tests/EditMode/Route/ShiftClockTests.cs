using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Route {
    // The shift clock core (§2.5, T-M2-12)
    public class ShiftClockTests {
        [Test]
        public void StartsAtTheShiftStart() {
            ShiftClock clock = new ShiftClock(1800.0, 6.0);
            Assert.AreEqual(1800.0, clock.NowGameSeconds);
            Assert.AreEqual(0.0, clock.ElapsedRealSeconds);
            Assert.AreEqual("12:30 AM", clock.Format(ClockFormat.Dash));
            Assert.AreEqual("12:30:00 AM", clock.Format(ClockFormat.Cctv));
        }

        [Test]
        public void RunsSixTimesRealTimeOnlyWhileDriving() {
            ShiftClock clock = new ShiftClock(1800.0, 6.0);
            clock.Tick(10.0, false);
            Assert.AreEqual(1800.0, clock.NowGameSeconds, "frozen outside Driving");
            clock.Tick(10.0, true);
            Assert.AreEqual(1860.0, clock.NowGameSeconds, 1e-9);
            Assert.AreEqual(10.0, clock.ElapsedRealSeconds, 1e-9);
            clock.Tick(0.0, true);
            clock.Tick(-5.0, true);
            Assert.AreEqual(1860.0, clock.NowGameSeconds, 1e-9, "a paused (zero) or negative frame never moves it");
        }

        [Test]
        public void ManySmallFramesAddUp() {
            ShiftClock clock = new ShiftClock(1800.0, 6.0);
            for (int i = 0; i < 60 * 60; i++) {
                clock.Tick(1.0 / 60.0, true);
            }
            Assert.AreEqual(1800.0 + 360.0, clock.NowGameSeconds, 1e-6);
        }

        [Test]
        public void ReachesTheLodgeScheduleFormat() {
            // 5213.3 game-s is the lodge's scheduled time (§3.2)
            ShiftClock clock = new ShiftClock(1800.0, 6.0);
            clock.Tick((5213.3 - 1800.0) / 6.0, true);
            Assert.AreEqual("01:26 AM", clock.Format(ClockFormat.Dash));
            Assert.AreEqual("01:26:53 AM", clock.Format(ClockFormat.Cctv));
        }

        [Test]
        public void ANonPositiveRateFallsBackToRealTime() {
            ShiftClock clock = new ShiftClock(0.0, 0.0);
            clock.Tick(2.0, true);
            Assert.AreEqual(2.0, clock.NowGameSeconds, 1e-9);
        }
    }
}
