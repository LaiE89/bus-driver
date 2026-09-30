using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Scares;
using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.UI.Hud {
    // Draws ScareOverlayState (§2.17): the full-screen overlay flash, the CCTV static and the two
    // hands closing over the view. Its canvas sits over the HUD and under the screen fade, so a
    // blackout still covers everything. The static's shimmer is UI animation, on unscaled time.
    public sealed class ScareOverlayView : MonoBehaviour, IShiftBindable {
        [SerializeField] RawImage overlay;
        [SerializeField] RawImage staticNoise;
        [SerializeField] RectTransform handLeft;
        [SerializeField] RectTransform handRight;
        [Tooltip("Where the left hand starts (out of view) and ends (over the view), anchored at the centre; the right hand mirrors it")]
        [SerializeField] Vector2 handOpen = new Vector2(-1250f, -1500f);
        [SerializeField] Vector2 handClosed = new Vector2(-430f, -180f);

        ScareOverlayState state;

        // Tests read what is drawn
        public float OverlayAlpha { get { return overlay != null && overlay.enabled ? overlay.color.a : 0f; } }
        public float StaticAlpha { get { return staticNoise != null && staticNoise.enabled ? staticNoise.color.a : 0f; } }

        public void Bind(ShiftServices shift) {
            state = shift.ScareOverlay;
            Apply();
        }

        void LateUpdate() {
            Apply();
        }

        void Apply() {
            float overlayAlpha = state != null ? state.OverlayAlpha : 0f;
            float staticAlpha = state != null ? state.StaticAlpha : 0f;
            float hands = state != null ? state.HandsProgress : 0f;
            if (overlay != null) {
                overlay.enabled = overlayAlpha > 0.001f;
                if (overlay.enabled) {
                    overlay.texture = state.Overlay;
                    overlay.color = new Color(1f, 1f, 1f, overlayAlpha);
                }
            }
            if (staticNoise != null) {
                staticNoise.enabled = staticAlpha > 0.001f;
                if (staticNoise.enabled) {
                    float t = Time.unscaledTime;
                    staticNoise.color = new Color(1f, 1f, 1f, staticAlpha);
                    staticNoise.uvRect = new Rect(Mathf.Repeat(t * 37.3f, 1f), Mathf.Repeat(t * 23.9f, 1f), 3f, 2f);
                }
            }
            SetHand(handLeft, hands, 1f);
            SetHand(handRight, hands, -1f);
        }

        void SetHand(RectTransform hand, float progress, float side) {
            if (hand == null) {
                return;
            }
            bool shown = progress > 0.001f;
            if (hand.gameObject.activeSelf != shown) {
                hand.gameObject.SetActive(shown);
            }
            if (!shown) {
                return;
            }
            Vector2 position = Vector2.Lerp(handOpen, handClosed, Mathf.SmoothStep(0f, 1f, progress));
            position.x *= side;
            hand.anchoredPosition = position;
        }
    }
}
