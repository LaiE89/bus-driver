using System;
using System.IO;
using System.Text.RegularExpressions;
using BusDriver.Core.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.EditMode.Save {
    public class SaveStoreTests {
        public enum Mood { Calm = 0, Spooked = 1 }

        public class Sample {
            public int walletCents;
            public string name;
            public Mood mood;
            public string[] slots;
        }

        static readonly DateTime FixedNow = new DateTime(2026, 10, 1, 20, 15, 0, DateTimeKind.Utc);

        SaveTestFolder folder;
        SaveService store;

        [SetUp]
        public void SetUp() {
            folder = new SaveTestFolder();
            store = new SaveService(folder.Root, SaveMigrations.CreateDefault(), "0.1.0 (test)", () => FixedNow);
        }

        [TearDown]
        public void TearDown() {
            folder.Dispose();
        }

        static Sample Make(int cents) {
            return new Sample { walletCents = cents, name = "run", mood = Mood.Spooked, slots = new[] { "coffee", null, "salt" } };
        }

        [Test]
        public void RoundTripLeavesNoTempFile() {
            store.Save(SaveSlot.Run, Make(3150));
            Sample loaded;
            Assert.IsTrue(store.TryLoad(SaveSlot.Run, out loaded));
            Assert.AreEqual(3150, loaded.walletCents);
            Assert.AreEqual(Mood.Spooked, loaded.mood);
            CollectionAssert.AreEqual(new[] { "coffee", null, "salt" }, loaded.slots);
            CollectionAssert.AreEqual(new[] { "run.json" }, folder.Files());
        }

        [Test]
        public void SecondSaveKeepsTheFirstAsBackup() {
            store.Save(SaveSlot.Run, Make(1));
            store.Save(SaveSlot.Run, Make(2));
            CollectionAssert.AreEqual(new[] { "run.json", "run.json.bak" }, folder.Files());
            Sample loaded;
            Assert.IsTrue(store.TryLoad(SaveSlot.Run, out loaded));
            Assert.AreEqual(2, loaded.walletCents);
        }

        [Test]
        public void EnvelopeMatchesTheSpec() {
            store.Save(SaveSlot.Run, Make(3150));
            string json = File.ReadAllText(folder.PathOf("run.json"));
            StringAssert.Contains("\"saveVersion\": 1", json);
            StringAssert.Contains("\"kind\": \"run\"", json);
            StringAssert.Contains("\"writtenUtc\": \"2026-10-01T20:15:00Z\"", json);
            StringAssert.Contains("\"build\": \"0.1.0 (test)\"", json);
            // Enums by name
            StringAssert.Contains("\"mood\": \"Spooked\"", json);
        }

        [Test]
        public void CrashBetweenWriteAndReplaceLeavesTheOldFileIntact() {
            store.Save(SaveSlot.Run, Make(100));
            store.AfterTempWritten = tmp => throw new IOException("simulated crash");
            Assert.Throws<IOException>(() => store.Save(SaveSlot.Run, Make(200)));
            store.AfterTempWritten = null;

            Sample loaded;
            Assert.IsTrue(store.TryLoad(SaveSlot.Run, out loaded));
            Assert.AreEqual(100, loaded.walletCents);
            // The next save simply overwrites the leftover .tmp
            store.Save(SaveSlot.Run, Make(300));
            Assert.IsTrue(store.TryLoad(SaveSlot.Run, out loaded));
            Assert.AreEqual(300, loaded.walletCents);
            CollectionAssert.DoesNotContain(folder.Files(), "run.json.tmp");
        }

        [Test]
        public void CorruptFileFallsBackToTheBackup() {
            store.Save(SaveSlot.Run, Make(1));
            store.Save(SaveSlot.Run, Make(2));
            File.WriteAllText(folder.PathOf("run.json"), "{ \"saveVersion\": 1, \"kind\": \"run\", \"data\": { trunc");
            Sample loaded;
            Assert.IsTrue(store.TryLoad(SaveSlot.Run, out loaded));
            Assert.AreEqual(1, loaded.walletCents);
        }

        [Test]
        public void BothCorruptReturnsDefaultAndKeepsACorruptCopy() {
            store.Save(SaveSlot.Meta, Make(1));
            store.Save(SaveSlot.Meta, Make(2));
            File.WriteAllText(folder.PathOf("meta.json"), "not json");
            File.WriteAllText(folder.PathOf("meta.json.bak"), "");
            LogAssert.Expect(LogType.Error, new Regex(@"meta\.json is unreadable"));

            Sample loaded;
            Assert.IsFalse(store.TryLoad(SaveSlot.Meta, out loaded));
            Assert.IsNull(loaded);
            Assert.IsFalse(File.Exists(folder.PathOf("meta.json")));
            CollectionAssert.Contains(folder.Files(), "meta.json.corrupt-20261001201500");
            Assert.AreEqual(0, store.LoadOrNew<Sample>(SaveSlot.Meta).walletCents);
        }

        [Test]
        public void NewerSaveVersionIsRefusedAndUntouched() {
            string newer = "{ \"saveVersion\": 2, \"kind\": \"run\", \"writtenUtc\": \"x\", \"build\": \"9.9\", \"data\": { \"walletCents\": 5 } }";
            File.WriteAllText(folder.PathOf("run.json"), newer);
            DateTime stamp = File.GetLastWriteTimeUtc(folder.PathOf("run.json"));

            Sample loaded;
            Assert.IsFalse(store.TryLoad(SaveSlot.Run, out loaded));
            Assert.AreEqual(newer, File.ReadAllText(folder.PathOf("run.json")));
            Assert.AreEqual(stamp, File.GetLastWriteTimeUtc(folder.PathOf("run.json")));
            CollectionAssert.AreEqual(new[] { "run.json" }, folder.Files());
        }

        [Test]
        public void WrongKindIsTreatedAsCorrupt() {
            store.Save(SaveSlot.Settings, Make(7));
            File.Copy(folder.PathOf("settings.json"), folder.PathOf("run.json"));
            LogAssert.Expect(LogType.Error, new Regex(@"run\.json is unreadable"));
            Sample loaded;
            Assert.IsFalse(store.TryLoad(SaveSlot.Run, out loaded));
        }

        [Test]
        public void MissingFileIsNotAnError() {
            Sample loaded;
            Assert.IsFalse(store.TryLoad(SaveSlot.Run, out loaded));
            Assert.IsFalse(store.Exists(SaveSlot.Run));
        }

        [Test]
        public void DeleteRemovesTheFileAndItsBackup() {
            store.Save(SaveSlot.Run, Make(1));
            store.Save(SaveSlot.Run, Make(2));
            store.Delete(SaveSlot.Run);
            Assert.IsEmpty(folder.Files());
            Assert.IsFalse(store.Exists(SaveSlot.Run));
        }

        [Test]
        public void UnknownMembersAreIgnored() {
            File.WriteAllText(folder.PathOf("run.json"),
                "{ \"saveVersion\": 1, \"kind\": \"run\", \"data\": { \"walletCents\": 9, \"fromTheFuture\": true } }");
            Sample loaded;
            Assert.IsTrue(store.TryLoad(SaveSlot.Run, out loaded));
            Assert.AreEqual(9, loaded.walletCents);
        }
    }
}
