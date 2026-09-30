using UnityEngine;

namespace BusDriver.Gameplay.World {
    // The tunnel (§3.3). T-M2-07 builds it as a stub. Its effects (GPS NO SIGNAL, CCTV grain, the
    // unstable lights, the Tunnel snapshot and amb.tunnel) arrive in T-M2-14, the sanity drain in M5.
    [RequireComponent(typeof(ZoneVolume))]
    public sealed class TunnelZone : MonoBehaviour {
        ZoneVolume volume;

        public bool IsInside { get { return volume != null && volume.IsBusInside; } }

        void Awake() {
            volume = GetComponent<ZoneVolume>();
        }
    }
}
