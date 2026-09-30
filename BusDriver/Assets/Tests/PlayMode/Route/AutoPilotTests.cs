using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.World;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Route {
    // AutoPilot (T-M2-10, §4.18): the whole of Route 1, depot to lodge, stopping at every stop
    public class AutoPilotTests {
        const float TimeScale = 3f;
        // "within 8 real minutes at timeScale 1 equivalent"
        const float MaxGameSeconds = 480f;
        const float MaxLateralOffset = 2.5f;

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

        [UnityTest, Timeout(900000)]
        public IEnumerator AutoPilot_DrivesFullRoute() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(20260930);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            RouteTracker tracker = night.Shift.Tracker;
            AutoPilot pilot = night.Shift.AutoPilot;
            BusController bus = night.Bus;
            BusCabin cabin = night.Shift.Cabin;
            RouteDefinition route = tracker.Route;
            Assert.IsNotNull(pilot, "Night_Systems has no AutoPilot");

            Time.timeScale = TimeScale;
            float start = Time.time;
            float worstOffset = 0f;
            float worstAt = 0f;
            foreach (RouteStop stop in route.stops) {
                pilot.StopAt(stop.stopId);
                while (!pilot.Arrived) {
                    yield return new WaitForFixedUpdate();
                    float offset = Mathf.Abs(tracker.Lateral - pilot.LaneOffset);
                    if (offset > worstOffset) {
                        worstOffset = offset;
                        worstAt = tracker.DistanceAlong;
                    }
                    Assert.Less(Time.time - start, MaxGameSeconds, $"still driving to {stop.stopId} after {MaxGameSeconds} s, at d={tracker.DistanceAlong:0}");
                }
                float settle = Time.time + 3f;
                while (!bus.IsStopped && Time.time < settle) {
                    yield return new WaitForFixedUpdate();
                }
                BusStop busStop = night.Route.Stop(stop.stopId);
                Assert.IsTrue(bus.IsStopped, $"the bus didn't come to a stop at {stop.stopId}");
                Assert.IsTrue(busStop.Contains(cabin), $"the door isn't in {stop.stopId}'s zone (bus at d={tracker.DistanceAlong:0.00})");
                Debug.Log($"[AUTOPILOT] {stop.stopId} at {Time.time - start:0.0} s, d={tracker.DistanceAlong:0.00}");
                pilot.Continue();
            }
            float duration = Time.time - start;
            Debug.Log($"[AUTOPILOT] depot → lodge in {duration:0.0} game-s, worst lateral offset {worstOffset:0.00} m at d={worstAt:0}");
            Assert.AreEqual(route.terminusStopId, route.stops[route.stops.Length - 1].stopId);
            Assert.LessOrEqual(worstOffset, MaxLateralOffset, $"left the lane by {worstOffset:0.00} m at d={worstAt:0}");
            Assert.AreEqual(0, tracker.RespawnCount, "the KillPlane respawned the bus");
        }
    }
}
