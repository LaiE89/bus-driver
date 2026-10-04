using System;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    // The rider line tables (§4.13): the right kind speaks, humans name their stop, and the same
    // seed draws the same lines.
    public class PassengerDialogueTests {
        static Random Stream() {
            return new RngStreams(1234).Get(RngStreams.Dialogue);
        }

        [Test]
        public void EveryMonsterIdMapsToItsOwnKind() {
            Assert.AreEqual(DialogueKind.Starer, PassengerDialogue.KindFor(MonsterIds.Starer));
            Assert.AreEqual(DialogueKind.Whisperer, PassengerDialogue.KindFor(MonsterIds.Whisperer));
            Assert.AreEqual(DialogueKind.WeepingAngel, PassengerDialogue.KindFor(MonsterIds.WeepingAngel));
        }

        // D34: it is pretending to be a passenger, so it has to sound like one
        [Test]
        public void TheMimicAndAnUnknownIdBothSpeakAsHumans() {
            Assert.AreEqual(DialogueKind.Human, PassengerDialogue.KindFor(MonsterIds.Mimic));
            Assert.AreEqual(DialogueKind.Human, PassengerDialogue.KindFor(""));
            Assert.AreEqual(DialogueKind.Human, PassengerDialogue.KindFor(null));
        }

        [Test]
        public void AHumanGreetingNamesTheStop() {
            Random rng = Stream();
            for (int i = 0; i < 50; i++) {
                StringAssert.Contains("Oak Hill", PassengerDialogue.Greeting(DialogueKind.Human, "Oak Hill", rng));
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
            DialogueKind[] kinds = { DialogueKind.Starer, DialogueKind.Whisperer, DialogueKind.WeepingAngel };
            foreach (DialogueKind kind in kinds) {
                for (int i = 0; i < 50; i++) {
                    Assert.IsFalse(PassengerDialogue.Greeting(kind, "Oak Hill", rng).Contains("Oak Hill"));
                    Assert.IsNotEmpty(PassengerDialogue.Seated(kind, rng));
                    Assert.IsNotEmpty(PassengerDialogue.Farewell(kind, ArrivalRating.Early, false, rng));
                    Assert.IsNotEmpty(PassengerDialogue.Refused(kind, rng));
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
                    line.IndexOf("throwing", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.IndexOf("fare", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.IndexOf("dark", StringComparison.OrdinalIgnoreCase) >= 0,
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
