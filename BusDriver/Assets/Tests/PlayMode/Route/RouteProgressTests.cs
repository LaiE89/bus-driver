using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Route;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Route {
    // RouteProgress (T-M2-11, §2.4) on the real route, driven by AutoPilot
    public class RouteProgressTests {
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

        // Drives to the stop with AutoPilot, opens the doors and waits until they're fully open
        static IEnumerator ServeStop(ShiftContext night, string stopId) {
            AutoPilot pilot = night.Shift.AutoPilot;
            BusController bus = night.Bus;
            BusDoors doors = night.Shift.Doors;
            pilot.StopAt(stopId);
            yield return FlowTestUtil.WaitFor(() => pilot.Arrived, 240f, "AutoPilot arriving at " + stopId);
            yield return FlowTestUtil.WaitFor(() => bus.IsStopped, 10f, "the bus stopping at " + stopId);
            Assert.IsTrue(doors.TryOpen(), "the doors wouldn't open at " + stopId);
            yield return FlowTestUtil.WaitFor(() => doors.IsFullyOpen, 10f, "the doors opening at " + stopId);
            yield return null;
            doors.TryClose();
            yield return FlowTestUtil.WaitFor(() => doors.IsClosed, 10f, "the doors closing at " + stopId);
            pilot.Continue();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Stops_ServedAndMissed() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(20260930);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            RouteProgress progress = night.Shift.Progress;
            Assert.IsNotNull(progress, "Night_Systems has no RouteProgress");
            List<string> events = new List<string>();
            progress.OnServed += record => events.Add("served " + record.StopId);
            progress.OnMissed += record => events.Add("missed " + record.StopId);
            Assert.AreEqual("farm_gate", progress.Next.StopId);

            Time.timeScale = TimeScale;
            yield return ServeStop(night, "farm_gate");
            Assert.AreEqual(StopState.Served, progress.Find("farm_gate").State);
            Assert.AreEqual("gas_station", progress.Next.StopId);
            // Straight past gas_station (800 m) to campground (1250 m)
            yield return ServeStop(night, "campground");

            CollectionAssert.AreEqual(new[] { "served farm_gate", "missed gas_station", "served campground" }, events);
            Assert.AreEqual(StopState.Missed, progress.Find("gas_station").State);
            Assert.AreEqual(ArrivalRating.Missed, progress.Find("gas_station").Rating);
            Assert.AreEqual(StopState.Served, progress.Find("campground").State);
            // Arrivals are game-seconds on the shift clock (T-M2-12)
            StopRecord farm = progress.Find("farm_gate");
            Assert.Greater(farm.ArrivalGameSeconds, night.Route.Route.schedule.shiftStartGameSeconds);
            Assert.LessOrEqual(farm.ArrivalGameSeconds, night.Shift.Clock.NowGameSeconds);
            Assert.AreNotEqual(ArrivalRating.None, farm.Rating);
            Assert.AreEqual("church", progress.Next.StopId);
            Assert.AreEqual("church", night.Shift.Tracker.NextStop.stopId, "the tracker's next stop follows RouteProgress");
            Assert.IsFalse(progress.TerminusReached);
        }
    }
}
