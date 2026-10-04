using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using UnityEditor;

namespace BusDriver.Editor.Builders {
    // Seed data for Data/Monsters/Starer, Whisperer, Mimic and WeepingAngel (§2.10–§2.12b, §4.8,
    // T-M4-03): rules, observer kinds, grace, seating, the kill sequence's escape, bounty, ability
    // numbers and journal text. logicPrefab is filled by PrefabBuilder, not here (D98).
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
            new Entry { Id = NightSeed.Mimic, File = "Mimic", Fill = FillMimic },
            new Entry { Id = NightSeed.WeepingAngel, File = "WeepingAngel", Fill = FillAngel },
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

        // §2.10, D106: punishes not watching the cabin. Watching only freezes it, so its meter never
        // falls (except the kill-sequence escape reset). Swap Observed for ObservedByCctv to count the
        // cameras alone
        static void FillStarer(MonsterDefinition m) {
            m.displayName = "The Starer";
            m.rules = new[] { Rule(ThreatCondition.Observed, 0f), Rule(ThreatCondition.Always, 10f) };
            m.seatZonePreference = SeatZone.Rear;
            m.escape = new MonsterEscape(EscapeKind.ObserveFor, 1.5f, 60f);
            m.ability = new StarerAdvanceConfig();
            m.journal = new MonsterJournal {
                sightingText = "A rider at the back who never takes their eyes off you.",
                tellsText = new[] {
                    "Its head turns to whichever camera is watching it.",
                    "The bolder it gets, the stiller it sits and the wider its eyes.",
                    "It moves forward a row whenever nobody is looking.",
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

        // §2.12, D22: punishes watching one passenger too long; on foot doesn't count
        static void FillMimic(MonsterDefinition m) {
            m.displayName = "The Mimic";
            m.rules = new[] { Rule(ThreatCondition.Observed, 2.5f), Rule(ThreatCondition.Always, -1f) };
            m.observerKinds = ObserverKinds.Cctv | ObserverKinds.Driver | ObserverKinds.Mirror;
            m.escape = new MonsterEscape(EscapeKind.UnobservedFor, 2f, 60f);
            m.ability = new MimicCopyConfig();
            m.journal = new MonsterJournal {
                sightingText = "Someone on the bus who looks exactly like someone else on the bus.",
                tellsText = new[] {
                    "Two riders with the same face.",
                    "It blinks out of sight for an instant, now and then.",
                    "It casts no shadow.",
                    "One of the cameras never shows it.",
                },
                hint = "Count the faces. Then look away.",
            };
        }

        // Main-branch Weeping Angel: no threat kill — proximity hunt only (kill scare is presentation)
        static void FillAngel(MonsterDefinition m) {
            m.displayName = "The Weeping Angel";
            m.rules = new[] { Rule(ThreatCondition.Observed, 0f), Rule(ThreatCondition.Always, 0f) };
            m.seatZonePreference = SeatZone.Rear;
            m.seatZoneFallback = SeatZone.Mid;
            m.escape = new MonsterEscape(EscapeKind.None, 0f, 60f);
            m.ability = new AngelStalkConfig();
            m.journal = new MonsterJournal {
                sightingText = "A rider who hides their face in their hands and weeps.",
                tellsText = new[] {
                    "It never moves while anyone is watching. Not even a breath.",
                    "Unwatched, it stands and comes down the aisle.",
                },
                hint = "Watching only holds it. Throw it out.",
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
