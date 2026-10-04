using BusDriver.Core.Data;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.World;
using UnityEngine;

namespace BusDriver.Gameplay.Debug {
    // A development and test hook (T-M2-07): puts extra riders at a stop, or straight into a seat,
    // registered with the PassengerRegistry like any manifest rider. A rider spec with a monsterId
    // gets that monster's generated prefab (T-M4-03), exactly as the manifest spawns it. The smoke
    // test boards and kicks through it, tests stage stops with it, and the F1 overlay offers it as
    // a cheat.
    public sealed class DebugRiders : MonoBehaviour {
        ShiftServices shift;
        int spawned;

        // ShiftContext, beside ManifestSpawner in the Init order (§4.5 step 14)
        public void Init(ShiftServices services) {
            shift = services;
            if (!DevBuild.IsEnabled()) {
                return;
            }
            services.Debug.AddCheat(new DebugCheat("Riders", "Rider at the nearest stop", () => SpawnAtNearestStop("")));
            GameRootConfig config = services.Game.Config;
            for (int i = 0; i < config.monsters.Length; i++) {
                MonsterDefinition monster = config.monsters[i];
                if (monster != null) {
                    string id = monster.id;
                    services.Debug.AddCheat(new DebugCheat("Riders", monster.displayName + " at the nearest stop", () => SpawnAtNearestStop(id)));
                }
            }
        }

        // A rider (or the monster of that id; "" for a human) waiting at the stop. Humans ride to
        // the night's end; monsters keep no drop-off.
        internal Passenger SpawnWaiting(BusStop stop, string monsterId) {
            if (stop == null) {
                return null;
            }
            bool monster = !string.IsNullOrEmpty(monsterId);
            RiderSpec spec = new RiderSpec {
                lookId = NextLookId(),
                boardStopId = stop.StopId,
                destinationStopId = monster ? "" : EndStopId(),
                monsterId = monsterId ?? "",
            };
            return SpawnWaiting(stop, spec);
        }

        // A rider waiting in the stop's next free spot, facing the road (spawned as the manifest does)
        internal Passenger SpawnWaiting(BusStop stop, RiderSpec spec) {
            if (stop == null) {
                return null;
            }
            return shift.Manifest.SpawnWaiting(spec, stop, null);
        }

        // A rider already sitting in a free seat (aboard without boarding: no Boarded event)
        internal Passenger SpawnSeated(RiderSpec spec) {
            return SpawnSeated(spec, shift.Cabin.FindFreeSeat());
        }

        // The same, in a given free seat (tests that need a rider where a camera can see them)
        internal Passenger SpawnSeated(RiderSpec spec, BusSeat seat) {
            BusCabin cabin = shift.Cabin;
            if (seat == null || !seat.IsFree) {
                return null;
            }
            Passenger passenger = shift.Manifest.Create(spec, null, seat.transform.position, seat.transform.rotation, cabin.PassengerRoot);
            passenger.PlaceSeated(cabin, seat);
            shift.Riders.MarkAboard(passenger);
            return passenger;
        }

        // Each debug rider in the next look, round the list
        internal string NextLookId() {
            PassengerLookDefinition[] looks = shift.Game.Config.looks;
            string id = looks.Length > 0 && looks[spawned % looks.Length] != null ? looks[spawned % looks.Length].id : "";
            spawned++;
            return id;
        }

        internal string EndStopId() {
            if (shift.Progress != null && shift.Progress.EndStop != null) {
                return shift.Progress.EndStop.StopId;
            }
            return shift.Route.Route.terminusStopId;
        }

        void SpawnAtNearestStop(string monsterId) {
            if (shift == null || shift.Route == null) {
                return;
            }
            Vector3 bus = shift.Bus.transform.position;
            BusStop nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < shift.Route.Stops.Count; i++) {
                BusStop stop = shift.Route.Stops[i];
                float distance = stop != null ? (stop.transform.position - bus).sqrMagnitude : float.MaxValue;
                if (distance < best) {
                    best = distance;
                    nearest = stop;
                }
            }
            SpawnWaiting(nearest, monsterId);
        }
    }
}
