using System.Collections.Generic;
using UnityEngine;

namespace BusDriver.Gameplay.Player {
    // Shakes the camera it sits on (the rumble strip now, scares from T-M4-05). Each source sets a
    // continuous amplitude (0..1); the strongest one wins. It offsets the camera's local pose, which
    // nothing else drives (DriverLook turns the pivot above it), and runs on scaled time so it stops
    // with the pause.
    public sealed class CameraShake : MonoBehaviour {
        [Tooltip("Position offset at amplitude 1, in metres [TUNE]")]
        [SerializeField] float maxOffset = 0.08f;
        [Tooltip("Rotation at amplitude 1, in degrees [TUNE]")]
        [SerializeField] float maxAngle = 2f;
        [Tooltip("Noise frequency, Hz [TUNE]")]
        [SerializeField] float frequency = 22f;

        readonly Dictionary<object, float> sources = new Dictionary<object, float>();
        Vector3 basePosition;
        Quaternion baseRotation;
        float time;

        public float Amplitude { get; private set; }

        void Awake() {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
        }

        // 0 removes the source
        public void SetShake(object source, float amplitude01) {
            if (amplitude01 <= 0f) {
                sources.Remove(source);
            }else {
                sources[source] = Mathf.Clamp01(amplitude01);
            }
            Amplitude = 0f;
            foreach (KeyValuePair<object, float> entry in sources) {
                Amplitude = Mathf.Max(Amplitude, entry.Value);
            }
        }

        void LateUpdate() {
            if (Amplitude <= 0f) {
                transform.localPosition = basePosition;
                transform.localRotation = baseRotation;
                return;
            }
            time += Time.deltaTime * frequency;
            Vector3 offset = new Vector3(Noise(0f), Noise(17f), 0f) * (maxOffset * Amplitude);
            Vector3 angles = new Vector3(Noise(31f), Noise(47f), Noise(59f) * 0.5f) * (maxAngle * Amplitude);
            transform.localPosition = basePosition + offset;
            transform.localRotation = baseRotation * Quaternion.Euler(angles);
        }

        float Noise(float lane) {
            return Mathf.PerlinNoise(time, lane) * 2f - 1f;
        }

        void OnDisable() {
            transform.localPosition = basePosition;
            transform.localRotation = baseRotation;
        }
    }
}
