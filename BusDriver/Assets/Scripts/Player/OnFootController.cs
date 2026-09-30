using UnityEngine;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Input;

namespace BusDriver.Gameplay.Player {
    // Walking around inside the parked bus. The rig lives at the scene root, not under the
    // bus: the bus is frozen while anyone is on foot, and this way the view stays level
    // even when the bus is parked with two wheels up a kerb.
    [RequireComponent(typeof(CharacterController))]
    public class OnFootController : MonoBehaviour, IGameBindable {
        [SerializeField] Transform head;
        [SerializeField] Transform avatarRoot;
        // The bus hull is one solid box around the whole interior
        [SerializeField] Collider[] ignoredColliders;
        [SerializeField] float walkSpeed = 2.2f;
        [SerializeField] float gravity = -15f;
        [SerializeField] float pitchLimit = 70f;
        // Same convention as DriverLook
        [SerializeField] float sensScale = 0.02f;
        [SerializeField] float fallbackSens = 60f;

        public Transform Head { get { return head; } }
        public Transform AvatarRoot { get { return avatarRoot; } }
        // The smoke test walks the player with these instead of the keyboard
        public bool ExternalControl { get; set; }
        public Vector2 ExternalMove { get; set; }

        CharacterController controller;
        SettingsService settings;
        PauseService pause;
        InputService input;
        float yaw;
        float pitch;
        float fallSpeed;

        // From the scene root (LegacyNightRoot, later ShiftContext)
        public void Bind(GameServices game) {
            settings = game.Settings;
            pause = game.Pause;
            input = game.Input;
        }

        internal float Sensitivity {
            get { return settings != null ? settings.Current.mouseSensitivity : fallbackSens; }
        }

        internal float PitchSign {
            get { return settings != null && settings.Current.invertY ? -1f : 1f; }
        }

        void Awake() {
            controller = GetComponent<CharacterController>();
            if (head == null) {
                Transform found = transform.Find("Head");
                if (found != null) {
                    head = found;
                }
            }
            if (avatarRoot == null) {
                Transform found = transform.Find("Avatar");
                if (found != null) {
                    avatarRoot = found;
                }
            }
            if (avatarRoot != null) {
                PlayerAvatarVisuals.ApplyCullLayer(avatarRoot, head);
            }else {
                Log.Warn(LogCat.Flow, "OnFootController: no Avatar in the scene. Run Tools/Bus Driver/Build MVP Scene.");
            }
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
            if (pause != null && pause.IsPaused) {
                return;
            }
            Vector2 move = ExternalMove;
            if (!ExternalControl && input != null) {
                Vector2 look = input.Actions.OnFootLook.ReadValue<Vector2>() * InputService.MouseAxisScale;
                float sens = Sensitivity;
                yaw += look.x * sens * sensScale;
                pitch = Mathf.Clamp(pitch + look.y * PitchSign * sens * sensScale, -pitchLimit, pitchLimit);
                move = input.Actions.Move.ReadValue<Vector2>();
            }else if (!ExternalControl) {
                move = Vector2.zero;
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (head != null) {
                head.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
            }

            Vector3 velocity = (transform.right * move.x + transform.forward * move.y);
            velocity = Vector3.ClampMagnitude(velocity, 1f) * walkSpeed;
            fallSpeed = controller.isGrounded ? -1f : fallSpeed + gravity * Time.deltaTime;
            velocity.y = fallSpeed;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
