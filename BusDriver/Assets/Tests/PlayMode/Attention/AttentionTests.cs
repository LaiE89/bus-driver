using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Attention;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Shift;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using AllocIs = UnityEngine.TestTools.Constraints.Is;

namespace BusDriver.Tests.PlayMode.Attention {
    // Observer evaluation (T-M4-01, §2.8): the live observer, range, viewport and occluders, and
    // the road-yaw rule
    public class AttentionTests {
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

        static IEnumerator Frames(int count) {
            for (int i = 0; i < count; i++) {
                yield return null;
            }
        }

        static IEnumerator StartNight(string saveRoot, ShiftServices[] result) {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForNight(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            if (night.Director.State != ShiftState.Driving) {
                Assert.AreEqual(AttentionMode.None, night.Shift.Attention.Mode, "no attention outside Driving");
            }
            yield return FlowTestUtil.WaitForDriving(game);
            result[0] = night.Shift;
        }

        // Where a seated rider's head will be (Passenger.PlaceSeated, §4.14)
        static Vector3 SeatedHead(BusCabin cabin, BusSeat seat) {
            Transform root = cabin.PassengerRoot;
            return root.TransformPoint(cabin.SeatLocal(seat)) + root.up * Passenger.SeatedHeadHeight;
        }

        // A free seat CAM 2 frames within its range, and too far from CAM 1 for it to count
        static BusSeat SeatOnlyCam2Counts(ShiftServices shift) {
            CctvCamera cam1 = shift.Cctv.Cameras[0];
            CctvCamera cam2 = shift.Cctv.Cameras[1];
            foreach (BusSeat seat in shift.Cabin.Seats) {
                Vector3 head = SeatedHead(shift.Cabin, seat);
                if (seat.IsFree && PlayerAttention.Sees(cam2.Camera, head, cam2.ObserveRange)
                    && (head - cam1.transform.position).magnitude > cam1.ObserveRange + 0.5f) {
                    return seat;
                }
            }
            return null;
        }

        static void ShowCamera(CCTVSystem cctv, int index) {
            cctv.ShowHome();
            while (cctv.ActiveIndex != index) {
                cctv.Cycle();
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Attention_ObservedByCctvWhenInFrame() {
            ShiftServices[] started = new ShiftServices[1];
            yield return StartNight(saveRoot, started);
            ShiftServices shift = started[0];
            PlayerAttention attention = shift.Attention;
            CCTVSystem cctv = shift.Cctv;

            BusSeat seat = SeatOnlyCam2Counts(shift);
            Assert.IsNotNull(seat, "no seat in CAM 2's frame beyond CAM 1's range");
            Passenger rider = shift.DebugRiders.SpawnSeated(new RiderSpec { lookId = "look01", boardStopId = "depot", destinationStopId = "church" }, seat);
            Assert.IsNotNull(rider);
            Assert.Less(Vector3.Distance(SeatedHead(shift.Cabin, seat), rider.Head.position), 0.01f);

            yield return Frames(2);
            Assert.AreEqual(AttentionMode.Road, attention.Mode);
            Assert.IsFalse(attention.IsObserved(rider, ObserverKinds.Cctv), "home view: no CCTV observer");

            ShowCamera(cctv, 1);
            yield return Frames(2);
            Assert.AreEqual(AttentionMode.Cctv, attention.Mode);
            Assert.AreEqual(1, attention.CctvIndex);
            Assert.AreEqual(ObserverKinds.Cctv, attention.ObservedBy(rider), "CAM 2 frames the rider within 7 m");
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(attention.TimeObserved(rider), 0.4f);
            Assert.AreEqual(0f, attention.TimeSinceObserved(rider));

            ShowCamera(cctv, 0);
            yield return Frames(2);
            Assert.AreEqual(ObserverKinds.None, attention.ObservedBy(rider), "CAM 1 is out of range");
            Assert.AreEqual(0f, attention.TimeObserved(rider));
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(attention.TimeSinceObserved(rider), 0.25f);
            Assert.Greater(attention.TimeSinceObserved(rider, ObserverKinds.Cctv), 0.25f);
            // Never seen on foot: counted from when the rider was first tracked, so the longest wait
            Assert.Greater(attention.TimeSinceObserved(rider, ObserverKinds.OnFoot), attention.TimeSinceObserved(rider, ObserverKinds.Cctv) + 0.4f);
            Assert.AreEqual(float.PositiveInfinity, attention.TimeSinceObserved(null), "an untracked rider has never been seen");

            ShowCamera(cctv, 1);
            yield return Frames(2);
            Assert.IsTrue(attention.IsObserved(rider, ObserverKinds.Cctv), "back on CAM 2");
            cctv.ShowHome();
            yield return Frames(2);
            Assert.IsFalse(attention.IsObserved(rider, ObserverKinds.Cctv));
            Assert.Greater(attention.TotalTimeObserved(rider), 0.4f);
        }

        // §4.1 rule 14: the evaluation runs every frame for every rider, so it must not allocate
        [UnityTest, Timeout(300000)]
        public IEnumerator Attention_EvaluationAllocatesNothing() {
            ShiftServices[] started = new ShiftServices[1];
            yield return StartNight(saveRoot, started);
            ShiftServices shift = started[0];
            for (int i = 0; i < 4; i++) {
                shift.DebugRiders.SpawnSeated(new RiderSpec { lookId = "look0" + (i + 1), boardStopId = "depot", destinationStopId = "church" });
            }
            ShowCamera(shift.Cctv, 1);
            yield return Frames(2);
            PlayerAttention attention = shift.Attention;
            attention.Tick(0.016f);

            // The constraint itself catches a real allocation
            Assert.That(() => { new object().GetHashCode(); }, AllocIs.AllocatingGCMemory());
            Assert.That(() => {
                for (int i = 0; i < 200; i++) {
                    attention.Tick(0.016f);
                }
            }, AllocIs.Not.AllocatingGCMemory());
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Attention_OccluderBlocks() {
            ShiftServices[] started = new ShiftServices[1];
            yield return StartNight(saveRoot, started);
            ShiftServices shift = started[0];
            PlayerAttention attention = shift.Attention;

            // The bus carries the hull shell and the driver partition as Occluder triggers, and
            // nothing a rider or a seat is made of is on that layer (§2.8, §4.16)
            int occluders = 0;
            foreach (Collider collider in shift.Bus.GetComponentsInChildren<Collider>(true)) {
                if (collider.gameObject.layer == Layers.Occluder) {
                    occluders++;
                    Assert.IsTrue(collider.isTrigger, collider.name + " must be a trigger");
                    Assert.IsNull(collider.GetComponentInParent<BusSeat>(), collider.name + " is part of a seat");
                    Assert.IsNull(collider.GetComponentInParent<Passenger>(), collider.name + " is part of a rider");
                }
            }
            Assert.AreEqual(7, occluders, "left, right, roof, floor, front, rear and the driver partition");

            BusSeat seat = SeatOnlyCam2Counts(shift);
            Passenger rider = shift.DebugRiders.SpawnSeated(new RiderSpec { lookId = "look02", boardStopId = "depot", destinationStopId = "church" }, seat);
            foreach (Collider collider in rider.GetComponentsInChildren<Collider>(true)) {
                Assert.AreNotEqual(Layers.Occluder, collider.gameObject.layer, "riders never occlude");
            }
            ShowCamera(shift.Cctv, 1);
            yield return Frames(2);
            Assert.IsTrue(attention.IsObserved(rider, ObserverKinds.Cctv), "in frame, in range, nothing between");

            // A wall of Occluder halfway along the line of sight
            Vector3 from = shift.Cctv.Cameras[1].transform.position;
            Vector3 to = rider.Head.position;
            GameObject wall = new GameObject("Test Occluder") { layer = Layers.Occluder };
            wall.transform.SetPositionAndRotation((from + to) * 0.5f, Quaternion.LookRotation(to - from));
            BoxCollider box = wall.AddComponent<BoxCollider>();
            box.size = new Vector3(0.6f, 0.6f, 0.05f);
            box.isTrigger = true;
            yield return Frames(2);
            Assert.IsFalse(attention.IsObserved(rider, ObserverKinds.Cctv), "the occluder hides the head");

            // Anything else in the way (a Default-layer collider, like a seat back) doesn't. It
            // stays a trigger so it can't push the bus.
            wall.layer = Layers.Default;
            yield return Frames(2);
            Assert.IsTrue(attention.IsObserved(rider, ObserverKinds.Cctv), "only the Occluder layer blocks");
            Object.Destroy(wall);

            // Anyone standing outside at the kerb is behind the hull shell
            Vector3 outside = shift.Bus.transform.TransformPoint(new Vector3(3.5f, 2f, -2f));
            Assert.IsTrue(Physics.Linecast(from, outside, Layers.Mask(Layers.Occluder), QueryTriggerInteraction.Collide),
                "the side wall stands between the cabin cameras and the kerb");
            Assert.IsFalse(PlayerAttention.Sees(shift.Cctv.Cameras[1].Camera, outside, 7f));
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Attention_RoadYaw() {
            ShiftServices[] started = new ShiftServices[1];
            yield return StartNight(saveRoot, started);
            ShiftServices shift = started[0];
            PlayerAttention attention = shift.Attention;

            shift.DriverLook.SetLook(0f, 0f);
            yield return Frames(2);
            Assert.AreEqual(AttentionMode.Road, attention.Mode);
            Assert.AreEqual(0f, attention.DriverYaw, 0.5f);
            Assert.IsTrue(attention.AttentionOnRoad);
            Assert.AreEqual(ObserverKinds.Driver, attention.LiveObservers);

            shift.DriverLook.SetLook(30f, -10f);
            yield return Frames(2);
            Assert.AreEqual(30f, attention.DriverYaw, 0.5f);
            Assert.IsTrue(attention.AttentionOnRoad, "30° is inside the 35° tolerance");

            shift.DriverLook.SetLook(40f, 0f);
            yield return Frames(2);
            Assert.IsFalse(attention.AttentionOnRoad, "turning the head 40° takes the eyes off the road");
            shift.DriverLook.SetLook(-40f, 0f);
            yield return Frames(2);
            Assert.AreEqual(-40f, attention.DriverYaw, 0.5f);
            Assert.IsFalse(attention.AttentionOnRoad);

            // Watching a feed is never eyes on the road, whatever the head does
            shift.DriverLook.SetLook(0f, 0f);
            ShowCamera(shift.Cctv, 0);
            yield return Frames(2);
            Assert.AreEqual(AttentionMode.Cctv, attention.Mode);
            Assert.IsFalse(attention.AttentionOnRoad);
            shift.Cctv.ShowHome();
            yield return Frames(2);
            Assert.IsTrue(attention.AttentionOnRoad);

            // On foot the on-foot camera is the only observer
            yield return FlowTestUtil.WaitFor(() => shift.Bus.IsStopped, 30f, "the bus standing still");
            Assert.IsTrue(shift.Mode.TryLeaveSeat(), "the bus is parked at the depot");
            yield return Frames(2);
            Assert.AreEqual(AttentionMode.OnFoot, attention.Mode);
            Assert.IsFalse(attention.AttentionOnRoad);
            Assert.AreEqual(ObserverKinds.OnFoot, attention.LiveObservers);
            Assert.IsTrue(shift.Mode.TrySitDown());
            yield return Frames(2);
            Assert.AreEqual(AttentionMode.Road, attention.Mode);
        }
    }
}
