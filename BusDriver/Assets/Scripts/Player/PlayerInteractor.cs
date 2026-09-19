using UnityEngine;

// Looks along the on-foot camera for something to interact with. Lives on the on-foot
// rig, so it only runs while walking.
public class PlayerInteractor : MonoBehaviour {
    [SerializeField] Camera viewCamera;
    [SerializeField] float reach = 2.2f;

    public IInteractable Current { get; private set; }
    public string CurrentPrompt { get { return Current != null ? Current.Prompt : ""; } }

    readonly RaycastHit[] hits = new RaycastHit[16];

    void Update() {
        if (ingameMenus.pausedGame) {
            return;
        }
        SetCurrent(FindTarget());
        if (Current != null && Input.GetKeyDown(GameKeys.interact)) {
            Current.Interact();
        }
    }

    void OnDisable() {
        SetCurrent(null);
    }

    // Nearest interactable along the ray. Occluders are ignored on purpose: a seat
    // back should not stop the driver pointing at the passenger behind it.
    IInteractable FindTarget() {
        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        int count = Physics.RaycastNonAlloc(ray, hits, reach, ~0, QueryTriggerInteraction.Collide);
        IInteractable best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++) {
            IInteractable candidate = hits[i].collider.GetComponentInParent<IInteractable>();
            if (candidate != null && candidate.CanInteract && hits[i].distance < bestDistance) {
                best = candidate;
                bestDistance = hits[i].distance;
            }
        }
        return best;
    }

    void SetCurrent(IInteractable target) {
        if (target == Current) {
            return;
        }
        // A destroyed passenger is still a non-null interface reference
        if (Current != null && !(Current is Object old && old == null)) {
            Current.SetFocused(false);
        }
        Current = target;
        if (Current != null) {
            Current.SetFocused(true);
        }
    }
}
