using UnityEngine;

// Looks along the active camera for something to interact with. Works seated (driver
// camera) and on foot. Lives on an always-active object so leaving the seat does not
// destroy it.
public class PlayerInteractor : MonoBehaviour {
    [SerializeField] Camera onFootCamera;
    [SerializeField] Camera driverCamera;
    [SerializeField] float onFootReach = 2.2f;
    [SerializeField] float seatedReach = 5f;

    public IInteractable Current { get; private set; }
    public string CurrentPrompt { get { return Current != null ? Current.Prompt : ""; } }

    readonly RaycastHit[] hits = new RaycastHit[16];

    void Awake() {
        // Older scenes put this on OnFootRig; keep it alive while the player is seated
        SceneController mode = FindAnyObjectByType<SceneController>();
        if (mode != null && transform.parent != mode.transform) {
            OnFootController onFootParent = GetComponentInParent<OnFootController>();
            if (onFootParent != null) {
                transform.SetParent(mode.transform, true);
            }
        }
        WireCamerasFromScene();
    }

    public void WireCameras(Camera onFoot, Camera driver) {
        if (onFoot != null) {
            onFootCamera = onFoot;
        }
        if (driver != null) {
            driverCamera = driver;
        }
    }

    void WireCamerasFromScene() {
        SceneController mode = SceneController.Instance != null
            ? SceneController.Instance
            : FindAnyObjectByType<SceneController>();
        if (mode == null) {
            return;
        }
        if (onFootCamera == null) {
            onFootCamera = mode.OnFootCamera;
        }
        if (driverCamera == null) {
            driverCamera = mode.DriverCamera;
        }
    }

    void Update() {
        if (ingameMenus.pausedGame) {
            SetCurrent(null);
            return;
        }
        SceneController mode = SceneController.Instance;
        if (mode == null) {
            SetCurrent(null);
            return;
        }

        if (onFootCamera == null || driverCamera == null) {
            WireCamerasFromScene();
        }

        Camera cam;
        float reach;
        if (mode.Mode == PlayerMode.OnFoot) {
            cam = onFootCamera;
            reach = onFootReach;
        }else if (mode.Mode == PlayerMode.Driving) {
            if (mode.IsViewingCCTV) {
                SetCurrent(null);
                return;
            }
            cam = driverCamera;
            reach = seatedReach;
        }else {
            SetCurrent(null);
            return;
        }

        if (cam == null) {
            SetCurrent(null);
            return;
        }

        SetCurrent(FindTarget(cam, reach));
        if (Current != null && Input.GetKeyDown(GameKeys.interact)) {
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
