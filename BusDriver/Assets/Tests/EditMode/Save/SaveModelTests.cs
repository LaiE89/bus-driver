using BusDriver.Core.Data;
using BusDriver.Core.Save;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Save {
    public class SaveModelTests {
        // The §2.23 settings table
        [Test]
        public void SettingsDefaultsMatchTheSpec() {
            SettingsData s = new SettingsData();
            Assert.AreEqual(0.8f, s.masterVolume);
            Assert.AreEqual(0.8f, s.musicVolume);
            Assert.AreEqual(0.8f, s.ambienceVolume);
            Assert.AreEqual(0.8f, s.sfxVolume);
            Assert.AreEqual(0.8f, s.voiceVolume);
            Assert.AreEqual(60f, s.mouseSensitivity);
            Assert.IsFalse(s.invertY);
            Assert.AreEqual("", s.bindingOverridesJson);
            Assert.AreEqual(SettingsData.QualityMedium, s.qualityLevel);
            Assert.AreEqual(0.1f, s.brightness);
            Assert.AreEqual(ScareIntensity.Full, s.scareIntensity);
            Assert.IsTrue(s.hintsEnabled);
            Assert.IsTrue(s.captionsEnabled);
            Assert.IsFalse(s.warningAcknowledged);
        }

        [Test]
        public void NewRunStartsAtNightOneWithFullSanityAndNoMoney() {
            RunState run = RunState.NewRun(99);
            Assert.AreEqual(99, run.seed);
            Assert.AreEqual(1, run.nightIndex);
            Assert.IsFalse(run.nightInProgress);
            Assert.AreEqual(100f, run.sanity);
            Assert.AreEqual(0, run.walletCents);
            Assert.AreEqual(3, run.slots.Length);
            Assert.IsEmpty(run.owned);
            Assert.IsEmpty(run.nights);
        }
    }
}
