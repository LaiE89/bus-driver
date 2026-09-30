using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Core {
    // The threat meter (§2.9, T-M4-02): stages, grace, freezing and the hold at 99.9 (§2.14)
    public class ThreatMeterTests {
        [TestCase(0f, ThreatStage.Dormant)]
        [TestCase(24.99f, ThreatStage.Dormant)]
        [TestCase(25f, ThreatStage.Unsettled)]
        [TestCase(49.99f, ThreatStage.Unsettled)]
        [TestCase(50f, ThreatStage.Aggressive)]
        [TestCase(99.99f, ThreatStage.Aggressive)]
        [TestCase(100f, ThreatStage.Lethal)]
        public void StageBoundaries(float value, ThreatStage expected) {
            Assert.AreEqual(expected, ThreatMeterCore.StageOf(value));
            Assert.AreEqual(expected, new ThreatMeterCore(value).Stage);
        }

        [Test]
        public void ValueIsClampedTo0And100() {
            ThreatMeterCore meter = new ThreatMeterCore();
            meter.Add(-5f);
            Assert.AreEqual(0f, meter.Value);
            meter.Add(250f);
            Assert.AreEqual(100f, meter.Value);
            Assert.AreEqual(ThreatStage.Lethal, meter.Stage);
        }

        [Test]
        public void TickMovesAtTheRate() {
            ThreatMeterCore meter = new ThreatMeterCore();
            for (int i = 0; i < 10; i++) {
                meter.Tick(0.5f, 1.5f);
            }
            Assert.AreEqual(7.5f, meter.Value, 1e-4f);
            meter.Tick(1f, -5f);
            Assert.AreEqual(2.5f, meter.Value, 1e-4f);
        }

        [Test]
        public void StageChangesRaiseFromAndToOncePerMove() {
            ThreatMeterCore meter = new ThreatMeterCore(20f);
            List<string> changes = new List<string>();
            meter.OnStageChanged += (from, to) => changes.Add(from + ">" + to);
            meter.Add(4f);
            Assert.AreEqual(0, changes.Count, "24 is still Dormant");
            meter.Add(1f);
            meter.Add(10f);
            meter.SetValue(100f);
            meter.SetValue(60f);
            meter.SetValue(10f);
            CollectionAssert.AreEqual(new[] {
                "Dormant>Unsettled", "Unsettled>Lethal", "Lethal>Aggressive", "Aggressive>Dormant",
            }, changes);
        }

        [Test]
        public void GraceHoldsTheMeterForItsSecondsThenTheRestOfTheFrameCounts() {
            ThreatMeterCore meter = new ThreatMeterCore();
            meter.StartGrace(10f);
            Assert.IsTrue(meter.InGrace);
            for (int i = 0; i < 9; i++) {
                meter.Tick(1f, 1.5f);
            }
            Assert.AreEqual(0f, meter.Value, "still in grace after 9 s");
            meter.Tick(1.5f, 2f);
            Assert.IsFalse(meter.InGrace);
            Assert.AreEqual(1f, meter.Value, 1e-4f, "only the 0.5 s past grace moves it");
        }

        [Test]
        public void GraceHoldsNegativeRatesToo() {
            ThreatMeterCore meter = new ThreatMeterCore(40f);
            meter.StartGrace(10f);
            meter.Tick(5f, -5f);
            Assert.AreEqual(40f, meter.Value);
        }

        [Test]
        public void AFrozenMeterDoesntMoveOrUseUpGrace() {
            ThreatMeterCore meter = new ThreatMeterCore(30f);
            meter.StartGrace(2f);
            meter.Frozen = true;
            meter.Tick(5f, 10f);
            Assert.AreEqual(30f, meter.Value);
            Assert.AreEqual(2f, meter.GraceRemaining);
            meter.Frozen = false;
            meter.Tick(3f, 10f);
            Assert.AreEqual(40f, meter.Value, 1e-4f);
        }

        [Test]
        public void AHeldMeterStopsAt99Point9AndReachesLethalOnceReleased() {
            ThreatMeterCore meter = new ThreatMeterCore(95f);
            meter.HeldBelowLethal = true;
            meter.Tick(10f, 1.5f);
            Assert.AreEqual(ThreatMeterCore.HeldCeiling, meter.Value, 1e-4f);
            Assert.AreEqual(ThreatStage.Aggressive, meter.Stage);
            meter.HeldBelowLethal = false;
            meter.Tick(1f, 1.5f);
            Assert.AreEqual(ThreatStage.Lethal, meter.Stage);
        }

        [Test]
        public void TheHoldCapsRisesOnly() {
            ThreatMeterCore meter = new ThreatMeterCore(100f);
            meter.HeldBelowLethal = true;
            meter.Tick(1f, 1f);
            Assert.AreEqual(ThreatStage.Lethal, meter.Stage, "a Lethal meter isn't pulled back by the hold");
            meter.SetValue(60f);
            Assert.AreEqual(60f, meter.Value, "a reset downward lands");
            meter.Tick(1f, -5f);
            Assert.AreEqual(55f, meter.Value, 1e-4f);
        }
    }
}
