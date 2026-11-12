using System;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using UnityEngine;

namespace BusDriver.Gameplay.Sanity {
    // A flat sprite standing on the road at low sanity. Hitting it only jump-scares; it is a trigger
    // so the bus does not take a real crash. The picture keeps its face toward the bus.
    public sealed class SanityObstacle : MonoBehaviour {
        ShiftServices shift;
        Action<SanityObstacle> onHit;
        bool hit;

        public Sprite Picture { get; set; }
        // Set when this one walks the centre of the lane toward the bus.
        public bool Approaches { get; set; }
        public float RouteDistance { get; set; }
        public float Lateral { get; set; }

        public void Init(ShiftServices services, Action<SanityObstacle> hitCallback) {
            shift = services;
            onHit = hitCallback;
            FaceBus();
        }

        void LateUpdate() {
            FaceBus();
        }

        // A SpriteRenderer's visible side is local -Z, so +Z points down the road and the picture
        // faces the driver.
        void FaceBus() {
            if (shift == null || shift.Bus == null) {
                return;
            }
            Vector3 forward = shift.Bus.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) {
                return;
            }
            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        void OnTriggerEnter(Collider other) {
            if (hit || shift == null) {
                return;
            }
            BusController bus = other.GetComponentInParent<BusController>();
            if (bus == null || bus != shift.Bus) {
                return;
            }
            hit = true;
            Hide();
            if (onHit != null) {
                onHit(this);
            }
        }

        // Gone the same frame the bus touches it, so the quad never fills the windshield.
        // The PNG is shown by the full-screen flash instead.
        void Hide() {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++) {
                renderers[i].enabled = false;
            }
        }
    }
}
