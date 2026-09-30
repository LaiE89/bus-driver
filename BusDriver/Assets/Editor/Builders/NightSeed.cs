using System.Collections.Generic;
using BusDriver.Core.Data;
using UnityEditor;

namespace BusDriver.Editor.Builders {
    // Seed data for Data/Nights/Night1..Night5 (§2.19, T-M3-03). Night 1 is the scripted short shift
    // to the church (D43); nights 2–5 carry the generator's parameters (T-M7-01).
    public static class NightSeed {
        public const string Folder = "Nights";
        public const int Count = 5;
        public const string Starer = "starer";
        public const string Whisperer = "whisperer";
        public const string Mimic = "mimic";
        public const string WeepingAngel = "weeping_angel";

        public static string RelativePath(int nightIndex) {
            return Folder + "/Night" + nightIndex + ".asset";
        }

        public static IEnumerable<Seed> Seeds() {
            for (int n = 1; n <= Count; n++) {
                int night = n;
                yield return Seed.Of<NightDefinition>(RelativePath(night), asset => Fill(asset, night));
            }
        }

        static RiderSpec Rider(string board, string look, string destination, DecoyKind decoy = DecoyKind.None, string monster = "") {
            return new RiderSpec { boardStopId = board, lookId = look, destinationStopId = destination, decoy = decoy, monsterId = monster };
        }

        public static void Fill(NightDefinition night, int index) {
            night.nightIndex = index;
            night.endStopId = index == 1 ? "church" : "lodge";
            night.hintsEnabled = index == 1;
            night.scripted = new RiderSpec[0];
            night.requiredMonsters = new string[0];
            night.monsterPool = new string[0];
            night.minDistinctTypes = 0;
            night.maxPerType = 0;
            night.mimicBoardFirstStop = "";
            night.mimicBoardLastStop = "";
            string[] all = { Starer, Whisperer, Mimic, WeepingAngel };
            switch (index) {
                case 1:
                    night.threatRateMultiplier = 1f;
                    // §2.19's table, in order; the Starer boards at gas_station, well before the cliff (D106)
                    night.scripted = new[] {
                        Rider("farm_gate", "look01", "campground"),
                        Rider("farm_gate", "look02", "church"),
                        Rider("gas_station", "look03", "church", DecoyKind.NodOff),
                        Rider("gas_station", "look04", "campground"),
                        Rider("campground", "look05", "church"),
                        Rider("gas_station", "look06", "church", DecoyKind.None, Starer),
                    };
                    night.riderCountMin = night.riderCountMax = 5;
                    night.decoyCount = 1;
                    night.monsterCountMin = night.monsterCountMax = 1;
                    break;
                case 2:
                    night.threatRateMultiplier = 1f;
                    night.riderCountMin = 8;
                    night.riderCountMax = 10;
                    night.decoyCount = 1;
                    night.monsterCountMin = night.monsterCountMax = 2;
                    night.requiredMonsters = new[] { Starer, Whisperer };
                    night.monsterBoardFirstStop = "farm_gate";
                    night.monsterBoardLastStop = "campground";
                    break;
                case 3:
                    night.threatRateMultiplier = 1.1f;
                    night.riderCountMin = 9;
                    night.riderCountMax = 11;
                    night.decoyCount = 2;
                    night.monsterCountMin = night.monsterCountMax = 2;
                    night.requiredMonsters = new[] { Mimic };
                    night.monsterPool = new[] { Starer, Whisperer, WeepingAngel };
                    night.monsterBoardFirstStop = "farm_gate";
                    night.monsterBoardLastStop = "church";
                    night.mimicBoardFirstStop = "gas_station";
                    night.mimicBoardLastStop = "campground";
                    break;
                case 4:
                    night.threatRateMultiplier = 1.2f;
                    night.riderCountMin = 10;
                    night.riderCountMax = 12;
                    night.decoyCount = 2;
                    night.monsterCountMin = 2;
                    night.monsterCountMax = 3;
                    night.monsterPool = all;
                    night.maxPerType = 2;
                    night.monsterBoardFirstStop = "farm_gate";
                    night.monsterBoardLastStop = "church";
                    break;
                default:
                    night.threatRateMultiplier = 1.35f;
                    night.riderCountMin = 11;
                    night.riderCountMax = 13;
                    night.decoyCount = 3;
                    night.monsterCountMin = 3;
                    night.monsterCountMax = 4;
                    night.monsterPool = all;
                    night.minDistinctTypes = 3;
                    night.monsterBoardFirstStop = "farm_gate";
                    night.monsterBoardLastStop = "clinic";
                    break;
            }
        }

        // Fills the config's nights[] (index 0 = night 1) where it is empty; never replaces one
        public static void Adopt(string root, GameRootConfig config) {
            if (config.nights == null || config.nights.Length != Count) {
                NightDefinition[] nights = new NightDefinition[Count];
                if (config.nights != null) {
                    for (int i = 0; i < config.nights.Length && i < Count; i++) {
                        nights[i] = config.nights[i];
                    }
                }
                config.nights = nights;
            }
            for (int i = 0; i < Count; i++) {
                if (config.nights[i] == null) {
                    config.nights[i] = AssetDatabase.LoadAssetAtPath<NightDefinition>(root + "/" + RelativePath(i + 1));
                }
            }
        }
    }
}
