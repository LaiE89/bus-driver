using UnityEngine;

namespace BusDriver.Core.Data {
    // The night's look (§4.8, Data/Lighting/Night): flat ambient, fog, the moon and the scene post
    // profile. The Menu and every night scene use this one asset through LightingPresetApplier, so
    // the menu diorama and the road always match. Seeded with the MVP values.
    public sealed class NightLightingPreset : ScriptableObject {
        [Tooltip("Flat ambient at the reference brightness")]
        public Color ambient = new Color(0.1f, 0.1f, 0.12f, 1f);
        [Tooltip("The brightness setting at which the ambient is exactly `ambient` (the §2.23 default). "
            + "Ambient scales linearly with brightness / referenceBrightness (D75)")]
        public float referenceBrightness = 0.1f;
        public float reflectionIntensity = 0f;

        [Header("Fog")]
        public bool fog = true;
        public FogMode fogMode = FogMode.ExponentialSquared;
        public float fogDensity = 0.012f;
        public Color fogColor = new Color(0.02f, 0.025f, 0.04f, 1f);

        [Header("Moon")]
        [Tooltip("Euler x (pitch) and y (yaw) of the directional moon light")]
        public Vector2 moonRotation = new Vector2(50f, -30f);
        public float moonIntensity = 0.08f;
        public Color moonColor = new Color(0.6f, 0.7f, 1f, 1f);
        public LightShadows moonShadows = LightShadows.Soft;

        [Header("Post-processing")]
        [Tooltip("The scene's global VolumeProfile (typed loosely: Core doesn't reference the render pipeline, D75)")]
        public ScriptableObject postProfile;

        public Color AmbientFor(float brightness) {
            if (referenceBrightness <= 0f) {
                return ambient;
            }
            Color scaled = ambient * (Mathf.Max(0f, brightness) / referenceBrightness);
            scaled.a = 1f;
            return scaled;
        }
    }
}
