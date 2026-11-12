using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using UnityEditor;

namespace BusDriver.Editor.Builders {
    // Seed data for Data/Monsters/Starer, Whisperer, Gazer and Guest: rules,
    // observer kinds, grace, seating, the kill sequence's escape, bounty, ability numbers and
    // journal text. logicPrefab is filled by PrefabBuilder, not here (D98).
    public static class MonsterSeed {
        public const string Folder = "Monsters";

        struct Entry {
            public string Id;
            public string File;
            public Action<MonsterDefinition> Fill;
        }

        static readonly Entry[] Entries = {
            new Entry { Id = NightSeed.Starer, File = "Starer", Fill = FillStarer },
            new Entry { Id = NightSeed.Whisperer, File = "Whisperer", Fill = FillWhisperer },
            new Entry { Id = NightSeed.Gazer, File = "Gazer", Fill = FillGazer },
            new Entry { Id = NightSeed.Guest, File = "Guest", Fill = FillGuest },
        };

        public static IEnumerable<string> Ids {
            get {
                foreach (Entry entry in Entries) {
                    yield return entry.Id;
                }
            }
        }

        public static string RelativePath(string monsterId) {
            foreach (Entry entry in Entries) {
                if (entry.Id == monsterId) {
                    return Folder + "/" + entry.File + ".asset";
                }
            }
            return null;
        }

        public static IEnumerable<Seed> Seeds() {
            foreach (Entry entry in Entries) {
                Entry e = entry;
                yield return Seed.Of<MonsterDefinition>(Folder + "/" + e.File + ".asset", asset => {
                    GenericFill(asset, e.Id);
                    e.Fill(asset);
                });
            }
        }

        const ObserverKinds AllKinds = ObserverKinds.Cctv | ObserverKinds.Driver | ObserverKinds.OnFoot | ObserverKinds.Mirror;

        // The §4.8 defaults every monster shares
        static void GenericFill(MonsterDefinition monster, string id) {
            monster.id = id;
            monster.graceSeconds = 10f;
            monster.killTelegraphSeconds = 4f;
            monster.bountyCents = 500;
            monster.observerKinds = AllKinds;
            monster.seatZonePreference = SeatZone.Any;
            monster.seatZoneFallback = SeatZone.Any;
        }

        static ThreatRule Rule(ThreatCondition condition, float rate) {
            return new ThreatRule(condition, rate);
        }

        // Punishes not watching the cabin: silent one-row hops while unwatched; a hop from the
        // near-front row kills while driving (no aisle telegraph). EscapeKind.None = no KillSequence.
        static void FillStarer(MonsterDefinition m) {
            m.displayName = "The Starer";
            m.rules = new[] { Rule(ThreatCondition.Observed, 0f), Rule(ThreatCondition.Always, 10f) };
            m.seatZonePreference = SeatZone.Rear;
            m.seatZoneFallback = SeatZone.Mid;
            m.escape = new MonsterEscape(EscapeKind.None, 0f, 60f);
            m.ability = new StarerAdvanceConfig {
                advanceIntervalSeconds = 3f,
                killFromRow = StarerRules.DefaultKillFromRow,
            };
            m.journal = new MonsterJournal {
                sightingText = "A rider at the back who never takes their eyes off you.",
                tellsText = new[] {
                    "Its head turns to whichever camera is watching it.",
                    "The bolder it gets, the stiller it sits and the wider its eyes.",
                    "It vanishes into a closer seat whenever nobody is looking.",
                },
                hint = "It only moves when you aren't looking.",
            };
        }

        // §2.11: punishes watching the road too long; only the CCTV counts, and it never starts a
        // kill sequence (it kills through sanity)
        static void FillWhisperer(MonsterDefinition m) {
            m.displayName = "The Whisperer";
            m.rules = new[] {
                Rule(ThreatCondition.ObservedByCctv, -3f), Rule(ThreatCondition.AttentionOnRoad, 1.2f), Rule(ThreatCondition.Always, 0.4f),
            };
            m.observerKinds = ObserverKinds.Cctv;
            m.seatZonePreference = SeatZone.Mid;
            m.seatZoneFallback = SeatZone.Rear;
            m.escape = new MonsterEscape(EscapeKind.None, 0f, 60f);
            m.ability = new WhispererDrainConfig();
            m.journal = new MonsterJournal {
                sightingText = "A rider who leans toward the nearest passenger and never stops talking.",
                tellsText = new[] {
                    "It leans toward its neighbour.",
                    "Its mouth never stops moving.",
                    "The longer you watch the road, the louder the whispers get.",
                },
                hint = "Don't listen to it for too long.",
            };
        }

        // Looking through the cabin camera, or staring at it on foot, makes it sprint and kill.
        static void FillGazer(MonsterDefinition m) {
            m.displayName = "The Gazer";
            m.rules = new[] { Rule(ThreatCondition.Always, 0f) };
            m.observerKinds = ObserverKinds.Cctv | ObserverKinds.OnFoot;
            m.seatZonePreference = SeatZone.Mid;
            m.seatZoneFallback = SeatZone.Rear;
            m.escape = new MonsterEscape(EscapeKind.None, 0f, 60f);
            m.ability = new GazerRushConfig();
            m.journal = new MonsterJournal {
                sightingText = "A rider who will not raise its eyes.",
                tellsText = new[] {
                    "It boards looking at the floor.",
                    "The cabin camera makes it lift its head.",
                    "On foot, meeting its eyes is the same as the camera.",
                },
                hint = "Don't watch it. Don't watch the camera while it rides.",
            };
        }

        // Talking to it, or still carrying it at the last stop, kills. A kick does not.
        static void FillGuest(MonsterDefinition m) {
            m.displayName = "The Guest";
            m.rules = new[] { Rule(ThreatCondition.Always, 0f) };
            m.observerKinds = ObserverKinds.None;
            m.escape = new MonsterEscape(EscapeKind.None, 0f, 60f);
            m.ability = new GuestStrikeConfig();
            m.journal = new MonsterJournal {
                sightingText = "A rider who never says where it is going.",
                tellsText = new[] {
                    "It talks like anyone else who just wants out of the cold.",
                    "It never says which stop is theirs.",
                },
                hint = "Don't talk to it. Don't carry it to the last stop.",
            };
        }

        // Lists every seeded monster in the config, in seed order; never removes or replaces one
        public static void Adopt(string root, GameRootConfig config) {
            List<MonsterDefinition> monsters = new List<MonsterDefinition>(config.monsters ?? new MonsterDefinition[0]);
            monsters.RemoveAll(m => m == null);
            foreach (Entry entry in Entries) {
                MonsterDefinition monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(root + "/" + Folder + "/" + entry.File + ".asset");
                if (monster != null && !monsters.Contains(monster)) {
                    monsters.Add(monster);
                }
            }
            config.monsters = monsters.ToArray();
        }

        public static MonsterDefinition Load(string root, string monsterId) {
            string path = RelativePath(monsterId);
            return path != null ? AssetDatabase.LoadAssetAtPath<MonsterDefinition>(root + "/" + path) : null;
        }
    }
}
