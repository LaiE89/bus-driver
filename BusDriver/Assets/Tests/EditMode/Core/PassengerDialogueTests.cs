using System;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    // The rider line tables (§4.13): the right kind speaks, a human never names their stop,
    // and the same seed draws the same lines.
    public class PassengerDialogueTests {
        static Random Stream() {
            return new RngStreams(1234).Get(RngStreams.Dialogue);
        }

        [Test]
        public void EveryMonsterIdMapsToItsOwnKind() {
            Assert.AreEqual(DialogueKind.Starer, PassengerDialogue.KindFor(MonsterIds.Starer));
            Assert.AreEqual(DialogueKind.Whisperer, PassengerDialogue.KindFor(MonsterIds.Whisperer));
            Assert.AreEqual(DialogueKind.Gazer, PassengerDialogue.KindFor(MonsterIds.Gazer));
            Assert.AreEqual(DialogueKind.Guest, PassengerDialogue.KindFor(MonsterIds.Guest));
            Assert.AreEqual(DialogueKind.Mimic, PassengerDialogue.KindFor(MonsterIds.Mimic));
        }

        [Test]
        public void AnUnknownIdSpeaksAsAHuman() {
            Assert.AreEqual(DialogueKind.Human, PassengerDialogue.KindFor(""));
            Assert.AreEqual(DialogueKind.Human, PassengerDialogue.KindFor(null));
        }

        [Test]
        public void AHumanGreetingNeverNamesTheStop() {
            Random rng = Stream();
            for (int i = 0; i < 80; i++) {
                string line = PassengerDialogue.Greeting(DialogueKind.Human, "Oak Hill", rng);
                Assert.IsFalse(line.Contains("{0}"), "an unformatted line reached the screen: " + line);
                Assert.IsFalse(line.Contains("Oak Hill"), line);
            }
        }

        [Test]
        public void AHumanWithNoDestinationStillGreets() {
            string line = PassengerDialogue.Greeting(DialogueKind.Human, "", Stream());
            Assert.IsNotEmpty(line);
            Assert.IsFalse(line.Contains("{0}"), "an unformatted line reached the screen: " + line);
        }

        // A monster never mentions a destination, so its table must have no placeholders
        [Test]
        public void MonsterLinesAreNeverFormatStrings() {
            Random rng = Stream();
            DialogueKind[] kinds = { DialogueKind.Starer, DialogueKind.Whisperer, DialogueKind.Mimic };
            foreach (DialogueKind kind in kinds) {
                for (int i = 0; i < 50; i++) {
                    Assert.IsFalse(PassengerDialogue.Greeting(kind, "Oak Hill", rng).Contains("Oak Hill"));
                    Assert.IsNotEmpty(PassengerDialogue.Seated(kind, rng));
                    Assert.IsNotEmpty(PassengerDialogue.Farewell(kind, ArrivalRating.Early, false, rng));
                }
            }
        }

        [Test]
        public void AKickedHumanComplainsAboutTheKick() {
            Random rng = Stream();
            for (int i = 0; i < 50; i++) {
                string line = PassengerDialogue.Farewell(DialogueKind.Human, ArrivalRating.Early, true, rng);
                Assert.IsFalse(line.IndexOf("star", StringComparison.OrdinalIgnoreCase) >= 0, line);
                Assert.IsTrue(
                    line.IndexOf("throw", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.IndexOf("dark", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.IndexOf("what did i do", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.IndexOf("leave me", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.IndexOf("surviv", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.IndexOf("manager", StringComparison.OrdinalIgnoreCase) >= 0,
                    line);
            }
        }

        [Test]
        public void EveryRatingHasAFarewell() {
            Random rng = Stream();
            foreach (ArrivalRating rating in Enum.GetValues(typeof(ArrivalRating))) {
                Assert.IsNotEmpty(PassengerDialogue.Farewell(DialogueKind.Human, rating, false, rng), "no line for " + rating);
            }
        }

        [Test]
        public void TheSameSeedDrawsTheSameLines() {
            Random first = Stream();
            Random second = Stream();
            for (int i = 0; i < 20; i++) {
                Assert.AreEqual(PassengerDialogue.Seated(DialogueKind.Human, first), PassengerDialogue.Seated(DialogueKind.Human, second));
            }
        }

        // Nothing throws without a stream: a rider with no night behind them still has a line
        [Test]
        public void AMissingStreamStillReturnsALine() {
            Assert.IsNotEmpty(PassengerDialogue.Seated(DialogueKind.Human, null));
            Assert.IsNotEmpty(PassengerDialogue.Greeting(DialogueKind.Human, "Oak Hill", null));
            Assert.IsNotEmpty(PassengerDialogue.Farewell(DialogueKind.Human, ArrivalRating.None, false, null));
        }
    }
}
