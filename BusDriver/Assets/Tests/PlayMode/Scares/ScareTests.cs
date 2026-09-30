using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Scares;
using BusDriver.Tests.PlayMode.Flow;
using BusDriver.UI.Hud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Scares {
    // ScarePlayer and ScareDirector (T-M4-05, §2.17): every step kind runs and puts back what it
    // changed, Reduced intensity caps flashes and drops shake, and the director gates requests
    public class ScareTests {
        const int Seed = 20260930;

        string saveRoot;
        readonly List<ScriptableObject> created = new List<ScriptableObject>();

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            Time.timeScale = 1f;
            foreach (ScriptableObject asset in created) {
                Object.Destroy(asset);
            }
            created.Clear();
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        static IEnumerator StartNight(string saveRoot, ShiftServices[] result) {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForDriving(game);
            result[0] = Object.FindAnyObjectByType<ShiftContext>().Shift;
        }

        ScareDefinition Scare(string id, ScareTier tier, params ScareStep[] steps) {
            ScareDefinition scare = ScriptableObject.CreateInstance<ScareDefinition>();
            scare.id = id;
            scare.tier = tier;
            scare.steps = steps;
            created.Add(scare);
            return scare;
        }

        static ScareStep Step(float at, ScareStepKind kind, float duration = 0f, float intensity = 0f, string soundId = "", string anchor = "", string param = "") {
            return new ScareStep { at = at, kind = kind, duration = duration, intensity = intensity, soundId = soundId, anchor = anchor, param = param };
        }

        static int ScareHeads() {
            int count = 0;
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)) {
                if (go.name.StartsWith("ScareHead_Greybox") && go.layer == BusDriver.Core.Util.Layers.ScareFx) {
                    count++;
                }
            }
            return count;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Scares_StepsExecute() {
            ShiftServices[] started = new ShiftServices[1];
            yield return StartNight(saveRoot, started);
            ShiftServices shift = started[0];
            Assert.IsNotNull(shift.DebugRiders.SpawnSeated(new RiderSpec { lookId = "look01", boardStopId = "farm_gate", destinationStopId = "church" }));

            ScareDefinition scare = Scare("test.every_step", ScareTier.Startle,
                Step(0f, ScareStepKind.PlaySound, soundId: SoundIds.CctvSwitch),
                Step(0f, ScareStepKind.ShowOverlay, 0.4f, 1f),
                Step(0f, ScareStepKind.CameraShake, 0.4f, 0.8f),
                Step(0f, ScareStepKind.FlickerCabinLights, 0.4f),
                Step(0f, ScareStepKind.LockInput, 0.8f),
                Step(0f, ScareStepKind.CutToCctv, param: "2"),
                Step(0f, ScareStepKind.ShowScareHead, 0.5f, anchor: "CctvLens"),
                Step(0.1f, ScareStepKind.CctvStatic, 0.3f, 1f),
                Step(0.1f, ScareStepKind.AllPassengersReact, 0.3f),
                Step(0.5f, ScareStepKind.CabinLightsOff, 0.2f),
                Step(0.6f, ScareStepKind.HandsOverCamera, 0.3f),
                Step(0.8f, ScareStepKind.ForceHomeView),
                Step(0.8f, ScareStepKind.Blackout, 0.2f),
                Step(0.9f, ScareStepKind.Wait, 0.2f));
            Assert.AreEqual(1.1f, scare.Duration, 1e-5f);

            int viewBefore = shift.Cctv.ActiveIndex;
            InputContext inputBefore = shift.Game.Input.Context;
            Quaternion lookBefore = shift.DriverLook.transform.localRotation;
            Assert.AreEqual(-1, viewBefore, "the night starts on the driver view");
            Assert.AreEqual(InputContext.Driving, inputBefore);
            Assert.AreEqual(1f, shift.CabinLights.Level);
            Assert.AreEqual(0f, shift.Fade.Alpha);

            bool sawOverlay = false, sawStatic = false, sawShake = false, sawFlicker = false, sawOff = false;
            bool sawCut = false, sawLock = false, sawHead = false, sawHands = false, sawBlack = false, sawHome = false;
            ScareHandle handle = shift.ScarePlayer.Begin(scare, default(ScareContext));
            Assert.IsFalse(handle.IsNone);
            while (shift.ScarePlayer.IsRunning(handle)) {
                ScareOverlayState state = shift.ScareOverlay;
                sawOverlay |= state.OverlayAlpha > 0.99f;
                sawStatic |= state.StaticAlpha > 0.99f;
                sawShake |= shift.Shake.Amplitude > 0.7f;
                sawFlicker |= shift.CabinLights.IsFlickering;
                sawOff |= shift.CabinLights.Level == 0f;
                sawCut |= shift.Cctv.ActiveIndex == 1;
                sawHome |= sawCut && shift.Cctv.ActiveIndex == -1;
                sawLock |= shift.Game.Input.Context == InputContext.Cinematic;
                sawHead |= ScareHeads() == 1;
                sawHands |= state.HandsProgress > 0.5f;
                sawBlack |= shift.Fade.Alpha > 0.99f;
                yield return null;
            }
            Assert.IsTrue(sawOverlay, "ShowOverlay");
            Assert.IsTrue(sawStatic, "CctvStatic");
            Assert.IsTrue(sawShake, "CameraShake");
            Assert.IsTrue(sawFlicker, "FlickerCabinLights");
            Assert.IsTrue(sawOff, "CabinLightsOff");
            Assert.IsTrue(sawCut, "CutToCctv to CAM 2");
            Assert.IsTrue(sawHome, "ForceHomeView");
            Assert.IsTrue(sawLock, "LockInput");
            Assert.IsTrue(sawHead, "ShowScareHead");
            Assert.IsTrue(sawHands, "HandsOverCamera");
            Assert.IsTrue(sawBlack, "Blackout");

            // Everything it changed is back
            yield return null;
            Assert.AreEqual(viewBefore, shift.Cctv.ActiveIndex, "camera view");
            Assert.AreEqual(inputBefore, shift.Game.Input.Context, "input");
            Assert.AreEqual(1f, shift.CabinLights.Level, "cabin lights");
            Assert.IsFalse(shift.CabinLights.IsFlickering || shift.CabinLights.IsOff);
            Assert.AreEqual(0f, shift.Fade.Alpha, "blackout");
            Assert.AreEqual(0f, shift.Shake.Amplitude, "shake");
            Assert.AreEqual(0f, shift.ScareOverlay.OverlayAlpha);
            Assert.AreEqual(0f, shift.ScareOverlay.StaticAlpha);
            Assert.AreEqual(0f, shift.ScareOverlay.HandsProgress);
            Assert.AreEqual(0, ScareHeads(), "the scare head is gone");
            Assert.AreEqual(lookBefore, shift.DriverLook.transform.localRotation, "driver look");

            // An interrupted scare puts things back too
            ScareHandle cut = shift.ScarePlayer.Begin(Scare("test.cut", ScareTier.Startle,
                Step(0f, ScareStepKind.CutToCctv, param: "3"), Step(0f, ScareStepKind.CabinLightsOff, 5f), Step(0f, ScareStepKind.LockInput, 5f)), default(ScareContext));
            yield return null;
            Assert.AreEqual(2, shift.Cctv.ActiveIndex);
            Assert.AreEqual(0f, shift.CabinLights.Level);
            shift.ScarePlayer.Interrupt(cut);
            Assert.IsFalse(shift.ScarePlayer.IsRunning(cut));
            yield return null;
            Assert.AreEqual(-1, shift.Cctv.ActiveIndex);
            Assert.AreEqual(1f, shift.CabinLights.Level);
            Assert.AreEqual(InputContext.Driving, shift.Game.Input.Context);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Scares_ReducedIntensity() {
            ShiftServices[] started = new ShiftServices[1];
            yield return StartNight(saveRoot, started);
            ShiftServices shift = started[0];
            shift.Game.Settings.Current.scareIntensity = ScareIntensity.Reduced;
            Assert.IsTrue(shift.ScarePlayer.Reduced);
            ScareOverlayView view = Object.FindAnyObjectByType<ScareOverlayView>();
            Assert.IsNotNull(view, "the HUD draws the scare overlay");

            ScareDefinition scare = Scare("test.reduced", ScareTier.Startle,
                Step(0f, ScareStepKind.ShowOverlay, 0.6f, 1f),
                Step(0f, ScareStepKind.CctvStatic, 0.6f, 1f),
                Step(0f, ScareStepKind.CameraShake, 0.6f, 1f),
                Step(0f, ScareStepKind.PlaySound, soundId: SoundIds.ScareStartleSting));
            float peakOverlay = 0f;
            float peakStatic = 0f;
            ScareHandle handle = shift.ScarePlayer.Begin(scare, default(ScareContext));
            while (shift.ScarePlayer.IsRunning(handle)) {
                peakOverlay = Mathf.Max(peakOverlay, shift.ScareOverlay.OverlayAlpha, view.OverlayAlpha);
                peakStatic = Mathf.Max(peakStatic, shift.ScareOverlay.StaticAlpha, view.StaticAlpha);
                Assert.AreEqual(0f, shift.Shake.Amplitude, "no camera shake at Reduced");
                yield return null;
            }
            Assert.LessOrEqual(peakOverlay, 0.5f + 1e-4f, "overlay alpha");
            Assert.LessOrEqual(peakStatic, 0.5f + 1e-4f, "static alpha");
            Assert.Greater(peakOverlay, 0.4f, "the overlay still shows, faded in");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Scares_DirectorGatesRequests() {
            ShiftServices[] started = new ShiftServices[1];
            yield return StartNight(saveRoot, started);
            ShiftServices shift = started[0];
            ScareDefinition first = Scare("test.startle_a", ScareTier.Startle, Step(0f, ScareStepKind.Wait, 0.3f));
            ScareDefinition second = Scare("test.startle_b", ScareTier.Startle, Step(0f, ScareStepKind.Wait, 0.3f));
            ScareDefinition kill = Scare("test.kill", ScareTier.Kill, Step(0f, ScareStepKind.Wait, 0.3f));
            List<string> startedIds = new List<string>();
            shift.Scares.OnScareStarted += scare => startedIds.Add(scare.id);

            Assert.IsTrue(shift.Scares.Request(first, default(ScareContext)), "a Startle plays in Driving");
            Assert.IsTrue(shift.ScarePlayer.IsPlaying);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(shift.Scares.Request(second, default(ScareContext)), "the 6 s global gap drops a Startle");
            Assert.AreEqual(ScareReason.GlobalGap, shift.Scares.LastDecision.Reason);
            shift.Scares.TelegraphActive = true;
            Assert.IsTrue(shift.Scares.Request(kill, default(ScareContext)), "a Kill plays even during a telegraph");
            shift.Scares.TelegraphActive = false;
            CollectionAssert.AreEqual(new[] { "test.startle_a", "test.kill" }, startedIds);
            Assert.AreEqual(-5f, shift.Game.Config.Scare("scare.starer.lens").SanityCost(shift.Balance), "a Monster scare costs 5 sanity");
            Assert.AreEqual(-2f, first.SanityCost(shift.Balance), "a Startle costs 2");
        }
    }
}
