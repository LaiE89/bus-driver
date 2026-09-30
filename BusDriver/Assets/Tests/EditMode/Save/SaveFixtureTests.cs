using System;
using System.Collections.Generic;
using System.IO;
using BusDriver.Core.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Save {
    // Every fixture set under Tests/Fixtures/v<N>/ must load through the current code, with no
    // field lost (§4.9). From G1 on, each save-model change adds a migration and a new set.
    public class SaveFixtureTests {
        static string FixturesRoot {
            get { return Path.Combine(Application.dataPath, "Tests", "Fixtures"); }
        }

        public static IEnumerable<string> FixtureFiles() {
            List<string> files = new List<string>();
            foreach (string dir in Directory.GetDirectories(FixturesRoot, "v*")) {
                foreach (string file in Directory.GetFiles(dir, "*.json")) {
                    files.Add(Path.GetFileName(dir) + "/" + Path.GetFileName(file));
                }
            }
            files.Sort(StringComparer.Ordinal);
            return files;
        }

        [Test]
        public void EveryCurrentKindHasAFixture() {
            foreach (string kind in new[] { "run", "meta", "settings" }) {
                Assert.IsTrue(File.Exists(Path.Combine(FixturesRoot, "v1", kind + ".json")), "v1/" + kind + ".json");
            }
        }

        [TestCaseSource(nameof(FixtureFiles))]
        public void FixtureLoadsAndRoundTripsWithNoFieldLost(string fixture) {
            string source = Path.Combine(FixturesRoot, fixture);
            string kind = Path.GetFileNameWithoutExtension(fixture);
            JObject expected = (JObject)JObject.Parse(File.ReadAllText(source))["data"];

            using (SaveTestFolder folder = new SaveTestFolder()) {
                File.Copy(source, folder.PathOf(kind + ".json"));
                SaveService store = new SaveService(folder.Root, SaveMigrations.CreateDefault(), "test");
                object loaded = Load(store, kind);
                Assert.IsNotNull(loaded, fixture + " didn't load");
                // Through text, as a real save goes, so floats compare as written
                JObject actual = JObject.Parse(JsonConvert.SerializeObject(loaded, SaveJson.CreateSettings()));
                List<string> lost = new List<string>();
                CollectLost(expected, actual, "data", lost);
                Assert.IsEmpty(lost, fixture + " lost or changed: " + string.Join(", ", lost));
            }
        }

        static object Load(SaveService store, string kind) {
            switch (kind) {
                case "run": {
                    RunState run;
                    return store.TryLoad(SaveSlot.Run, out run) ? run : null;
                }
                case "meta": {
                    MetaProgress meta;
                    return store.TryLoad(SaveSlot.Meta, out meta) ? meta : null;
                }
                case "settings": {
                    SettingsData settings;
                    return store.TryLoad(SaveSlot.Settings, out settings) ? settings : null;
                }
                default:
                    Assert.Fail("unknown fixture kind " + kind);
                    return null;
            }
        }

        // Every value in the fixture must come back unchanged at the same path
        static void CollectLost(JToken expected, JToken actual, string path, List<string> lost) {
            if (actual == null) {
                lost.Add(path);
                return;
            }
            if (expected is JObject expectedObject) {
                JObject actualObject = actual as JObject;
                if (actualObject == null) {
                    lost.Add(path);
                    return;
                }
                foreach (JProperty property in expectedObject.Properties()) {
                    CollectLost(property.Value, actualObject[property.Name], path + "." + property.Name, lost);
                }
            }else if (expected is JArray expectedArray) {
                JArray actualArray = actual as JArray;
                if (actualArray == null || actualArray.Count != expectedArray.Count) {
                    lost.Add(path);
                    return;
                }
                for (int i = 0; i < expectedArray.Count; i++) {
                    CollectLost(expectedArray[i], actualArray[i], path + "[" + i + "]", lost);
                }
            }else if (!JToken.DeepEquals(expected, actual)) {
                lost.Add(path + " (" + expected + " -> " + actual + ")");
            }
        }
    }
}
