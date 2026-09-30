using UnityEngine;

namespace BusDriver.Gameplay.World {
    // A rumble strip (§3.3): across the road before Dead Man's Bend, then along its left edge.
    // T-M2-07 builds it as a stub; the sound and the camera shake arrive in T-M2-14.
    [RequireComponent(typeof(ZoneVolume))]
    public sealed class RumbleZone : MonoBehaviour {
        ZoneVolume volume;

        public bool IsBusInside { get { return volume != null && volume.IsBusInside; } }

        void Awake() {
            volume = GetComponent<ZoneVolume>();
        }
    }
}
