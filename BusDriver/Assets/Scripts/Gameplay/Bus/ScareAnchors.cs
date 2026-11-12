using UnityEngine;

namespace BusDriver.Gameplay.Bus {
    // The bus's named scare anchors (§4.8): DriverShoulder, DriverWindow, DriverView, CabinCenter
    // and CctvLens:<n> (n = 1, the front CCTV). Each anchor's +Z faces what it frames.
    // PrefabBuilder fills the fixed references. DriverView is created in front of the driver
    // camera, so a face there sits in the middle of the view.
    public sealed class ScareAnchors : MonoBehaviour {
        public const string DriverShoulder = "DriverShoulder";
        public const string DriverWindow = "DriverWindow";
        public const string DriverView = "DriverView";
        public const string CabinCenter = "CabinCenter";
        // "CctvLens" alone means the scare's own camera; "CctvLens:1" means CAM 1
        public const string CctvLens = "CctvLens";
        // Same gap as a CCTV lens scare: the face fills the view without crossing the near clip
        const float DriverViewDistance = 0.35f;

        [SerializeField] Transform driverShoulder;
        [SerializeField] Transform driverWindow;
        [SerializeField] Transform cabinCenter;
        [Tooltip("In front of CAM 1 (front cabin camera)")]
        [SerializeField] Transform[] cctvLens = new Transform[0];

        Transform driverView;

        public int LensCount { get { return cctvLens.Length; } }

        // cctvIndex: the 0-based camera a bare "CctvLens" means (−1: none)
        public Transform Resolve(string anchor, int cctvIndex) {
            if (string.IsNullOrEmpty(anchor)) {
                return null;
            }
            switch (anchor) {
                case DriverShoulder: return driverShoulder;
                case DriverWindow: return driverWindow;
                case DriverView: return DriverViewAnchor();
                case CabinCenter: return cabinCenter;
                case CctvLens: return Lens(cctvIndex);
            }
            if (anchor.StartsWith(CctvLens + ":", System.StringComparison.Ordinal)) {
                int n;
                if (int.TryParse(anchor.Substring(CctvLens.Length + 1), out n)) {
                    return Lens(n - 1);
                }
            }
            return null;
        }

        // A valid anchor string (the content validator's check)
        public static bool IsValidName(string anchor) {
            if (anchor == DriverShoulder || anchor == DriverWindow || anchor == DriverView || anchor == CabinCenter || anchor == CctvLens) {
                return true;
            }
            int n;
            return anchor != null && anchor.StartsWith(CctvLens + ":", System.StringComparison.Ordinal)
                && int.TryParse(anchor.Substring(CctvLens.Length + 1), out n) && n == 1;
        }

        Transform Lens(int index) {
            return index >= 0 && index < cctvLens.Length ? cctvLens[index] : null;
        }

        // Child of the driver camera, dead ahead, so the head stays centered as the view moves
        Transform DriverViewAnchor() {
            if (driverView != null) {
                return driverView;
            }
            CCTVSystem cctv = transform.root.GetComponentInChildren<CCTVSystem>(true);
            Camera home = cctv != null ? cctv.HomeCamera : null;
            if (home == null) {
                return null;
            }
            GameObject node = new GameObject("Anchor_Scare_DriverView");
            driverView = node.transform;
            driverView.SetParent(home.transform, false);
            driverView.localPosition = new Vector3(0f, 0f, DriverViewDistance);
            driverView.localRotation = Quaternion.identity;
            return driverView;
        }
    }
}
