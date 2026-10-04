using UnityEngine;

namespace BusDriver.Gameplay.Player {
    // Sits on the driver seat's interior collider, so it is only reachable on foot. BusCabin binds
    // it to the mode switch in its Init (T-M1-15).
    public class DriverSeat : MonoBehaviour, IInteractable {
        PlayerModeController mode;

        public void Bind(PlayerModeController controller) {
            mode = controller;
        }

        public string Prompt { get { return "Sit down"; } }
        public bool CanInteract {
            get { return mode != null && mode.Mode == PlayerMode.OnFoot; }
        }
        public string AltPrompt { get { return ""; } }

        public void Interact() {
            if (mode != null) {
                mode.TrySitDown();
            }
        }

        public void AltInteract() { }

        public void SetFocused(bool focused) { }
    }
}
