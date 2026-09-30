using UnityEngine;

namespace BusDriver.Gameplay.World {
    // The "ROAD CLOSED" barrier across the road 60 m past a night's end stop, when that stop isn't
    // the route's last (§2.4, D43): concrete barriers and a sign. RouteBuilder bakes one, inactive,
    // for every such end stop a night names; the night switches its own on.
    public sealed class NightEndBarrier : MonoBehaviour {
        public const float DistancePastStop = 60f;

        [SerializeField] string stopId = "";
        [Tooltip("Route distance of the barrier (the stop's distance + 60 m)")]
        [SerializeField] float distance;

        public string StopId { get { return stopId; } }
        public float Distance { get { return distance; } }
    }
}
