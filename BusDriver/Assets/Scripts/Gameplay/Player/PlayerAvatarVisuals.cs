using BusDriver.Core.Util;
using UnityEngine;

namespace BusDriver.Gameplay.Player {
    // Layer helpers for the player's body (D49). The body itself is a PlayerAvatarViewBase that
    // PrefabBuilder generates on the bus and the OnFootRig (T-M1-14); these only keep it on the
    // PlayerAvatar layer and out of the first-person cameras.
    public static class PlayerAvatarVisuals {
        public const string HeadLayerName = "PlayerAvatar";
        public const int AvatarLayerIndex = Layers.PlayerAvatar;

        public static int AvatarLayer {
            get { return AvatarLayerIndex; }
        }

        public static void ApplyCullLayer(Transform avatarRoot, Transform headAnchor = null) {
            if (avatarRoot != null) {
                SetLayer(avatarRoot.gameObject, AvatarLayer);
            }
            if (headAnchor != null) {
                Transform headVisual = headAnchor.Find("HeadVisual");
                if (headVisual != null) {
                    SetLayer(headVisual.gameObject, AvatarLayer);
                }
            }
        }

        // First-person cameras should not draw the avatar; CCTV still should.
        public static void HideAvatarFromCamera(Camera camera) {
            if (camera == null) {
                return;
            }
            camera.cullingMask &= ~Layers.Mask(AvatarLayer);
        }

        static void SetLayer(GameObject go, int layer) {
            if (go == null || layer < 0) {
                return;
            }
            go.layer = layer;
            foreach (Transform child in go.transform) {
                SetLayer(child.gameObject, layer);
            }
        }
    }
}
