using BusDriver.Core.Data;
using UnityEngine;

namespace BusDriver.Gameplay.Views {
    // The player's visible body (D49): seated at the wheel and standing on foot. Only CCTV and the
    // Mirror ever see it; everything under the view stays on the PlayerAvatar layer (19), which
    // the driver and on-foot cameras cull. Phase B swaps the greybox for an art view (Appendix A.1).
    public abstract class PlayerAvatarViewBase : MonoBehaviour {
        public abstract void SetPose(PassengerPose pose);
        public abstract void SetLocomotion(float metresPerSecond);
        public abstract void SetVisible(bool visible);
    }
}
