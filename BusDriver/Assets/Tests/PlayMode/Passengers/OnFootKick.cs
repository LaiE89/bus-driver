using System.Collections;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Tests.PlayMode.Flow;
using NUnit.Framework;
using UnityEngine;

namespace BusDriver.Tests.PlayMode.Passengers {
    // The kick flow as a player does it (§2.13): leave the seat at a complete stop, walk down the
    // aisle with OnFootController.ExternalControl, face the rider within reach, and use the alt
    // interact (Kick) on what the interactor offers
    static class OnFootKick {
        // Stops a row ahead of the rider, so the rider is in reach and in front
        const float StandAhead = 0.9f;
        const float WalkTimeout = 30f;

        public static IEnumerator WalkUpAndKick(ShiftServices shift, Passenger rider) {
            Assert.IsTrue(shift.Bus.IsStopped, "kicking needs a complete stop");
            Assert.IsTrue(shift.Mode.TryLeaveSeat(), "couldn't leave the seat");
            OnFootController onFoot = shift.OnFoot;
            onFoot.ExternalControl = true;
            Transform bus = shift.Bus.transform;

            // The rig starts at StandPoint facing the back of the bus, so forward is down the aisle
            float stopAt = bus.InverseTransformPoint(rider.transform.position).z + StandAhead;
            float giveUp = Time.realtimeSinceStartup + WalkTimeout;
            while (bus.InverseTransformPoint(onFoot.transform.position).z > stopAt) {
                Assert.Less(Time.realtimeSinceStartup, giveUp, "the walk down the aisle got stuck");
                onFoot.ExternalMove = new Vector2(0f, 1f);
                yield return null;
            }
            onFoot.ExternalMove = Vector2.zero;

            // Look at them: a scripted turn of the head, the way the mouse would
            Vector3 standLocal = bus.InverseTransformPoint(onFoot.transform.position);
            Vector3 riderLocal = bus.InverseTransformPoint(rider.transform.position);
            Vector3 toRider = bus.TransformDirection(new Vector3(riderLocal.x - standLocal.x, 0f, riderLocal.z - standLocal.z));
            onFoot.gameObject.SetActive(false);
            onFoot.Place(onFoot.transform.position, Quaternion.LookRotation(toRider).eulerAngles.y);
            onFoot.gameObject.SetActive(true);

            PlayerInteractor interactor = shift.Interactor;
            yield return FlowTestUtil.WaitFor(() => ReferenceEquals(interactor.Current, rider), 5f, "the interactor to offer the rider");
            Assert.AreEqual("Talk", interactor.CurrentPrompt);
            Assert.AreEqual("Kick out", interactor.CurrentAltPrompt);
            interactor.Current.AltInteract();
            Assert.IsTrue(rider.WasKicked, "the kick didn't take");
        }

        // The kicked rider is out of the door and gone, the doors shut again, and the player back
        // in the seat
        public static IEnumerator WaitGoneAndSitDown(ShiftServices shift, Passenger rider) {
            yield return FlowTestUtil.WaitFor(() => rider == null, 60f, "the kicked rider to leave");
            yield return FlowTestUtil.WaitFor(() => shift.Doors.IsClosed, 20f, "the doors to close after the kick");
            shift.OnFoot.ExternalControl = false;
            Assert.IsTrue(shift.Mode.TrySitDown(), "couldn't sit back down");
        }
    }
}
