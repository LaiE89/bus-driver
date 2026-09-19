using UnityEngine;

public class BusInput : MonoBehaviour {
    [SerializeField] BusController bus;
    [SerializeField] KeyCode handbrakeKey = KeyCode.LeftShift;
    [SerializeField] KeyCode resetKey = KeyCode.R;

    // Something else (the smoke test) calls bus.SetInput. Parking on disable still applies.
    public bool ExternalControl { get; set; }

    void OnEnable() {
        bus.Park(false);
    }

    void Update() {
        if (ingameMenus.pausedGame || ExternalControl) {
            return;
        }
        // Raw axes, BusController does its own steering smoothing
        float steer = Input.GetAxisRaw("Horizontal");
        float accel = Input.GetAxisRaw("Vertical");
        bus.SetInput(steer, accel, Input.GetKey(handbrakeKey));

        if (Input.GetKeyDown(resetKey)) {
            bus.ResetUpright();
        }
    }

    // Nobody in the seat, so the bus parks itself
    void OnDisable() {
        if (bus != null) {
            bus.SetInput(0f, 0f, true);
            bus.Park(true);
        }
    }
}
