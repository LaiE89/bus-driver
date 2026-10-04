using System.Collections;
using BusDriver.Core.Data;
using BusDriver.Core.Save;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Shift;
using BusDriver.Tests.PlayMode.Flow;
using BusDriver.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Death {
    // DeathDirector, its presenters and the Game Over screen (T-M4-06, §2.14, §2.21, §4.4)
    public class DeathTests {
        const int Seed = 20260930;

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

        static ShiftServices Shift() {
            return Object.FindAnyObjectByType<ShiftContext>().Shift;
        }

        static IEnumerator WaitForGameOver() {
            return FlowTestUtil.WaitFor(() => {
                ShiftDirector director = FlowTestUtil.Director();
                return director != null && director.State == ShiftState.GameOver;
            }, 20f, "Game Over");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Death_DeletesRunSave() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForDriving(game);
            Assert.IsTrue(game.Saves.Exists(SaveSlot.Run), "New Run writes run.json");
            ShiftServices shift = Shift();
            DeathReport started = null;
            shift.Death.OnDeathStarted += report => started = report;

            Assert.IsTrue(shift.Death.Die(DeathCause.MonsterKill, "starer"));
            // D21: gone before the presenter has done anything
            Assert.IsFalse(game.Saves.Exists(SaveSlot.Run), "run.json is deleted at once");
            Assert.IsFalse(shift.Death.IsPresented);
            Assert.AreEqual(ShiftState.Dying, shift.Director.State);
            Assert.AreEqual(InputContext.Cinematic, game.Input.Context);
            Assert.IsNotNull(started);
            Assert.AreEqual(DeathCause.MonsterKill, started.Cause);
            Assert.AreEqual("starer", started.SourceId);
            Assert.IsFalse(shift.Death.Die(DeathCause.Fall), "only one death");

            // The meta save records it straight away (§2.20, D21)
            MetaProgress meta;
            Assert.IsTrue(game.Saves.TryLoad(SaveSlot.Meta, out meta));
            Assert.AreEqual(1, meta.runsLost);
            Assert.AreEqual(1, meta.deathsByCause[DeathCause.MonsterKill]);
            Assert.AreEqual(1, meta.journal["starer"].timesKilledBy);

            yield return WaitForGameOver();
            Assert.IsTrue(shift.Death.IsPresented);
            Assert.AreEqual(1f, shift.Fade.Alpha, "the screen stays black behind Game Over");
            Assert.IsFalse(game.Saves.Exists(SaveSlot.Run));
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator GameOver_ShowsCauseAndHint_AndNewRunWorks() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun(Seed);
            yield return FlowTestUtil.WaitForDriving(game);

            Shift().Death.Die(DeathCause.Fall);
            yield return WaitForGameOver();
            GameOverScreen screen = Object.FindAnyObjectByType<GameOverScreen>();
            Assert.IsNotNull(screen);
            Assert.IsTrue(screen.IsOpen, "Game Over opens after the presenter");
            Assert.AreEqual("YOU WENT OVER THE EDGE", screen.CauseText);
            Assert.AreEqual("Keep your eyes on the road at Dead Man's Bend.", screen.HintText);
            StringAssert.Contains("NIGHTS SURVIVED  0", screen.StatsText);
            Assert.AreSame(screen.NewRunButton.gameObject, EventSystem.current.currentSelectedGameObject, "New Run has focus");
            Assert.IsFalse(game.Pause.TrySetPaused(true), "Game Over can't be paused");
            Assert.AreEqual(InputContext.Screen, game.Input.EffectiveContext);

            // New Run from Game Over
            screen.NewRunButton.onClick.Invoke();
            yield return FlowTestUtil.WaitForDriving(game);
            Assert.AreEqual(1, Object.FindObjectsByType<GameRoot>().Length, "one GameRoot");
            Assert.AreEqual(1, game.Flow.Run.nightIndex);
            Assert.AreEqual(2, game.Meta.Current.runsStarted);
            Assert.AreEqual(0f, Shift().Fade.Alpha, "the new night isn't black");

            Shift().Death.Die(DeathCause.MonsterKill, "starer");
            yield return WaitForGameOver();
            screen = Object.FindAnyObjectByType<GameOverScreen>();
            Assert.AreEqual("THE STARER GOT YOU", screen.CauseText);
            Assert.AreEqual("It only moves when you aren't looking.", screen.HintText);

            screen.NewRunButton.onClick.Invoke();
            yield return FlowTestUtil.WaitForDriving(game);
            Shift().Death.Die(DeathCause.SanityZero);
            yield return WaitForGameOver();
            screen = Object.FindAnyObjectByType<GameOverScreen>();
            Assert.AreEqual("YOUR MIND WENT DARK", screen.CauseText);
            Assert.AreEqual("The dark gets in. Coffee helps.", screen.HintText, "no Whisperer aboard");

            screen.MainMenuButton.onClick.Invoke();
            yield return FlowTestUtil.WaitForMenu(game);
            Assert.AreEqual(3, game.Meta.Current.runsLost);
        }
    }
}
