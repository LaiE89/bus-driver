using UnityEngine;

namespace BusDriver.Gameplay.Bus {
    // The bus's named scare anchors (§4.8): DriverShoulder, DriverWindow, CabinCenter and CctvLens:<n>
    // (n = 1..3, the CCTV camera). Each anchor's +Z faces what it frames. PrefabBuilder fills the
    // references, so ScarePlayer resolves a step's anchor string without a lookup by name.
    public sealed class ScareAnchors : MonoBehaviour {
        public const string DriverShoulder = "DriverShoulder";
        public const string DriverWindow = "DriverWindow";
        public const string CabinCenter = "CabinCenter";
        // "CctvLens" alone means the scare's own camera; "CctvLens:2" means CAM 2
        public const string CctvLens = "CctvLens";

        [SerializeField] Transform driverShoulder;
        [SerializeField] Transform driverWindow;
        [SerializeField] Transform cabinCenter;
        [Tooltip("In front of CAM 1, CAM 2, CAM 3")]
        [SerializeField] Transform[] cctvLens = new Transform[0];

        public int LensCount { get { return cctvLens.Length; } }

        // cctvIndex: the 0-based camera a bare "CctvLens" means (−1: none)
        public Transform Resolve(string anchor, int cctvIndex) {
            if (string.IsNullOrEmpty(anchor)) {
                return null;
            }
            switch (anchor) {
                case DriverShoulder: return driverShoulder;
                case DriverWindow: return driverWindow;
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
            if (anchor == DriverShoulder || anchor == DriverWindow || anchor == CabinCenter || anchor == CctvLens) {
                return true;
            }
            int n;
            return anchor != null && anchor.StartsWith(CctvLens + ":", System.StringComparison.Ordinal)
                && int.TryParse(anchor.Substring(CctvLens.Length + 1), out n) && n >= 1 && n <= 3;
        }

        Transform Lens(int index) {
            return index >= 0 && index < cctvLens.Length ? cctvLens[index] : null;
        }
    }
}
