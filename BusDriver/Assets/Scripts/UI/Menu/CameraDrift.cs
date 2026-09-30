using UnityEngine;

namespace BusDriver.UI.Menu {
    // The menu diorama's handheld feel (§2.22): slow Perlin drift of ±0.05 m and ±0.6° at 0.07 Hz
    // around the camera's built pose. Unscaled time, since it's presentation (§4.1.9).
    public sealed class CameraDrift : MonoBehaviour {
        [SerializeField] float positionAmplitude = 0.05f;
        [SerializeField] float rotationAmplitude = 0.6f;
        [SerializeField] float frequency = 0.07f;

        Vector3 basePosition;
        Quaternion baseRotation;

        public Vector3 BasePosition { get { return basePosition; } }

        void Awake() {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
        }

        void Update() {
            float t = Time.unscaledTime * frequency;
            Vector3 offset = new Vector3(Noise(t, 0f), Noise(t, 11f), Noise(t, 23f)) * positionAmplitude;
            Vector3 angles = new Vector3(Noise(t, 37f), Noise(t, 53f), Noise(t, 71f) * 0.5f) * rotationAmplitude;
            transform.localPosition = basePosition + offset;
            transform.localRotation = baseRotation * Quaternion.Euler(angles);
        }

        static float Noise(float t, float lane) {
            return Mathf.PerlinNoise(t, lane) * 2f - 1f;
        }
    }
}
