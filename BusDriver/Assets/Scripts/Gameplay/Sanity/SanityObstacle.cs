using System;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using UnityEngine;

namespace BusDriver.Gameplay.Sanity {
    // A phantom figure on the road at low sanity. Hitting it only jump-scares; it is a trigger so
    // the bus does not take a real crash.
    public sealed class SanityObstacle : MonoBehaviour {
        ShiftServices shift;
        Action<SanityObstacle> onHit;
        bool hit;

        public void Init(ShiftServices services, Action<SanityObstacle> hitCallback) {
            shift = services;
            onHit = hitCallback;
        }

        void OnTriggerEnter(Collider other) {
            if (hit || shift == null) {
                return;
            }
            BusController bus = other.GetComponentInParent<BusController>();
            if (bus == null || bus != shift.Bus) {
                return;
            }
            hit = true;
            if (onHit != null) {
                onHit(this);
            }
        }
    }
}
