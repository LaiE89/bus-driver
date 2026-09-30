using BusDriver.Core.Data;
using UnityEngine;
using BusDriver.Gameplay.Passengers;

namespace BusDriver.Gameplay.Monsters {
    // Throwaway test monster (deleted in T-M4-03). Its only tell: the head slowly turns to stare at
    // whatever is watching the cabin, be it the active CCTV camera, the driver, or the player on
    // foot. The head-turn math moved into the view's HeadTrack tell (T-M3-01).
    public class StaringMonster : Monster {
        protected override void Tick(float deltaTime) {
            if (State != PassengerState.Seated || View == null || Shift == null) {
                return;
            }
            Camera watcher = Shift.Cctv.ActiveCamera;
            View.SetLookAt(watcher != null ? watcher.transform : null, 0f);
            View.SetTell(TellId.HeadTrack, watcher != null ? 1f : 0f);
        }

        protected override void OnLeaving(bool kicked) {
            if (View != null) {
                View.SetTell(TellId.HeadTrack, 0f);
            }
        }
    }
}
