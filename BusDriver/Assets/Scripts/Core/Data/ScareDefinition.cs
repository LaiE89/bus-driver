using System;
using UnityEngine;

namespace BusDriver.Core.Data {
    // One timed step of a scare (§4.8, D36). Steps start `at` seconds into the scare and may run
    // concurrently; the scare ends when the last one does. Which fields matter depends on the kind:
    // - PlaySound: soundId (3D at the anchor when one is given); duration > 0 stops it early
    // - ShowOverlay: overlay (null = the greybox flash), intensity = its opacity, for duration
    // - CameraShake: intensity (0–1) for duration
    // - FlickerCabinLights / CabinLightsOff: for duration
    // - CctvStatic: full-screen static at intensity, for duration
    // - LockInput: the player's input is off for duration
    // - ForceHomeView: back to the driver view
    // - CutToCctv: to camera param ("1".."3"), or the scare's own camera when empty
    // - ShowScareHead: the scare head at anchor for duration; param "lookAt" turns the driver's
    //   head to it
    // - AllPassengersReact: every rider aboard plays param's ReactionId (TurnToCamera when empty)
    //   and, for TurnToCamera, faces the camera in use for duration
    // - Blackout: fades to black over duration
    // - Wait: nothing, it only takes time
    // - HandsOverCamera: two dark hands close over the view from the bottom corners over duration
    [Serializable]
    public struct ScareStep {
        [Tooltip("Seconds after the scare starts")]
        public float at;
        public ScareStepKind kind;
        [Tooltip("Seconds; 0 for an instant step")]
        public float duration;
        [Tooltip("PlaySound: a SoundIds id")]
        public string soundId;
        [Tooltip("ShowOverlay: a full-screen texture (T_Scare_*, Appendix A.5)")]
        public Texture2D overlay;
        [Tooltip("ShowOverlay and CctvStatic: opacity; CameraShake: amplitude (0–1)")]
        [Range(0f, 1f)] public float intensity;
        [Tooltip("DriverShoulder, DriverWindow, CabinCenter, CctvLens:<n> or CctvLens (the scare's camera) (§4.8)")]
        public string anchor;
        [Tooltip("Per kind: CutToCctv's camera, ShowScareHead's \"lookAt\", AllPassengersReact's ReactionId")]
        public string param;

        public float End { get { return at + Mathf.Max(0f, duration); } }
    }

    // A scare as data (§2.17, §4.8, D36): its tier and its steps, played by ScarePlayer once
    // ScareDirector accepts it. A Timeline asset may replace the steps in Phase B.
    public sealed class ScareDefinition : ScriptableObject {
        [Tooltip("lower_snake_case segments joined by dots, e.g. scare.starer.lens")]
        public string id = "";
        public ScareTier tier = ScareTier.Startle;
        public ScareStep[] steps = new ScareStep[0];
        [Tooltip("Phase B: a Timeline (PlayableAsset) that replaces the steps. Typed loosely because Core doesn't reference Timeline")]
        public ScriptableObject timelineOverride;
        [Tooltip("Off: the tier's default sanity cost (Monster −5, Startle −2, none otherwise, §2.15)")]
        public bool overrideSanityCost;
        public float sanityCost;

        // When the last step ends, in seconds
        public float Duration {
            get {
                float end = 0f;
                for (int i = 0; i < steps.Length; i++) {
                    end = Mathf.Max(end, steps[i].End);
                }
                return end;
            }
        }

        // The sanity change this scare costs when it plays (§2.15): negative
        public float SanityCost(BalanceConfig balance) {
            if (overrideSanityCost) {
                return sanityCost;
            }
            switch (tier) {
                case ScareTier.Monster: return balance != null ? balance.monsterScareSanity : -5f;
                case ScareTier.Startle: return balance != null ? balance.startleSanity : -2f;
                default: return 0f;
            }
        }
    }
}
