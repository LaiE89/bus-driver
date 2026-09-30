using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.World;
using BusDriver.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BusDriver.Tests.PlayMode.Flow {
    // T-M2-05: the brightness setting reaches the night's ambient light through the route scene's
    // LightingPresetApplier, immediately
    public class LightingTests {
        string saveRoot;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        static LightingPresetApplier ActiveApplier() {
            Scene active = SceneManager.GetActiveScene();
            foreach (LightingPresetApplier applier in Object.FindObjectsByType<LightingPresetApplier>(FindObjectsInactive.Include)) {
                if (applier.gameObject.scene == active) {
                    return applier;
                }
            }
            return null;
        }

        static void AssertColor(Color expected, Color actual, string what) {
            Assert.AreEqual(expected.r, actual.r, 1e-4f, what + " (r)");
            Assert.AreEqual(expected.g, actual.g, 1e-4f, what + " (g)");
            Assert.AreEqual(expected.b, actual.b, 1e-4f, what + " (b)");
        }

        [UnityTest]
        public IEnumerator Lighting_BrightnessChangesNightAmbientImmediately() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun();
            yield return FlowTestUtil.WaitForNight(game);

            LightingPresetApplier applier = ActiveApplier();
            Assert.IsNotNull(applier, "the active (route) scene has no LightingPresetApplier");
            NightLightingPreset preset = applier.Preset;
            Assert.IsNotNull(preset);
            AssertColor(preset.AmbientFor(game.Settings.Current.brightness), RenderSettings.ambientLight, "ambient at night start");

            // Through the Options screen's own slider, as the player would
            OptionsScreen options = Object.FindAnyObjectByType<OptionsScreen>(FindObjectsInactive.Include);
            ScreenRouter router = Object.FindAnyObjectByType<ScreenRouter>(FindObjectsInactive.Include);
            Assert.IsNotNull(options);
            Assert.IsNotNull(router);
            router.Push(options);
            yield return null;
            Slider brightness = null;
            foreach (Slider slider in options.GetComponentsInChildren<Slider>(true)) {
                if (slider.name == "Brightness Slider") {
                    brightness = slider;
                }
            }
            Assert.IsNotNull(brightness, "no Brightness Slider on the Options screen");
            brightness.value = 0.3f;
            // Same frame: no yield
            AssertColor(preset.AmbientFor(0.3f), RenderSettings.ambientLight, "ambient after moving the slider");
            Assert.Greater(RenderSettings.ambientLight.r, preset.ambient.r, "brighter than the reference");
            Assert.AreEqual(preset.fogDensity, RenderSettings.fogDensity, 1e-6f);
        }

        [UnityTest]
        public IEnumerator Lighting_MenuUsesTheSamePreset() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun();
            yield return FlowTestUtil.WaitForNight(game);
            NightLightingPreset night = ActiveApplier().Preset;

            game.Flow.QuitToMenu();
            yield return FlowTestUtil.WaitForMenu(game);
            LightingPresetApplier menu = ActiveApplier();
            Assert.IsNotNull(menu, "the menu has no LightingPresetApplier");
            Assert.AreSame(night, menu.Preset, "the menu and the night use different presets");
            AssertColor(night.AmbientFor(game.Settings.Current.brightness), RenderSettings.ambientLight, "menu ambient");
        }
    }
}
