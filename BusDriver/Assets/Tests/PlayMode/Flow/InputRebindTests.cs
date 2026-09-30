using System.Collections;
using BusDriver.Core.Save;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Input;
using BusDriver.UI.Hud;
using BusDriver.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Flow {
    // T-M1-07: a rebind reaches the HUD prompt, is written to settings.json and survives a restart
    public class InputRebindTests {
        string saveRoot;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        static int KeyboardIndex(InputAction action) {
            for (int i = 0; i < action.bindings.Count; i++) {
                if (action.bindings[i].groups == InputService.KeyboardMouseGroup) {
                    return i;
                }
            }
            return -1;
        }

        [UnityTest]
        public IEnumerator Rebind_CycleCameraToC_PersistsAndShowsInTheHud() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun();
            yield return FlowTestUtil.WaitForNight(game);
            yield return null;
            DrivingHUD hud = Object.FindAnyObjectByType<DrivingHUD>();
            StringAssert.Contains("SPACE CAMERAS", hud.ControlsHint);

            string error;
            Assert.IsTrue(game.Input.TryApplyBinding("CycleCamera", KeyboardIndex(game.Input.Actions.CycleCamera), "<Keyboard>/c", out error), error);
            StringAssert.Contains("C CAMERAS", hud.ControlsHint, "the HUD prompt follows the rebind");

            SaveService disk = new SaveService(saveRoot, SaveMigrations.CreateDefault(), "test");
            SettingsData saved;
            Assert.IsTrue(disk.TryLoad(SaveSlot.Settings, out saved), "settings.json was not written");
            StringAssert.Contains("<Keyboard>/c", saved.bindingOverridesJson);

            // A restart on the same save root brings the rebind back
            game = FlowTestUtil.Reboot(saveRoot).Services;
            StringAssert.AreEqualIgnoringCase("C", game.Input.GetDisplayString("CycleCamera"));
            game.Input.ResetAll();
        }

        [UnityTest]
        public IEnumerator ControlsScreen_ListsEveryRebindableBindingAndFollowsRebinds() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun();
            yield return FlowTestUtil.WaitForNight(game);
            yield return null;
            PauseScreen pause = Object.FindAnyObjectByType<PauseScreen>();
            pause.Back();
            pause.OpenOptions();
            pause.OpenControls();
            yield return null;
            ControlsScreen controls = Object.FindAnyObjectByType<ControlsScreen>();
            Assert.IsNotNull(controls, "the Controls screen should be open");
            Assert.AreEqual(game.Input.RebindableBindings().Count, controls.RowCount);

            int index = KeyboardIndex(game.Input.Actions.CycleCamera);
            Assert.AreEqual("SPACE", controls.KeyTextFor("Driving/CycleCamera", index));
            string error;
            game.Input.TryApplyBinding("CycleCamera", index, "<Keyboard>/c", out error);
            Assert.AreEqual("C", controls.KeyTextFor("Driving/CycleCamera", index));
            controls.ResetKeybinds();
            Assert.AreEqual("SPACE", controls.KeyTextFor("Driving/CycleCamera", index));
        }
    }
}
