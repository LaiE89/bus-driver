using UnityEngine;

// Seated head look, sits on the pivot that parents the driver camera
public class DriverLook : MonoBehaviour {
    [SerializeField] CCTVSystem cctv;
    [SerializeField] float yawLimit = 100f;
    [SerializeField] float pitchMin = -50f;
    [SerializeField] float pitchMax = 35f;
    // Degrees per mouse unit at sensitivity 1
    [SerializeField] float sensScale = 0.02f;
    // Used when the scene is played directly and the options menu never initialised
    [SerializeField] float fallbackSens = 60f;

    float yaw;
    float pitch;

    void Update() {
        if (ingameMenus.pausedGame || (cctv != null && cctv.IsViewingCCTV)) {
            return;
        }
        float sens = OptionsMenu.sens > 0f ? OptionsMenu.sens : fallbackSens;
        yaw = Mathf.Clamp(yaw + Input.GetAxis("Mouse X") * sens * sensScale, -yawLimit, yawLimit);
        // Positive pitch looks up
        pitch = Mathf.Clamp(pitch + Input.GetAxis("Mouse Y") * sens * sensScale, pitchMin, pitchMax);
        transform.localRotation = Quaternion.Euler(-pitch, yaw, 0f);
    }
}
