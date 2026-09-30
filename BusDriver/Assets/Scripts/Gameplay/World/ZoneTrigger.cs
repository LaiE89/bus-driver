using UnityEngine;

namespace BusDriver.Gameplay.World {
    // One box of a ZoneVolume. Trigger messages only reach the collider's own GameObject, so each
    // box passes them up to its volume.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ZoneTrigger : MonoBehaviour {
        [SerializeField] ZoneVolume volume;

        void OnTriggerEnter(Collider other) {
            if (volume != null) {
                volume.Enter(other);
            }
        }

        void OnTriggerExit(Collider other) {
            if (volume != null) {
                volume.Exit(other);
            }
        }
    }
}
