using UnityEngine;

// Sits on the driver seat's interior collider, so it is only reachable on foot
public class DriverSeat : MonoBehaviour, IInteractable {
    public string Prompt { get { return "Sit down"; } }
    public bool CanInteract { get { return PlayerModeController.Instance != null; } }

    public void Interact() {
        PlayerModeController.Instance.TrySitDown();
    }

    public void SetFocused(bool focused) { }
}
