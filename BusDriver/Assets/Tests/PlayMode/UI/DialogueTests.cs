using System.Collections;
using BusDriver.Gameplay.Dialogue;
using BusDriver.Gameplay.Flow;
using BusDriver.Tests.PlayMode.Flow;
using BusDriver.UI.Hud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.UI {
    // The dialogue box (§4.13): a line types itself out and clears, a second line waits its turn,
    // and the panel is hidden whenever nobody is talking.
    public class DialogueTests {
        string saveRoot;
        DialogueService dialogue;
        DialogueView view;

        [SetUp]
        public void SetUp() {
            saveRoot = FlowTestUtil.NewSaveRoot();
        }

        [TearDown]
        public void TearDown() {
            FlowTestUtil.DeleteSaveRoot(saveRoot);
        }

        IEnumerator StartNight() {
            GameServices game = FlowTestUtil.Reboot(saveRoot).Services;
            game.Flow.NewRun();
            yield return FlowTestUtil.WaitForDriving(game);
            dialogue = Object.FindAnyObjectByType<DialogueService>();
            view = Object.FindAnyObjectByType<DialogueView>();
            Assert.IsNotNull(dialogue, "the night has no dialogue service");
            Assert.IsNotNull(view, "the HUD has no dialogue box");
            Assert.IsFalse(view.IsShown, "the box starts hidden");
        }

        [UnityTest]
        public IEnumerator ALineTypesItselfOutThenClears() {
            yield return StartNight();
            dialogue.Say("Passenger", "Evening, driver.");
            Assert.IsTrue(dialogue.IsPlaying);
            yield return FlowTestUtil.WaitFor(() => view.ShownText == "Evening, driver.", 5f, "the whole line on screen");
            Assert.IsTrue(view.IsShown, "the box should be up while a rider is talking");
            yield return FlowTestUtil.WaitFor(() => !dialogue.IsPlaying, 10f, "the line to finish");
            Assert.IsFalse(view.IsShown, "the box should hide once nobody is talking");
        }

        [UnityTest]
        public IEnumerator ASecondLineWaitsItsTurn() {
            yield return StartNight();
            dialogue.Say("Passenger", "First.");
            dialogue.Say("Passenger", "Second.");
            Assert.AreEqual(1, dialogue.QueuedCount, "the second line should be queued, not spoken over the first");
            yield return FlowTestUtil.WaitFor(() => view.ShownText == "Second.", 20f, "the queued line to be spoken");
            Assert.AreEqual(0, dialogue.QueuedCount);
        }

        [UnityTest]
        public IEnumerator ClearDropsEverythingAtOnce() {
            yield return StartNight();
            dialogue.Say("Passenger", "Evening, driver.");
            dialogue.Say("Passenger", "And another thing.");
            dialogue.Clear();
            Assert.IsFalse(dialogue.IsPlaying);
            Assert.AreEqual(0, dialogue.QueuedCount);
            Assert.AreEqual("", dialogue.VisibleText);
            yield return null;
            Assert.IsFalse(view.IsShown);
        }

        [UnityTest]
        public IEnumerator AnEmptyLineIsIgnored() {
            yield return StartNight();
            dialogue.Say("Passenger", "");
            dialogue.Say("Passenger", null);
            Assert.IsFalse(dialogue.IsPlaying);
            Assert.IsFalse(view.IsShown);
        }
    }
}
