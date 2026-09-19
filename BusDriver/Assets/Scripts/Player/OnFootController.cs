using UnityEngine;

// Walking around inside the parked bus. The rig lives at the scene root, not under the
// bus: the bus is frozen while anyone is on foot, and this way the view stays level
// even when the bus is parked with two wheels up a kerb.
[RequireComponent(typeof(CharacterController))]
public class OnFootController : MonoBehaviour {
    [SerializeField] Transform head;
    // The bus hull is one solid box around the whole interior
    [SerializeField] Collider[] ignoredColliders;
    [SerializeField] float walkSpeed = 2.2f;
    [SerializeField] float gravity = -15f;
    [SerializeField] float pitchLimit = 70f;
    // Same convention as DriverLook
    [SerializeField] float sensScale = 0.02f;
    [SerializeField] float fallbackSens = 60f;

    public Transform Head { get { return head; } }
    // The smoke test walks the player with these instead of the keyboard
    public bool ExternalControl { get; set; }
    public Vector2 ExternalMove { get; set; }

    CharacterController controller;
    float yaw;
    float pitch;
    float fallSpeed;

    void Awake() {
        controller = GetComponent<CharacterController>();
    }

    void OnEnable() {
        // Re-applied every time, the ignore can be lost when a collider is deactivated
        foreach (Collider ignored in ignoredColliders) {
            if (ignored != null) {
                Physics.IgnoreCollision(controller, ignored, true);
            }
        }
    }

    // Call while the rig is inactive so the CharacterController picks the pose up cleanly
    public void Place(Vector3 worldPosition, float worldYaw) {
        transform.SetPositionAndRotation(worldPosition, Quaternion.Euler(0f, worldYaw, 0f));
        yaw = worldYaw;
        pitch = 0f;
        fallSpeed = 0f;
        if (head != null) {
            head.localRotation = Quaternion.identity;
        }
    }

    void Update() {
        if (ingameMenus.pausedGame) {
            return;
        }
        Vector2 move = ExternalMove;
        if (!ExternalControl) {
            float sens = OptionsMenu.sens > 0f ? OptionsMenu.sens : fallbackSens;
            yaw += Input.GetAxis("Mouse X") * sens * sensScale;
            pitch = Mathf.Clamp(pitch + Input.GetAxis("Mouse Y") * sens * sensScale, -pitchLimit, pitchLimit);
            move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        head.localRotation = Quaternion.Euler(-pitch, 0f, 0f);

        Vector3 velocity = (transform.right * move.x + transform.forward * move.y);
        velocity = Vector3.ClampMagnitude(velocity, 1f) * walkSpeed;
        fallSpeed = controller.isGrounded ? -1f : fallSpeed + gravity * Time.deltaTime;
        velocity.y = fallSpeed;
        controller.Move(velocity * Time.deltaTime);
    }
}
