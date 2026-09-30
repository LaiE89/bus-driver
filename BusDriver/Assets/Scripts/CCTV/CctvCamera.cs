using BusDriver.Core.Util;
using UnityEngine;

namespace BusDriver.Gameplay.Bus {
    // One cabin camera (§4.6): its on-screen label, the range it observes riders at (§2.8, Cctv
    // observer 7 m), and which Mimic hide layer it doesn't render (§2.12, D39).
    [RequireComponent(typeof(Camera))]
    public sealed class CctvCamera : MonoBehaviour {
        [SerializeField] string label = "CAM 1";
        [SerializeField] float observeRange = 7f;
        [Tooltip("k in 1..3: this camera never renders layer MimicHideCam<k>")]
        [SerializeField] int hideLayerIndex = 1;

        Camera cam;

        public string Label { get { return label; } }
        public float ObserveRange { get { return observeRange; } }
        public int HideLayerIndex { get { return hideLayerIndex; } }
        public int HideLayer { get { return Layers.MimicHideCamBase + hideLayerIndex; } }

        // Lazy, since CCTVSystem may ask before this Awake has run
        public Camera Camera {
            get {
                if (cam == null) {
                    cam = GetComponent<Camera>();
                }
                return cam;
            }
        }

        void Awake() {
            Camera.cullingMask &= ~Layers.Mask(HideLayer);
        }
    }
}
