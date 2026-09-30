using System.IO;
using BusDriver.Core.Save;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace BusDriver.Tests.EditMode.Save {
    public class SaveMigrationTests {
        public class MetaV1 {
            public int runsStarted;
            public int runsLost;
        }

        // v0 called the counter "runs" and had no loss count
        sealed class RenameRunsV0 : ISaveMigration {
            public SaveSlot Slot { get { return SaveSlot.Meta; } }
            public int FromVersion { get { return 0; } }
            public void Migrate(JObject data) {
                data["runsStarted"] = data["runs"];
                data.Remove("runs");
                data["runsLost"] = 0;
            }
        }

        sealed class DoubleLossesV1 : ISaveMigration {
            public SaveSlot Slot { get { return SaveSlot.Meta; } }
            public int FromVersion { get { return 1; } }
            public void Migrate(JObject data) {
                data["runsLost"] = (int)data["runsLost"] * 2;
            }
        }

        SaveTestFolder folder;

        [SetUp]
        public void SetUp() {
            folder = new SaveTestFolder();
        }

        [TearDown]
        public void TearDown() {
            folder.Dispose();
        }

        void WriteV0Meta() {
            File.WriteAllText(folder.PathOf("meta.json"),
                "{ \"saveVersion\": 0, \"kind\": \"meta\", \"writtenUtc\": \"2026-01-01T00:00:00Z\", \"build\": \"0.0.1\", \"data\": { \"runs\": 4 } }");
        }

        [Test]
        public void V0ToV1StepRunsBeforeDeserializing() {
            WriteV0Meta();
            SaveService store = new SaveService(folder.Root, new SaveMigrations().Register(new RenameRunsV0()), "test");
            MetaV1 meta;
            Assert.IsTrue(store.TryLoad(SaveSlot.Meta, out meta));
            Assert.AreEqual(4, meta.runsStarted);
            Assert.AreEqual(0, meta.runsLost);
        }

        [Test]
        public void StepsRunInOrder() {
            File.WriteAllText(folder.PathOf("meta.json"),
                "{ \"saveVersion\": 0, \"kind\": \"meta\", \"data\": { \"runs\": 2 } }");
            SaveMigrations migrations = new SaveMigrations().SetCurrent(SaveSlot.Meta, 2)
                .Register(new DoubleLossesV1()).Register(new RenameRunsV0());
            JObject data = new JObject { ["runs"] = 2 };
            migrations.Migrate(SaveSlot.Meta, data, 0);
            Assert.AreEqual(2, (int)data["runsStarted"]);
            Assert.AreEqual(0, (int)data["runsLost"]);
        }

        [Test]
        public void SavingAfterAMigrationWritesTheCurrentVersion() {
            WriteV0Meta();
            SaveService store = new SaveService(folder.Root, new SaveMigrations().Register(new RenameRunsV0()), "test");
            MetaV1 meta;
            Assert.IsTrue(store.TryLoad(SaveSlot.Meta, out meta));
            store.Save(SaveSlot.Meta, meta);
            StringAssert.Contains("\"saveVersion\": 1", File.ReadAllText(folder.PathOf("meta.json")));
        }

        [Test]
        public void MissingStepRefusesAndLeavesTheFile() {
            WriteV0Meta();
            string before = File.ReadAllText(folder.PathOf("meta.json"));
            SaveService store = new SaveService(folder.Root, SaveMigrations.CreateDefault(), "test");
            MetaV1 meta;
            Assert.IsFalse(store.TryLoad(SaveSlot.Meta, out meta));
            Assert.AreEqual(before, File.ReadAllText(folder.PathOf("meta.json")));
            CollectionAssert.AreEqual(new[] { "meta.json" }, folder.Files());
        }

        [Test]
        public void DuplicateStepIsRejected() {
            SaveMigrations migrations = new SaveMigrations().Register(new RenameRunsV0());
            Assert.Throws<System.ArgumentException>(() => migrations.Register(new RenameRunsV0()));
        }

        [Test]
        public void EveryGameSlotStartsAtVersionOne() {
            SaveMigrations migrations = SaveMigrations.CreateDefault();
            foreach (SaveSlot slot in new[] { SaveSlot.Settings, SaveSlot.Meta, SaveSlot.Run }) {
                Assert.AreEqual(1, migrations.Current(slot), slot.ToString());
            }
        }
    }
}
