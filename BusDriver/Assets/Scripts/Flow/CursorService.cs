using BusDriver.Core.Data;
using UnityEngine;

namespace BusDriver.Gameplay.Flow {
    // Cursor lock and visibility follow the effective input context (§4.6, §4.11): locked and
    // hidden while the player controls the bus or their body, free everywhere else
    public sealed class CursorService {
        public static bool IsLocked(InputContext context) {
            return context == InputContext.Driving || context == InputContext.OnFoot || context == InputContext.Cinematic;
        }

        public void Apply(InputContext context) {
            bool locked = IsLocked(context);
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
