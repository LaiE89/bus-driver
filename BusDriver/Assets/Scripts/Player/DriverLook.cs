using UnityEngine;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;

namespace BusDriver.Gameplay.Player {
    // Seated head look, sits on the pivot that parents the driver camera
    public class DriverLook : MonoBehaviour, IGameBindable {
        [SerializeField] CCTVSystem cctv;
        [SerializeField] float yawLimit = 100f;
        [SerializeField] float pitchMin = -50f;
        [SerializeField] float pitchMax = 35f;
        // Degrees per mouse unit at sensitivity 1
        [SerializeField] float sensScale = 0.02f;
        // Used until a scene root binds the settings
        [SerializeField] float fallbackSens = 60f;

        float yaw;
        float pitch;
        SettingsService settings;
        PauseService pause;

        // From the scene root (LegacyNightRoot, later ShiftContext)
        public void Bind(GameServices game) {
            settings = game.Settings;
            pause = game.Pause;
        }

        internal float Sensitivity {
            get { return settings != null ? settings.Current.mouseSensitivity : fallbackSens; }
        }

        // Invert Y flips the vertical axis (§4.10)
        internal float PitchSign {
            get { return settings != null && settings.Current.invertY ? -1f : 1f; }
        }

        void Update() {
            if ((pause != null && pause.IsPaused) || (cctv != null && cctv.IsViewingCCTV)) {
                return;
            }
            float sens = Sensitivity;
            yaw = Mathf.Clamp(yaw + UnityEngine.Input.GetAxis("Mouse X") * sens * sensScale, -yawLimit, yawLimit);
            // Positive pitch looks up
            pitch = Mathf.Clamp(pitch + UnityEngine.Input.GetAxis("Mouse Y") * PitchSign * sens * sensScale, pitchMin, pitchMax);
            transform.localRotation = Quaternion.Euler(-pitch, yaw, 0f);
        }
    }
}
