using UnityEngine;

namespace BusDriver.Gameplay.Player {
    // Sits on the driver seat's interior collider, so it is only reachable on foot
    public class DriverSeat : MonoBehaviour, IInteractable {
        public string Prompt { get { return "Sit down"; } }
        public bool CanInteract {
            get {
                return SceneController.Instance != null
                    && SceneController.Instance.Mode == PlayerMode.OnFoot;
            }
        }

        public void Interact() {
            SceneController.Instance.TrySitDown();
        }

        public void SetFocused(bool focused) { }
    }
}
