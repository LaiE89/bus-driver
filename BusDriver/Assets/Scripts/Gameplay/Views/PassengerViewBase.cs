using BusDriver.Core.Data;
using UnityEngine;

namespace BusDriver.Gameplay.Views {
    // The only thing passenger logic knows about visuals (§4.14). GreyboxPassengerView implements it
    // now and AnimatedPassengerView in Phase B. Logic keeps its own anchors (Anchor_Head for
    // observation, the interact collider); views never hold colliders.
    public abstract class PassengerViewBase : MonoBehaviour {
        // The visual head (IK, head tracking)
        public abstract Transform Head { get; }
        // Anchor_Face: scare framing
        public abstract Transform Face { get; }
        public abstract void SetPose(PassengerPose pose);
        public abstract void SetLocomotion(float metresPerSecond);
        // Unknown tells are ignored
        public abstract void SetTell(TellId tell, float intensity01);
        public abstract void SetLookAt(Transform target, float weight01);
        public abstract void PlayReaction(ReactionId reaction);
        public abstract void PlayDeath();
        public abstract void SetShadowCasting(bool on);
        // The Mimic's hide-from-one-camera (D39)
        public abstract void SetRenderLayer(int layer);
        public abstract void SetVisible(bool visible);
    }
}
