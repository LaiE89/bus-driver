using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Passengers;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // Out of the seat, up the aisle, then the last step to the driver. True once it is close enough to kill.
    static class AisleRush {
        public static bool Step(Passenger passenger, BusCabin cabin, float metresPerSecond, float reachMetres, ref float travelled) {
            if (passenger == null || cabin == null || passenger.State != PassengerState.Seated) {
                return false;
            }
            Vector3 world = cabin.StandPointWorld;
            Transform space = passenger.transform.parent != null ? passenger.transform.parent : cabin.transform;
            Vector3 target = space.InverseTransformPoint(world);
            Vector3 aisle = space.InverseTransformPoint(cabin.PassengerRoot.TransformPoint(cabin.AisleAtDoorLocal));
            Vector3 here = passenger.transform.localPosition;
            bool inAisle = Mathf.Abs(here.x - aisle.x) <= 0.2f && Mathf.Abs(here.y - aisle.y) <= 0.25f;
            bool beside = inAisle && Mathf.Abs(here.z - target.z) <= 0.2f;
            Vector3 step = !inAisle
                ? new Vector3(aisle.x, aisle.y, here.z)
                : (beside ? target : new Vector3(aisle.x, aisle.y, target.z));
            Vector3 before = passenger.transform.position;
            passenger.RushStep(step, metresPerSecond);
            travelled += Vector3.Distance(before, passenger.transform.position);
            return Vector3.Distance(passenger.transform.position, world) <= reachMetres;
        }

        // How many full metres of travel are waiting, and drop them from the accumulator.
        public static int StepsOf(ref float travelled, float metres = 1f) {
            if (metres <= 0.01f) {
                return 0;
            }
            int count = Mathf.FloorToInt(travelled / metres);
            if (count <= 0) {
                return 0;
            }
            travelled -= count * metres;
            return count;
        }
    }
}
