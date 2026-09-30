using System;
using BusDriver.Gameplay.Bus;
using UnityEngine;

namespace BusDriver.Gameplay.World {
    // A trigger under the whole map (§3.4). A bus that falls through the world anywhere but the
    // cliff is put back on the road (§2.3): RouteTracker listens and does the respawn.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class KillPlane : MonoBehaviour {
        public event Action OnBusEntered;

        void OnTriggerEnter(Collider other) {
            if (other is WheelCollider || other.attachedRigidbody == null
                || other.attachedRigidbody.GetComponent<BusController>() == null) {
                return;
            }
            if (OnBusEntered != null) {
                OnBusEntered();
            }
        }
    }
}
