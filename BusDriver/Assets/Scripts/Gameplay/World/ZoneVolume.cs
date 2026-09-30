using System;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Bus;
using UnityEngine;

namespace BusDriver.Gameplay.World {
    // One route zone's trigger volumes (§3.3; layer Zone, §4.16). RouteBuilder puts a box per stretch
    // of road under it, each with a ZoneTrigger reporting here. It only tracks whether the bus hull
    // is inside; what that means (NO SIGNAL, the rumble, the fall) belongs to the behaviour component
    // beside it: TunnelZone, RumbleZone or FallZone.
    public sealed class ZoneVolume : MonoBehaviour {
        [SerializeField] RouteZoneKind kind;
        [Tooltip("Metres along the route")]
        [SerializeField] float start;
        [SerializeField] float end;

        public RouteZoneKind Kind { get { return kind; } }
        public float Start { get { return start; } }
        public float End { get { return end; } }
        public bool IsBusInside { get { return inside > 0; } }
        public event Action<bool> OnBusInsideChanged;

        // Hull colliders currently overlapping any of the boxes, so one box handing over to the
        // next never reads as leaving
        int inside;

        internal void Enter(Collider other) {
            if (!IsBusHull(other)) {
                return;
            }
            inside++;
            if (inside == 1 && OnBusInsideChanged != null) {
                OnBusInsideChanged(true);
            }
        }

        internal void Exit(Collider other) {
            if (!IsBusHull(other) || inside == 0) {
                return;
            }
            inside--;
            if (inside == 0 && OnBusInsideChanged != null) {
                OnBusInsideChanged(false);
            }
        }

        // The hull, not the wheels: WheelColliders are colliders too
        static bool IsBusHull(Collider other) {
            return !(other is WheelCollider) && other.attachedRigidbody != null
                && other.attachedRigidbody.GetComponent<BusController>() != null;
        }
    }
}
