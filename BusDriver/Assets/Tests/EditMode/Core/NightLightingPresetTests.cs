using BusDriver.Core.Data;
using BusDriver.Editor.Builders;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Core {
    // T-M2-05: ambient scales with brightness around the reference, and the seed holds the MVP look
    public class NightLightingPresetTests {
        [Test]
        public void Ambient_IsExactAtTheReferenceBrightness() {
            NightLightingPreset preset = ScriptableObject.CreateInstance<NightLightingPreset>();
            LightingSeed.FillPreset(preset);
            Color at = preset.AmbientFor(preset.referenceBrightness);
            Assert.AreEqual(0.1f, at.r, 1e-6f);
            Assert.AreEqual(0.12f, at.b, 1e-6f);
            Color doubled = preset.AmbientFor(preset.referenceBrightness * 2f);
            Assert.AreEqual(0.2f, doubled.r, 1e-6f);
            Assert.AreEqual(0f, preset.AmbientFor(0f).r, 1e-6f);
            Assert.AreEqual(1f, preset.AmbientFor(0.5f).a, "ambient alpha");
            Object.DestroyImmediate(preset);
        }

        [Test]
        public void Seed_KeepsTheMvpNight() {
            NightLightingPreset preset = ScriptableObject.CreateInstance<NightLightingPreset>();
            LightingSeed.FillPreset(preset);
            Assert.AreEqual(FogMode.ExponentialSquared, preset.fogMode);
            Assert.AreEqual(0.012f, preset.fogDensity, 1e-6f);
            Assert.AreEqual(0.08f, preset.moonIntensity, 1e-6f);
            Assert.AreEqual(new Vector2(50f, -30f), preset.moonRotation);
            Object.DestroyImmediate(preset);
        }

        [Test]
        public void SeededAsset_ExistsWithItsPostProfile() {
            NightLightingPreset preset = AssetDatabase.LoadAssetAtPath<NightLightingPreset>(LightingBuild.PresetPath);
            Assert.IsNotNull(preset, "run BuildAll: " + LightingBuild.PresetPath + " is missing");
            Assert.IsNotNull(preset.postProfile, "the preset has no post profile");
        }
    }
}
