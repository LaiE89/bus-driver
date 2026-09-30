using System.IO;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BusDriver.Tests.EditMode.Flow {
    // §4.11 rules that don't need a scene
    public class PauseServiceTests {
        InputActionAsset copy;
        InputService input;
        PauseService pause;

        [SetUp]
        public void SetUp() {
            copy = InputActionAsset.FromJson(File.ReadAllText("Assets/Input/BusDriver.inputactions"));
            input = new InputService(copy, null);
            pause = new PauseService(input);
            input.SetContext(InputContext.Driving);
        }

        [TearDown]
        public void TearDown() {
            copy.Disable();
            Object.DestroyImmediate(copy);
        }

        // Only the refusals here: a successful pause writes Time.timeScale, which in edit mode
        // dirties ProjectSettings/TimeManager.asset. PauseTests (PlayMode) cover the rest.
        [Test]
        public void PausingNeedsTheSceneRootsPermission() {
            Assert.IsFalse(pause.TrySetPaused(true), "no CanPause means no pause");
            pause.CanPause = () => false;
            Assert.IsFalse(pause.TrySetPaused(true));
            Assert.IsFalse(pause.IsPaused);
            Assert.IsTrue(pause.TrySetPaused(false), "unpausing is always allowed");
        }

        [Test]
        public void TheCursorIsLockedOnlyWhileThePlayerControlsTheBusOrBody() {
            Assert.IsTrue(CursorService.IsLocked(InputContext.Driving));
            Assert.IsTrue(CursorService.IsLocked(InputContext.OnFoot));
            Assert.IsTrue(CursorService.IsLocked(InputContext.Cinematic));
            Assert.IsFalse(CursorService.IsLocked(InputContext.Menu));
            Assert.IsFalse(CursorService.IsLocked(InputContext.Screen));
            Assert.IsFalse(CursorService.IsLocked(InputContext.None));
        }
    }
}
