using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using NUnit.Framework;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Core {
    // Nights 2–5 draw riders from the night's counts and monster pool (§2.19).
    public class ManifestGeneratorTests {
        static readonly string[] Looks = {
            "look01", "look02", "look03", "look04", "look05", "look06",
            "look07", "look08", "look09", "look10", "look11", "look12",
        };

        static RouteDefinition Route() {
            RouteDefinition route = ScriptableObject.CreateInstance<RouteDefinition>();
            route.terminusStopId = "lodge";
            route.stops = new[] {
                Stop("farm_gate", 350f), Stop("gas_station", 800f), Stop("campground", 1250f),
                Stop("church", 1900f), Stop("clinic", 2400f), Stop("trailhead", 2750f), Stop("lodge", 2960f),
            };
            return route;
        }

        static RouteStop Stop(string id, float distance) {
            return new RouteStop { stopId = id, distance = distance };
        }

        static NightDefinition Night2() {
            NightDefinition night = ScriptableObject.CreateInstance<NightDefinition>();
            night.nightIndex = 2;
            night.endStopId = "lodge";
            night.riderCountMin = 8;
            night.riderCountMax = 10;
            night.decoyCount = 1;
            night.monsterCountMin = 2;
            night.monsterCountMax = 2;
            night.requiredMonsters = new[] { "starer", "whisperer" };
            night.monsterBoardFirstStop = "farm_gate";
            night.monsterBoardLastStop = "campground";
            return night;
        }

        static NightDefinition Night3() {
            NightDefinition night = ScriptableObject.CreateInstance<NightDefinition>();
            night.nightIndex = 3;
            night.endStopId = "lodge";
            night.riderCountMin = 9;
            night.riderCountMax = 11;
            night.decoyCount = 2;
            night.monsterCountMin = 2;
            night.monsterCountMax = 2;
            night.requiredMonsters = new[] { "gazer", "guest" };
            night.monsterBoardFirstStop = "farm_gate";
            night.monsterBoardLastStop = "church";
            return night;
        }

        [Test]
        public void SameSeed_ProducesTheSameManifest() {
            RouteDefinition route = Route();
            NightDefinition night = Night2();
            Manifest a = ManifestGenerator.Generate(night, route, Looks, new System.Random(42), false);
            Manifest b = ManifestGenerator.Generate(night, route, Looks, new System.Random(42), false);
            Assert.AreEqual(a.Riders.Count, b.Riders.Count);
            for (int i = 0; i < a.Riders.Count; i++) {
                Assert.AreEqual(a.Riders[i].lookId, b.Riders[i].lookId);
                Assert.AreEqual(a.Riders[i].boardStopId, b.Riders[i].boardStopId);
                Assert.AreEqual(a.Riders[i].destinationStopId, b.Riders[i].destinationStopId);
                Assert.AreEqual(a.Riders[i].monsterId, b.Riders[i].monsterId);
                Assert.AreEqual(a.Riders[i].decoy, b.Riders[i].decoy);
            }
        }

        [Test]
        public void Night2_DrawsHumansAndTheRequiredMonsters() {
            RouteDefinition route = Route();
            NightDefinition night = Night2();
            for (int seed = 1; seed <= 40; seed++) {
                Manifest manifest = ManifestGenerator.Generate(night, route, Looks, new System.Random(seed), false);
                int humans = 0;
                int decoys = 0;
                int starers = 0;
                int whisperers = 0;
                for (int i = 0; i < manifest.Riders.Count; i++) {
                    RiderSpec rider = manifest.Riders[i];
                    if (rider.IsMonster) {
                        if (rider.monsterId == "starer") {
                            starers++;
                        } else if (rider.monsterId == "whisperer") {
                            whisperers++;
                        } else {
                            Assert.Fail("unexpected monster " + rider.monsterId);
                        }
                        Assert.AreEqual("", rider.destinationStopId);
                        Assert.IsTrue(BoardInWindow(route, rider.boardStopId, "farm_gate", "campground"));
                    } else {
                        humans++;
                        if (rider.decoy != DecoyKind.None) {
                            decoys++;
                        }
                        Assert.IsTrue(StopIndex(route, rider.destinationStopId) > StopIndex(route, rider.boardStopId));
                    }
                }
                Assert.GreaterOrEqual(humans, 8);
                Assert.LessOrEqual(humans, 10);
                Assert.AreEqual(1, starers);
                Assert.AreEqual(1, whisperers);
                Assert.AreEqual(1, decoys);
                AssertAboard(route, manifest, ManifestGenerator.MaxAboard);
                AssertNoSharedLookAboard(route, manifest);
            }
        }

        [Test]
        public void Night3_DrawsTheGazerAndTheGuest() {
            RouteDefinition route = Route();
            NightDefinition night = Night3();
            for (int seed = 1; seed <= 40; seed++) {
                Manifest manifest = ManifestGenerator.Generate(night, route, Looks, new System.Random(seed), false);
                int gazers = 0;
                int guests = 0;
                for (int i = 0; i < manifest.Riders.Count; i++) {
                    RiderSpec rider = manifest.Riders[i];
                    if (!rider.IsMonster) {
                        continue;
                    }
                    if (rider.monsterId == "gazer") {
                        gazers++;
                    } else if (rider.monsterId == "guest") {
                        guests++;
                    } else {
                        Assert.Fail("unexpected monster " + rider.monsterId);
                    }
                    Assert.IsTrue(BoardInWindow(route, rider.boardStopId, "farm_gate", "church"));
                    Assert.AreEqual("", rider.destinationStopId);
                }
                Assert.AreEqual(1, gazers);
                Assert.AreEqual(1, guests);
                AssertAboard(route, manifest, ManifestGenerator.MaxAboard);
            }
        }

        [Test]
        public void NoMonsters_SkipsThePool() {
            Manifest manifest = ManifestGenerator.Generate(Night2(), Route(), Looks, new System.Random(7), true);
            Assert.AreEqual(0, manifest.MonsterCount);
            Assert.GreaterOrEqual(manifest.Riders.Count, 8);
        }

        static int StopIndex(RouteDefinition route, string stopId) {
            return route.IndexOfStop(stopId);
        }

        static bool BoardInWindow(RouteDefinition route, string board, string first, string last) {
            int index = StopIndex(route, board);
            return index >= StopIndex(route, first) && index <= StopIndex(route, last);
        }

        static void AssertAboard(RouteDefinition route, Manifest manifest, int maxAboard) {
            int aboard = 0;
            int peak = 0;
            for (int s = 0; s < route.stops.Length; s++) {
                string stopId = route.stops[s].stopId;
                for (int i = 0; i < manifest.Riders.Count; i++) {
                    RiderSpec rider = manifest.Riders[i];
                    if (!rider.IsMonster && rider.destinationStopId == stopId) {
                        aboard--;
                    }
                }
                for (int i = 0; i < manifest.Riders.Count; i++) {
                    if (manifest.Riders[i].boardStopId == stopId) {
                        aboard++;
                    }
                }
                if (aboard > peak) {
                    peak = aboard;
                }
            }
            Assert.LessOrEqual(peak, maxAboard, "peak aboard " + peak);
        }

        static void AssertNoSharedLookAboard(RouteDefinition route, Manifest manifest) {
            List<RiderSpec> aboard = new List<RiderSpec>();
            for (int s = 0; s < route.stops.Length; s++) {
                string stopId = route.stops[s].stopId;
                for (int i = aboard.Count - 1; i >= 0; i--) {
                    if (!aboard[i].IsMonster && aboard[i].destinationStopId == stopId) {
                        aboard.RemoveAt(i);
                    }
                }
                for (int i = 0; i < manifest.Riders.Count; i++) {
                    if (manifest.Riders[i].boardStopId != stopId) {
                        continue;
                    }
                    for (int a = 0; a < aboard.Count; a++) {
                        Assert.AreNotEqual(aboard[a].lookId, manifest.Riders[i].lookId, "two riders share " + manifest.Riders[i].lookId + " at " + stopId);
                    }
                    aboard.Add(manifest.Riders[i]);
                }
            }
        }
    }
}
