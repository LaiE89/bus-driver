using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Shift;
using BusDriver.Tests.PlayMode.Flow;
using BusDriver.UI.Hud;
using BusDriver.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Death {
    // The cliff (T-M4-09, §2.14 Fall, D14): driving off Dead Man's Bend plays the fall cam and
    // ends the run
    public class FallDeathTests {
        const int Seed = 20260930;
        const float StartAt = 1420f;
        const float SwerveAt = 1490f;

        string saveRoot;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            Time.timeScale = 1f;
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Cliff_DriveOff_FallDeath() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForDriving(game);
            ShiftContext night = Object.FindAnyObjectByType<ShiftContext>();
            ShiftServices shift = night.Shift;
            FallDeathPresenter presenter = Object.FindAnyObjectByType<FallDeathPresenter>();
            Assert.IsNotNull(presenter, "Night_Systems has a fall presenter");
            Assert.IsNotNull(presenter.FallCamera, "wired to the FallCamera instance");
            Assert.IsFalse(presenter.FallCamera.enabled);

            // Just before the bend, in the right lane
            RouteDefinition route = shift.Tracker.Route;
            RoutePose pose = shift.Tracker.Path.Evaluate(StartAt);
            night.Bus.PlaceAt(pose.Offset(route.RoadWidthAt(StartAt) * 0.25f, 0.3f), Quaternion.LookRotation(pose.Tangent, Vector3.up));
            yield return new WaitForSeconds(1f);
            AutoPilot pilot = shift.AutoPilot;
            pilot.Engage(true);
            DeathReport report = null;
            shift.Death.OnDeathStarted += r => report = r;
            Time.timeScale = 2f;
            yield return FlowTestUtil.WaitFor(() => shift.Tracker.DistanceAlong >= SwerveAt, 60f, "reaching the bend");
            Assert.IsTrue(route.InZone(RouteZoneKind.Cliff, shift.Tracker.DistanceAlong), "inside the cliff zone");
            // The lateral offset command: over the left edge, where there's no guardrail
            pilot.LaneOffset = -14f;
            yield return FlowTestUtil.WaitFor(() => report != null, 60f, "the fall death");
            pilot.Engage(false);

            Assert.AreEqual(DeathCause.Fall, report.Cause);
            Assert.AreEqual(ShiftState.Dying, shift.Director.State);
            Assert.AreEqual(InputContext.Cinematic, game.Input.Context, "input locks");
            Assert.AreEqual(0.5f, Time.timeScale, 0.001f, "slow motion");
            Assert.IsTrue(presenter.FallCamera.enabled, "the fall camera takes over");
            Assert.IsFalse(shift.DriverCamera.enabled, "the driver camera is off");
            Assert.IsFalse(shift.EngineSound.enabled, "the engine stops");

            // Mid-fall, for a person to look at
            float start = Time.realtimeSinceStartup;
            yield return FlowTestUtil.WaitFor(() => Time.realtimeSinceStartup - start > 1.2f, 5f, "the fall");
            Assert.AreEqual(1f, Time.timeScale, 0.001f, "back to full speed after a second");
            ScreenFadeView fadeView = Object.FindAnyObjectByType<ScreenFadeView>();
            CaptureUtil.CaptureWithCanvas("fall", presenter.FallCamera, fadeView.GetComponent<Canvas>());

            yield return FlowTestUtil.WaitFor(() => shift.Director.State == ShiftState.GameOver, 30f, "Game Over");
            Assert.AreEqual(FallDeathPresenter.Caption, shift.Fade.Caption, "the caption over the black");
            Assert.AreEqual(1f, shift.Fade.Alpha);
            Assert.IsTrue(presenter.Landed, "the hull hit the valley floor");
            Assert.AreEqual(0, shift.Tracker.RespawnCount, "the KillPlane leaves a fall alone");
            Assert.AreEqual(1f, Time.timeScale, 0.001f);
            GameOverScreen screen = Object.FindAnyObjectByType<GameOverScreen>();
            Assert.IsTrue(screen.IsOpen);
            Assert.AreEqual("YOU WENT OVER THE EDGE", screen.CauseText);
            Assert.AreEqual(1, game.Meta.Current.deathsByCause[DeathCause.Fall]);
        }
    }
}
