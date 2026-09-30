using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Core {
    // Manifest.FromScripted (T-M3-03): the scripted list, the night-1 fallback, monsters riding to
    // the night's end stop, and the noMonsters flag
    public class ManifestTests {
        static NightDefinition Night(int index, string endStop, params RiderSpec[] scripted) {
            NightDefinition night = ScriptableObject.CreateInstance<NightDefinition>();
            night.nightIndex = index;
            night.endStopId = endStop;
            night.scripted = scripted;
            return night;
        }

        static RiderSpec Rider(string look, string board, string destination, string monster = "") {
            return new RiderSpec { lookId = look, boardStopId = board, destinationStopId = destination, monsterId = monster };
        }

        [Test]
        public void Scripted_CopiesTheListInOrder() {
            NightDefinition one = Night(1, "church", Rider("look01", "farm_gate", "campground"), Rider("look06", "campground", "church", "starer"));
            Manifest manifest = Manifest.FromScripted(one, one, false);
            Assert.AreEqual(1, manifest.NightIndex);
            Assert.AreEqual(2, manifest.Riders.Count);
            Assert.AreEqual("look01", manifest.Riders[0].lookId);
            Assert.AreEqual(1, manifest.MonsterCount);
            // A copy: the manifest never writes to the data asset (§4.1 rule 2)
            Assert.AreNotSame(one.scripted[0], manifest.Riders[0]);
        }

        [Test]
        public void UnscriptedNight_ReusesNightOne_WithMonstersToItsEndStop() {
            NightDefinition one = Night(1, "church", Rider("look01", "farm_gate", "campground"), Rider("look06", "campground", "church", "starer"));
            NightDefinition two = Night(2, "lodge");
            Manifest manifest = Manifest.FromScripted(two, one, false);
            Assert.AreEqual(2, manifest.NightIndex);
            Assert.AreEqual(2, manifest.Riders.Count);
            Assert.AreEqual("campground", manifest.Riders[0].destinationStopId, "people keep their stops");
            Assert.AreEqual("lodge", manifest.Riders[1].destinationStopId, "a monster rides to the night's end stop");
            Assert.AreEqual("church", one.scripted[1].destinationStopId, "night 1's data is untouched");
        }

        [Test]
        public void NoMonsters_DropsMonsterRiders() {
            NightDefinition one = Night(1, "church", Rider("look01", "farm_gate", "campground"), Rider("look06", "campground", "church", "starer"));
            Manifest manifest = Manifest.FromScripted(one, one, true);
            Assert.AreEqual(1, manifest.Riders.Count);
            Assert.AreEqual(0, manifest.MonsterCount);
        }
    }
}
