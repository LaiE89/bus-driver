using UnityEngine;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Input;

namespace BusDriver.Gameplay.Player {
    // Seated head look, sits on the pivot that parents the driver camera
    public class DriverLook : MonoBehaviour {
        [SerializeField] CCTVSystem cctv;
        [SerializeField] float yawLimit = 100f;
        [SerializeField] float pitchMin = -50f;
        [SerializeField] float pitchMax = 35f;
        // Degrees per legacy mouse-axis unit at sensitivity 1; the Look action gives pixels, which
        // InputService.MouseAxisScale converts (§4.10)
        [SerializeField] float sensScale = 0.02f;
        // Used until a scene root binds the settings
        [SerializeField] float fallbackSens = 60f;

        float yaw;
        float pitch;
        SettingsService settings;
        PauseService pause;
        InputService input;

        // ShiftContext, with the input adapters (§4.5 step 3)
        public void Init(ShiftServices shift) {
            settings = shift.Game.Settings;
            pause = shift.Game.Pause;
            input = shift.Game.Input;
        }

        internal float Sensitivity {
            get { return settings != null ? settings.Current.mouseSensitivity : fallbackSens; }
        }

        // Invert Y flips the vertical axis (§4.10)
        internal float PitchSign {
            get { return settings != null && settings.Current.invertY ? -1f : 1f; }
        }

        // Points the head straight at a yaw and pitch, degrees (tests: the road-yaw check, §2.8)
        internal void SetLook(float yawDegrees, float pitchDegrees) {
            yaw = Mathf.Clamp(yawDegrees, -yawLimit, yawLimit);
            pitch = Mathf.Clamp(pitchDegrees, pitchMin, pitchMax);
            transform.localRotation = Quaternion.Euler(-pitch, yaw, 0f);
        }

        void Update() {
            if (input == null || (pause != null && pause.IsPaused) || (cctv != null && cctv.IsViewingCCTV)) {
                return;
            }
            Vector2 look = input.Actions.DrivingLook.ReadValue<Vector2>() * InputService.MouseAxisScale;
            float sens = Sensitivity;
            yaw = Mathf.Clamp(yaw + look.x * sens * sensScale, -yawLimit, yawLimit);
            // Positive pitch looks up
            pitch = Mathf.Clamp(pitch + look.y * PitchSign * sens * sensScale, pitchMin, pitchMax);
            transform.localRotation = Quaternion.Euler(-pitch, yaw, 0f);
        }
    }
}
