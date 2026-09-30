using UnityEngine;
using BusDriver.Gameplay.Flow;

namespace BusDriver.Gameplay.Player {
    // Looks along the on-foot camera for something to interact with. Kicking is on foot only
    // (D46), so nothing is offered while seated. Lives on an always-active object so leaving
    // the seat does not destroy it.
    public class PlayerInteractor : MonoBehaviour {
        [SerializeField] float onFootReach = 2.2f;

        public IInteractable Current { get; private set; }
        public string CurrentPrompt { get { return Current != null ? Current.Prompt : ""; } }

        readonly RaycastHit[] hits = new RaycastHit[16];
        GameServices game;
        PlayerModeController mode;
        Camera onFootCamera;

        // ShiftContext, with the input adapters (§4.5 step 3)
        public void Init(ShiftServices shift) {
            game = shift.Game;
            mode = shift.Mode;
            onFootCamera = shift.OnFootCamera;
        }

        void Update() {
            if (game == null || game.Pause.IsPaused || mode == null || mode.Mode != PlayerMode.OnFoot || onFootCamera == null) {
                SetCurrent(null);
                return;
            }
            SetCurrent(FindTarget(onFootCamera, onFootReach));
            if (Current != null && game.Input.Actions.Interact.WasPressedThisFrame()) {
                Current.Interact();
            }
        }

        void OnDisable() {
            SetCurrent(null);
        }

        // Nearest interactable along the ray. Occluders are ignored on purpose: a seat
        // back should not stop the driver pointing at the passenger behind it.
        IInteractable FindTarget(Camera cam, float reach) {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            int count = Physics.RaycastNonAlloc(ray, hits, reach, ~0, QueryTriggerInteraction.Collide);
            IInteractable best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++) {
                // Prefer the collider itself so a bus-root interactable can't steal hull hits
                IInteractable candidate = hits[i].collider.GetComponent<IInteractable>();
                if (candidate == null) {
                    candidate = hits[i].collider.GetComponentInParent<IInteractable>();
                }
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
}
