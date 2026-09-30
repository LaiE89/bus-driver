using System;
using UnityEngine;

namespace BusDriver.Gameplay.Bus {
    // Only hull hits land here, wheel contacts are raycasts and never raise collision events
    [RequireComponent(typeof(Rigidbody))]
    public class CrashDetector : MonoBehaviour {
        // Change in speed (m/s) caused by the hit
        [SerializeField] float minorDeltaV = 1f;
        [SerializeField] float majorDeltaV = 4f;
        [SerializeField] float cooldown = 0.5f;

        // deltaV, isMajor, collision
        public event Action<float, bool, Collision> OnCrash;

        // Speed before the physics response for this frame's collisions (km/h)
        public float PreCollisionSpeedKmh { get; private set; }

        Rigidbody rb;
        float lastCrashTime = -999f;

        void Awake() {
            rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate() {
            // FixedUpdate runs before the physics step, so this is pre-impact speed
            PreCollisionSpeedKmh = Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.forward)) * 3.6f;
        }

        void OnCollisionEnter(Collision collision) {
            if (Time.time - lastCrashTime < cooldown) {
                return;
            }
            float deltaV = collision.impulse.magnitude / rb.mass;
            if (deltaV < minorDeltaV) {
                return;
            }
            lastCrashTime = Time.time;
            bool isMajor = deltaV >= majorDeltaV;
            Debug.Log($"{(isMajor ? "Major" : "Minor")} crash into {collision.collider.name}, deltaV {deltaV:F1} m/s");
            OnCrash?.Invoke(deltaV, isMajor, collision);
        }
    }
}
