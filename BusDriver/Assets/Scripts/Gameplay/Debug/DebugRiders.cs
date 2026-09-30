using BusDriver.Core.Data;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.World;
using UnityEngine;

namespace BusDriver.Gameplay.Debug {
    // A development and test hook (T-M2-07): puts extra riders at a stop, or straight into a seat,
    // registered with the PassengerRegistry like any manifest rider. The smoke test boards and
    // kicks through it (the legacy Weeping Angel, until the monster prefabs of T-M4-03), tests
    // stage stops with it, and the F1 overlay offers it as a cheat.
    public sealed class DebugRiders : MonoBehaviour {
        [SerializeField] Passenger riderPrefab;
        [Tooltip("The legacy test monster, until the monster prefabs of T-M4-03")]
        [SerializeField] Passenger angelPrefab;

        ShiftServices shift;
        int spawned;

        // ShiftContext, beside ManifestSpawner in the Init order (§4.5 step 14)
        public void Init(ShiftServices services) {
            shift = services;
            if (!DevBuild.IsEnabled()) {
                return;
            }
            services.Debug.AddCheat(new DebugCheat("Riders", "Rider at the nearest stop", () => SpawnAtNearestStop(false)));
            services.Debug.AddCheat(new DebugCheat("Riders", "Weeping Angel at the nearest stop", () => SpawnAtNearestStop(true)));
        }

        // A rider (or the legacy angel) waiting at the stop, riding to the night's end stop
        internal Passenger SpawnWaiting(BusStop stop, bool angel) {
            if (stop == null) {
                return null;
            }
            RiderSpec spec = new RiderSpec {
                lookId = NextLookId(),
                boardStopId = stop.StopId,
                destinationStopId = EndStopId(),
                monsterId = angel ? "weeping_angel" : "",
            };
            return SpawnWaiting(stop, spec, angel);
        }

        // A rider waiting in the stop's next free spot, facing the road (spawned as the manifest does)
        internal Passenger SpawnWaiting(BusStop stop, RiderSpec spec, bool angel = false) {
            Passenger prefab = angel ? angelPrefab : riderPrefab;
            if (stop == null || prefab == null) {
                return null;
            }
            return shift.Manifest.SpawnWaiting(spec, stop, prefab);
        }

        // A rider already sitting in a free seat (aboard without boarding: no Boarded event)
        internal Passenger SpawnSeated(RiderSpec spec) {
            BusCabin cabin = shift.Cabin;
            BusSeat seat = cabin.FindFreeSeat();
            if (seat == null || riderPrefab == null) {
                return null;
            }
            Passenger passenger = shift.Manifest.Create(spec, riderPrefab, seat.transform.position, seat.transform.rotation, cabin.PassengerRoot);
            passenger.PlaceSeated(cabin, seat);
            shift.Riders.MarkAboard(passenger);
            return passenger;
        }

        // Each debug rider in the next look, round the list
        string NextLookId() {
            PassengerLookDefinition[] looks = shift.Game.Config.looks;
            string id = looks.Length > 0 && looks[spawned % looks.Length] != null ? looks[spawned % looks.Length].id : "";
            spawned++;
            return id;
        }

        string EndStopId() {
            if (shift.Progress != null && shift.Progress.EndStop != null) {
                return shift.Progress.EndStop.StopId;
            }
            return shift.Route.Route.terminusStopId;
        }

        void SpawnAtNearestStop(bool angel) {
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
            SpawnWaiting(nearest, angel);
        }
    }
}
