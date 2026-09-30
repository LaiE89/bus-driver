using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BusDriver.Gameplay.World {
    // Puts a NightLightingPreset on its scene (§4.8): RenderSettings, the moon and the global post
    // volume, with ambient × the brightness setting (§2.23). The builders bake the preset into the
    // scene file too; this re-applies it at runtime and whenever brightness changes.
    public sealed class LightingPresetApplier : MonoBehaviour, IGameBindable {
        [SerializeField] NightLightingPreset preset;
        [SerializeField] Light moon;
        [SerializeField] Volume postVolume;

        SettingsService settings;

        public NightLightingPreset Preset { get { return preset; } }

        public void Bind(GameServices game) {
            settings = game.Settings;
            settings.OnBrightnessChanged += Apply;
            Apply(settings.Current.brightness);
        }

        void OnDestroy() {
            if (settings != null) {
                settings.OnBrightnessChanged -= Apply;
            }
        }

        public void Apply(float brightness) {
            if (preset == null) {
                return;
            }
            ApplyMoon(preset, moon);
            if (postVolume != null) {
                postVolume.sharedProfile = preset.postProfile as VolumeProfile;
            }
            // RenderSettings are the active scene's; a night's route scene becomes active only
            // after it's bound, and applies again then (RunFlow)
            if (gameObject.scene == SceneManager.GetActiveScene()) {
                ApplyRenderSettings(preset, brightness);
            }
        }

        // Also used by the builders, at the reference brightness, to bake the scene's settings
        public static void ApplyRenderSettings(NightLightingPreset preset, float brightness) {
            RenderSettings.skybox = null;
            // Flat ambient is what makes the brightness setting do anything
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = preset.AmbientFor(brightness);
            RenderSettings.reflectionIntensity = preset.reflectionIntensity;
            RenderSettings.fog = preset.fog;
            RenderSettings.fogMode = preset.fogMode;
            RenderSettings.fogColor = preset.fogColor;
            RenderSettings.fogDensity = preset.fogDensity;
        }

        public static void ApplyMoon(NightLightingPreset preset, Light light) {
            if (light == null) {
                return;
            }
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(preset.moonRotation.x, preset.moonRotation.y, 0f);
            light.intensity = preset.moonIntensity;
            light.color = preset.moonColor;
            light.shadows = preset.moonShadows;
        }
    }
}
