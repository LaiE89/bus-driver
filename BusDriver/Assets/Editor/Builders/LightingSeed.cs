using BusDriver.Core.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusDriver.Editor.Builders {
    // Seed data for Data/Lighting (§4.8, T-M2-05): the night preset with the MVP's values, and the
    // scene post profile. The MVP had no scene volume (only the CCTV grade, which CCTVSystem makes
    // at runtime), so the profile starts empty: the look doesn't change, and grading lands here.
    public static class LightingSeed {
        public const string PresetRelativePath = "Lighting/Night.asset";
        public const string PostRelativePath = "Lighting/NightPost.asset";

        public static void FillPreset(NightLightingPreset preset) {
            preset.ambient = new Color(0.1f, 0.1f, 0.12f, 1f);
            preset.referenceBrightness = 0.1f;
            preset.reflectionIntensity = 0f;
            preset.fog = true;
            preset.fogMode = FogMode.ExponentialSquared;
            preset.fogDensity = 0.012f;
            preset.fogColor = new Color(0.02f, 0.025f, 0.04f, 1f);
            preset.moonRotation = new Vector2(50f, -30f);
            preset.moonIntensity = 0.08f;
            preset.moonColor = new Color(0.6f, 0.7f, 1f, 1f);
            preset.moonShadows = LightShadows.Soft;
        }

        public static void FillPost(VolumeProfile profile) {
        }

        // Adoption only fills an empty reference (DataSeeder)
        public static void Adopt(string root) {
            NightLightingPreset preset = AssetDatabase.LoadAssetAtPath<NightLightingPreset>(root + "/" + PresetRelativePath);
            if (preset != null && preset.postProfile == null) {
                preset.postProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(root + "/" + PostRelativePath);
                EditorUtility.SetDirty(preset);
            }
        }
    }
}
