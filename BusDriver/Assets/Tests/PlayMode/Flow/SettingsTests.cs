using System.Collections;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Flow {
    public class SettingsTests {
        string saveRoot;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        [UnityTest]
        public IEnumerator Settings_PersistAcrossBoot() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Settings.Current.mouseSensitivity = 137f;
            game.Settings.Current.invertY = true;
            game.Settings.Current.brightness = 0.25f;
            game.Settings.Save();
            yield return null;

            GameServices rebooted = FlowTestUtil.Reboot(saveRoot).Services;
            Assert.AreNotSame(game, rebooted);
            Assert.AreEqual(137f, rebooted.Settings.Current.mouseSensitivity);
            Assert.IsTrue(rebooted.Settings.Current.invertY);
            Assert.AreEqual(0.25f, rebooted.Settings.Current.brightness);
        }

        // The sensitivity set in Options reaches the driving look after New Run
        [UnityTest]
        public IEnumerator Settings_SensitivityReachesDriverLook() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Settings.Current.mouseSensitivity = 150f;
            game.Settings.Current.invertY = true;
            game.Settings.Save();

            game.Flow.NewRun();
            yield return FlowTestUtil.WaitForNight(game);

            DriverLook look = Object.FindAnyObjectByType<DriverLook>();
            Assert.IsNotNull(look);
            Assert.AreEqual(150f, look.Sensitivity);
            Assert.AreEqual(-1f, look.PitchSign);
            OnFootController onFoot = Object.FindAnyObjectByType<OnFootController>(FindObjectsInactive.Include);
            Assert.IsNotNull(onFoot);
            Assert.AreEqual(150f, onFoot.Sensitivity);
            // Ambient is the night preset's, scaled by the brightness (T-M2-05, D75)
            LightingPresetApplier lighting = Object.FindAnyObjectByType<LightingPresetApplier>();
            Assert.IsNotNull(lighting);
            Color ambient = RenderSettings.ambientLight;
            Assert.AreEqual(lighting.Preset.AmbientFor(game.Settings.Current.brightness).r, ambient.r, 1e-4, "the night scene didn't get the brightness");
        }
    }
}
