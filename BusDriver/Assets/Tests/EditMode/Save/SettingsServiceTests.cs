using System.IO;
using BusDriver.Core.Save;
using BusDriver.Gameplay.Flow;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Save {
    public class SettingsServiceTests {
        SaveTestFolder folder;
        SaveService store;

        [SetUp]
        public void SetUp() {
            folder = new SaveTestFolder();
            store = new SaveService(folder.Root, SaveMigrations.CreateDefault(), "test");
        }

        [TearDown]
        public void TearDown() {
            folder.Dispose();
        }

        [Test]
        public void MissingFileGivesTheDefaults() {
            SettingsService settings = new SettingsService(store, null);
            Assert.AreEqual(60f, settings.Current.mouseSensitivity);
            Assert.AreEqual(0.8f, settings.Current.masterVolume);
            Assert.IsFalse(File.Exists(folder.PathOf("settings.json")), "loading must not write");
        }

        [Test]
        public void SavedValuesComeBack() {
            SettingsService first = new SettingsService(store, null);
            first.Current.mouseSensitivity = 123f;
            first.Current.invertY = true;
            first.Save();
            SettingsService second = new SettingsService(store, null);
            Assert.AreEqual(123f, second.Current.mouseSensitivity);
            Assert.IsTrue(second.Current.invertY);
        }

        [Test]
        public void OutOfRangeValuesAreClampedOnLoad() {
            store.Save(SaveSlot.Settings, new SettingsData { mouseSensitivity = 5000f, masterVolume = 3f, brightness = -1f });
            SettingsService settings = new SettingsService(store, null);
            Assert.AreEqual(200f, settings.Current.mouseSensitivity);
            Assert.AreEqual(1f, settings.Current.masterVolume);
            Assert.AreEqual(0f, settings.Current.brightness);
        }

        [TestCase(0, 30, 0)]
        [TestCase(1, 60, 0)]
        [TestCase(2, 120, 0)]
        [TestCase(3, -1, 0)]
        [TestCase(4, -1, 1)]
        public void TargetFpsIndexMapsLikeTheOldMenu(int index, int frameRate, int vSync) {
            int actualRate;
            int actualVSync;
            SettingsService.FrameRateFor(index, out actualRate, out actualVSync);
            Assert.AreEqual(frameRate, actualRate);
            Assert.AreEqual(vSync, actualVSync);
        }

        [Test]
        public void VolumeConvertsToDecibels() {
            Assert.AreEqual(0f, SettingsService.LinearToDecibels(1f), 1e-4);
            Assert.AreEqual(-6.0206f, SettingsService.LinearToDecibels(0.5f), 1e-3);
            Assert.AreEqual(-80f, SettingsService.LinearToDecibels(0f), 1e-3);
        }
    }
}
