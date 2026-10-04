using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Core {
    // Manifest.FromScripted (T-M3-03): the scripted list, the night-1 fallback, monsters with no
    // drop-off, and the noMonsters flag
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
        public void UnscriptedNight_ReusesNightOne_WithMonstersWithoutDropOff() {
            NightDefinition one = Night(1, "church", Rider("look01", "farm_gate", "campground"), Rider("look06", "campground", "church", "starer"));
            NightDefinition two = Night(2, "lodge");
            Manifest manifest = Manifest.FromScripted(two, one, false);
            Assert.AreEqual(2, manifest.NightIndex);
            Assert.AreEqual(2, manifest.Riders.Count);
            Assert.AreEqual("campground", manifest.Riders[0].destinationStopId, "people keep their stops");
            Assert.AreEqual("", manifest.Riders[1].destinationStopId, "a monster has no drop-off");
            Assert.AreEqual("church", one.scripted[1].destinationStopId, "night 1's data is untouched");
        }

        [Test]
        public void NoMonsters_DropsMonsterRiders() {
            NightDefinition one = Night(1, "church", Rider("look01", "farm_gate", "campground"), Rider("look06", "campground", "church", "starer"));
            Manifest manifest = Manifest.FromScripted(one, one, true);
            Assert.AreEqual(1, manifest.Riders.Count);
            Assert.AreEqual(0, manifest.MonsterCount);
        }

        [Test]
        public void EnsureBoardingCoverage_FillsEveryStopBeforeTheEnd() {
            RouteDefinition route = ScriptableObject.CreateInstance<RouteDefinition>();
            route.terminusStopId = "lodge";
            route.stops = new[] {
                new RouteStop { stopId = "farm_gate", distance = 350f },
                new RouteStop { stopId = "gas_station", distance = 800f },
                new RouteStop { stopId = "campground", distance = 1250f },
                new RouteStop { stopId = "church", distance = 1900f },
                new RouteStop { stopId = "clinic", distance = 2400f },
                new RouteStop { stopId = "trailhead", distance = 2750f },
                new RouteStop { stopId = "lodge", distance = 2960f },
            };
            NightDefinition one = Night(1, "church",
                Rider("look01", "farm_gate", "campground"),
                Rider("look02", "gas_station", "church"),
                Rider("look03", "campground", "church"));
            Manifest manifest = Manifest.FromScripted(one, one, false);
            // Night 1 ends at church: every stop before it is already covered
            manifest.EnsureBoardingCoverage(route, "church", new[] { "look01", "look02", "look03", "look04" });
            Assert.AreEqual(3, manifest.Riders.Count);

            // A lodge night that reused the short list must pad church / clinic / trailhead
            NightDefinition two = Night(2, "lodge");
            Manifest longNight = Manifest.FromScripted(two, one, false);
            longNight.EnsureBoardingCoverage(route, "lodge", new[] {
                "look01", "look02", "look03", "look04", "look05", "look06", "look07",
            });
            HashSet<string> boarded = new HashSet<string>();
            for (int i = 0; i < longNight.Riders.Count; i++) {
                boarded.Add(longNight.Riders[i].boardStopId);
            }
            Assert.IsTrue(boarded.Contains("farm_gate"));
            Assert.IsTrue(boarded.Contains("gas_station"));
            Assert.IsTrue(boarded.Contains("campground"));
            Assert.IsTrue(boarded.Contains("church"));
            Assert.IsTrue(boarded.Contains("clinic"));
            Assert.IsTrue(boarded.Contains("trailhead"));
            Assert.IsFalse(boarded.Contains("lodge"), "nobody waits at the night's end stop");
        }

        [Test]
        public void ClampToNight_PullsDestinationsOntoTheNightAndDropsEndBoards() {
            RouteDefinition route = ScriptableObject.CreateInstance<RouteDefinition>();
            route.terminusStopId = "lodge";
            route.stops = new[] {
                new RouteStop { stopId = "farm_gate", distance = 350f },
                new RouteStop { stopId = "church", distance = 1900f },
                new RouteStop { stopId = "lodge", distance = 2960f },
            };
            Manifest manifest = new Manifest(1);
            manifest.Riders.Add(Rider("look01", "farm_gate", "lodge")); // past night end
            manifest.Riders.Add(Rider("look02", "farm_gate", "farm_gate")); // not ahead
            manifest.Riders.Add(Rider("look03", "church", "lodge")); // boards at the end
            manifest.Riders.Add(Rider("look06", "farm_gate", "campground", "starer")); // monster
            manifest.ClampToNight(route, "church");
            Assert.AreEqual(3, manifest.Riders.Count, "the end-stop boarder is dropped");
            Assert.AreEqual("church", manifest.Riders[0].destinationStopId);
            Assert.AreEqual("church", manifest.Riders[1].destinationStopId);
            Assert.AreEqual("", manifest.Riders[2].destinationStopId, "monsters have no drop-off");
            Assert.AreEqual("starer", manifest.Riders[2].monsterId);
        }
    }
}
