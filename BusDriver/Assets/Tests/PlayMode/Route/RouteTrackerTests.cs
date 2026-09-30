using System.Collections;
using System.Text.RegularExpressions;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Route;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Route {
    // RouteTracker (T-M2-09, §4.6) and the KillPlane respawn (§2.3)
    public class RouteTrackerTests {
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

        static IEnumerator StartDriving(GameServices game) {
            game.Flow.NewRun(20260930);
            yield return FlowTestUtil.WaitForDriving(game);
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator Tracker_FollowsBus() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            yield return StartDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            RouteTracker tracker = night.Shift.Tracker;
            BusController bus = night.Bus;
            bus.GetComponent<BusInput>().ExternalControl = true;

            float start = tracker.DistanceAlong;
            Assert.AreEqual(night.Route.Route.depotSpawnDistance, start, 0.5f, "the tracker should start at the spawn marker");
            Assert.AreEqual("farm_gate", tracker.NextStop.stopId);
            float last = start;
            float until = Time.time + 8f;
            while (Time.time < until) {
                bus.SetInput(0f, 1f, false);
                yield return new WaitForFixedUpdate();
                Assert.GreaterOrEqual(tracker.DistanceAlong, last - 0.001f, "the distance went backwards while driving forward");
                last = tracker.DistanceAlong;
            }
            bus.SetInput(0f, 0f, false);
            Assert.Greater(last - start, 20f, "the bus should have covered some road in 8 s");
            Assert.AreEqual(last / tracker.Path.TotalLength, tracker.Progress01, 1e-4f);
            Assert.AreEqual(night.Route.Route.stops[0].distance - last, tracker.DistanceToNextStop, 0.01f);
            Assert.Greater(tracker.AverageSpeed, 1f, "the moving-average speed should follow the drive");
            Assert.IsFalse(float.IsInfinity(tracker.EtaGameSeconds), "a moving bus has an ETA");
        }

        [UnityTest]
        public IEnumerator KillPlane_Respawns() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            yield return StartDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            RouteTracker tracker = night.Shift.Tracker;
            BusController bus = night.Bus;
            bus.GetComponent<BusInput>().ExternalControl = true;
            bus.SetInput(0f, 0f, false);

            // Below the map at 600 m, far from the cliff, inside the KillPlane's trigger
            RoutePose pose = tracker.Path.Evaluate(600f);
            float killPlaneY = night.Route.Route.generation.killPlaneY;
            LogAssert.Expect(LogType.Error, new Regex(@"containment breach at d="));
            bus.PlaceAt(new Vector3(pose.Position.x, killPlaneY - 1.5f, pose.Position.z), pose.Rotation);

            float started = Time.time;
            yield return FlowTestUtil.WaitFor(() => tracker.RespawnCount == 1, 5f, "the respawn");
            Assert.LessOrEqual(Time.time - started, 2f, "the bus should be back within 2 s");
            yield return new WaitForFixedUpdate();

            Vector3 position = bus.transform.position;
            RouteProjection projection = tracker.Path.Project(position);
            Assert.AreEqual(600f, projection.Distance, 5f, "respawned at the nearest road point");
            Assert.AreEqual(night.Route.Route.roadWidth * 0.25f, projection.Lateral, 0.3f, "respawned in the right lane");
            Assert.AreEqual(tracker.Path.ElevationAt(projection.Distance), position.y, 1f, "respawned on the road, not below it");
            Assert.Greater(Vector3.Dot(bus.transform.forward, tracker.Path.Evaluate(projection.Distance).Forward), 0.99f, "facing the route direction");
            Assert.Greater(Vector3.Dot(bus.transform.up, Vector3.up), 0.99f, "upright");

            // The fade comes back
            yield return FlowTestUtil.WaitFor(() => night.Shift.Fade.Alpha == 0f, 3f, "the fade back in");
        }
    }
}
