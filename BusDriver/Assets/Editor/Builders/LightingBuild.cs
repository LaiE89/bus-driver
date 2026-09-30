using BusDriver.Core.Data;
using BusDriver.Gameplay.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static BusDriver.Editor.Builders.BuilderUtil;

namespace BusDriver.Editor.Builders {
    // The scene builders' side of NightLightingPreset (T-M2-05): every generated scene bakes the
    // same preset into its RenderSettings, and the scenes the player sees lit (Menu, Route01_World)
    // get a LightingPresetApplier with the moon and the global post volume.
    public static class LightingBuild {
        public static string PresetPath {
            get { return DataSeeder.DataRoot + "/" + LightingSeed.PresetRelativePath; }
        }

        // DataSeeder runs before the scene builders, so a missing preset is a builder bug
        public static NightLightingPreset Preset {
            get {
                NightLightingPreset preset = AssetDatabase.LoadAssetAtPath<NightLightingPreset>(PresetPath);
                if (preset == null) {
                    throw new System.InvalidOperationException("no " + PresetPath + "; DataSeeder runs before the scene builders");
                }
                return preset;
            }
        }

        // The open scene's RenderSettings at the reference brightness
        public static void BakeRenderSettings() {
            NightLightingPreset preset = Preset;
            LightingPresetApplier.ApplyRenderSettings(preset, preset.referenceBrightness);
        }

        public static LightingPresetApplier CreateApplier(Transform parent) {
            NightLightingPreset preset = Preset;
            BakeRenderSettings();
            GameObject root = Group("Lighting", parent);
            LightingPresetApplier applier = root.AddComponent<LightingPresetApplier>();

            Light moon = Group("Moon", root.transform).AddComponent<Light>();
            LightingPresetApplier.ApplyMoon(preset, moon);

            Volume volume = Group("Post Volume", root.transform).AddComponent<Volume>();
            volume.isGlobal = true;
            // Under the CCTV grade (priority 10) and the sanity effects
            volume.priority = 0f;
            volume.sharedProfile = preset.postProfile as VolumeProfile;

            SetRef(applier, "preset", preset);
            SetRef(applier, "moon", moon);
            SetRef(applier, "postVolume", volume);
            return applier;
        }
    }
}
