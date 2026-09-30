using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.World;
using UnityEngine;

namespace BusDriver.Gameplay.Debug {
    // A development and test hook (T-M2-07): puts a rider waiting at a stop. The legacy loop's
    // pooled riders are gone, and the night manifest (ManifestSpawner, T-M3-03) doesn't exist yet,
    // so the smoke test boards and kicks through this, and the F1 overlay offers it as a cheat.
    public sealed class DebugRiders : MonoBehaviour {
        [SerializeField] Passenger riderPrefab;
        [Tooltip("The legacy test monster, until the monster prefabs of T-M4-03")]
        [SerializeField] Passenger angelPrefab;

        ShiftServices shift;
        int spawned;

        // ShiftContext, where ManifestSpawner will be in the Init order (§4.5 step 14)
        public void Init(ShiftServices services) {
            shift = services;
            if (!DevBuild.IsEnabled()) {
                return;
            }
            services.Debug.AddCheat(new DebugCheat("Riders", "Rider at the nearest stop", () => SpawnAtNearestStop(false)));
            services.Debug.AddCheat(new DebugCheat("Riders", "Weeping Angel at the nearest stop", () => SpawnAtNearestStop(true)));
        }

        // A rider waiting in the stop's next free spot, bound to the night
        internal Passenger SpawnWaiting(BusStop stop, bool angel) {
            Passenger prefab = angel ? angelPrefab : riderPrefab;
            if (stop == null || prefab == null) {
                return null;
            }
            Transform at = stop.transform;
            Vector3 position = at.TransformPoint(stop.WaitLocalPosition(stop.WaitingCount));
            // Facing the road
            Quaternion rotation = at.rotation * Quaternion.Euler(0f, -90f, 0f);
            Passenger passenger = Instantiate(prefab, position, rotation, at);
            passenger.name = prefab.name;
            passenger.Bind(shift);
            // Each debug rider in the next look, round the list
            PassengerLookDefinition[] looks = shift.Game.Config.looks;
            PassengerLookDefinition look = looks.Length > 0 ? looks[spawned % looks.Length] : null;
            spawned++;
            shift.Views.Recreate(passenger, look);
            stop.AddWaiting(passenger);
            return passenger;
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
