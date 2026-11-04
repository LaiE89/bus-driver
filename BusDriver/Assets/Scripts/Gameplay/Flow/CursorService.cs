using BusDriver.Core.Data;
using UnityEngine;

namespace BusDriver.Gameplay.Flow {
    // Cursor lock and visibility follow the effective input context (§4.6, §4.11): locked and
    // hidden while the player controls the bus or their body, free everywhere else. While a pad
    // is in use the free cursor is hidden too (T-M4b-02).
    public sealed class CursorService {
        bool padActive;
        InputContext lastContext;

        public static bool IsLocked(InputContext context) {
            return context == InputContext.Driving || context == InputContext.OnFoot || context == InputContext.Cinematic;
        }

        public void Apply(InputContext context) {
            lastContext = context;
            bool locked = IsLocked(context);
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked && !padActive;
        }

        public void SetPadActive(bool active) {
            padActive = active;
            Apply(lastContext);
        }
    }
}
