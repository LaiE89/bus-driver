using UnityEngine;

namespace BusDriver.Gameplay.World {
    // The volumes beyond the cliff's outer edge (§2.14 Fall, §3.3, D14). T-M2-07 builds it as a
    // stub; from T-M4-09 the hull entering it calls DeathDirector.Die(Fall). The KillPlane leaves a
    // bus that went over here alone.
    [RequireComponent(typeof(ZoneVolume))]
    public sealed class FallZone : MonoBehaviour {
        ZoneVolume volume;

        public ZoneVolume Volume { get { return volume; } }
        public bool IsBusInside { get { return volume != null && volume.IsBusInside; } }

        void Awake() {
            volume = GetComponent<ZoneVolume>();
        }
    }
}
