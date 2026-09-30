using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.World {
    // The volumes beyond the cliff's outer edge (§2.14 Fall, §3.3, D14). The bus hull entering it
    // while the night drives is a death: DeathDirector.Die(Fall), whose presenter is the fall cam.
    // The KillPlane leaves a bus that went over here alone (RouteTracker).
    [RequireComponent(typeof(ZoneVolume))]
    public sealed class FallZone : MonoBehaviour {
        ZoneVolume volume;
        ShiftServices shift;

        public ZoneVolume Volume { get { return volume; } }
        public bool IsBusInside { get { return volume != null && volume.IsBusInside; } }

        void Awake() {
            volume = GetComponent<ZoneVolume>();
        }

        // ShiftContext, after DeathDirector (§4.5 step 9)
        public void Init(ShiftServices services) {
            shift = services;
            volume.OnBusInsideChanged += HandleBusInside;
        }

        void OnDestroy() {
            if (volume != null) {
                volume.OnBusInsideChanged -= HandleBusInside;
            }
        }

        void HandleBusInside(bool inside) {
            if (!inside || shift == null || shift.Death == null || shift.Director.State != ShiftState.Driving) {
                return;
            }
            shift.Death.Die(DeathCause.Fall);
        }
    }
}
