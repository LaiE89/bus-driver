using System;
using System.Collections.Generic;
using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // Nights 2–5 (§2.19): riders and monsters drawn from the night's counts and monster pool.
    // Deterministic for a given (seed, night) when the caller passes the manifest RNG stream.
    // Night 1 stays on its scripted list (Manifest.FromScripted).
    public static class ManifestGenerator {
        public const int MaxAboard = 10;
        public const int PlacementAttempts = 200;

        static readonly DecoyKind[] DecoyKinds = {
            DecoyKind.NodOff, DecoyKind.PhoneGlow, DecoyKind.Mutter, DecoyKind.FacingBackwards, DecoyKind.HoodUp,
        };

        sealed class Draft {
            public RiderSpec Spec;
            public int BoardIndex;
            public int DestIndex;
            public int Sequence;
        }

        public static Manifest Generate(NightDefinition night, RouteDefinition route, IReadOnlyList<string> lookIds, Random rng, bool noMonsters, int maxAboard = MaxAboard) {
            Manifest manifest = new Manifest(night != null ? night.nightIndex : 0);
            if (night == null || route == null || route.stops == null || rng == null) {
                return manifest;
            }
            if (maxAboard < 1) {
                maxAboard = MaxAboard;
            }
            List<string> stops = StopsThroughEnd(route, night.endStopId);
            if (stops.Count < 2) {
                return manifest;
            }

            int humanCount = Inclusive(rng, night.riderCountMin, night.riderCountMax);
            List<string> monsters = noMonsters ? new List<string>() : ChooseMonsters(night, rng);
            string[] looks = CleanLooks(lookIds);

            List<Draft> best = null;
            int bestPeak = int.MaxValue;
            for (int attempt = 0; attempt < PlacementAttempts; attempt++) {
                List<Draft> drafted = Place(night, stops, humanCount, monsters, rng);
                int peak;
                if (Simulate(drafted, stops.Count, maxAboard, out peak)) {
                    Publish(manifest, drafted, stops.Count, looks, night.decoyCount, rng);
                    return manifest;
                }
                if (peak < bestPeak) {
                    bestPeak = peak;
                    best = drafted;
                }
            }
            if (best != null) {
                Publish(manifest, best, stops.Count, looks, night.decoyCount, rng);
            }
            return manifest;
        }

        // Required types always board. The rest are drawn from monsterPool until the night's count,
        // without passing maxPerType, then topped up so minDistinctTypes is met when the pool allows it.
        static List<string> ChooseMonsters(NightDefinition night, Random rng) {
            List<string> chosen = new List<string>();
            Dictionary<string, int> counts = new Dictionary<string, int>();
            if (night.requiredMonsters != null) {
                for (int i = 0; i < night.requiredMonsters.Length; i++) {
                    AddMonster(chosen, counts, night.requiredMonsters[i]);
                }
            }

            int min = night.monsterCountMin;
            int max = night.monsterCountMax;
            if (max < min) {
                max = min;
            }
            if (min < chosen.Count) {
                min = chosen.Count;
            }
            if (max < chosen.Count) {
                max = chosen.Count;
            }
            int target = min >= max ? min : rng.Next(min, max + 1);

            string[] pool = night.monsterPool ?? new string[0];
            int guard = 0;
            while (chosen.Count < target && pool.Length > 0 && guard++ < 64) {
                int options = 0;
                for (int i = 0; i < pool.Length; i++) {
                    if (CanTake(night, counts, pool[i])) {
                        options++;
                    }
                }
                if (options == 0) {
                    break;
                }
                int pick = rng.Next(options);
                for (int i = 0; i < pool.Length; i++) {
                    if (!CanTake(night, counts, pool[i])) {
                        continue;
                    }
                    if (pick == 0) {
                        AddMonster(chosen, counts, pool[i]);
                        break;
                    }
                    pick--;
                }
            }

            if (night.minDistinctTypes > 1) {
                guard = 0;
                while (counts.Count < night.minDistinctTypes && guard++ < 32) {
                    int missing = 0;
                    for (int i = 0; i < pool.Length; i++) {
                        if (!string.IsNullOrEmpty(pool[i]) && !counts.ContainsKey(pool[i])) {
                            missing++;
                        }
                    }
                    if (missing == 0) {
                        break;
                    }
                    int pick = rng.Next(missing);
                    string add = null;
                    for (int i = 0; i < pool.Length; i++) {
                        if (string.IsNullOrEmpty(pool[i]) || counts.ContainsKey(pool[i])) {
                            continue;
                        }
                        if (pick == 0) {
                            add = pool[i];
                            break;
                        }
                        pick--;
                    }
                    if (add == null) {
                        break;
                    }
                    if (chosen.Count < max) {
                        AddMonster(chosen, counts, add);
                        continue;
                    }
                    int duplicate = -1;
                    for (int i = 0; i < chosen.Count; i++) {
                        int have;
                        if (counts.TryGetValue(chosen[i], out have) && have > 1) {
                            duplicate = i;
                            break;
                        }
                    }
                    if (duplicate < 0) {
                        break;
                    }
                    string old = chosen[duplicate];
                    counts[old]--;
                    if (counts[old] <= 0) {
                        counts.Remove(old);
                    }
                    chosen[duplicate] = add;
                    int added;
                    counts[add] = counts.TryGetValue(add, out added) ? added + 1 : 1;
                }
            }
            return chosen;
        }

        static bool CanTake(NightDefinition night, Dictionary<string, int> counts, string id) {
            if (string.IsNullOrEmpty(id)) {
                return false;
            }
            if (night.maxPerType <= 0) {
                return true;
            }
            int have;
            return !counts.TryGetValue(id, out have) || have < night.maxPerType;
        }

        static void AddMonster(List<string> chosen, Dictionary<string, int> counts, string id) {
            if (string.IsNullOrEmpty(id)) {
                return;
            }
            chosen.Add(id);
            int have;
            counts[id] = counts.TryGetValue(id, out have) ? have + 1 : 1;
        }

        static List<Draft> Place(NightDefinition night, List<string> stops, int humanCount, List<string> monsters, Random rng) {
            List<Draft> drafted = new List<Draft>();
            int endIndex = stops.Count - 1;
            int sequence = 0;
            for (int i = 0; i < humanCount; i++) {
                int board = rng.Next(endIndex);
                int dest = board + 1 + rng.Next(endIndex - board);
                drafted.Add(new Draft {
                    Spec = new RiderSpec { boardStopId = stops[board], destinationStopId = stops[dest] },
                    BoardIndex = board,
                    DestIndex = dest,
                    Sequence = sequence++,
                });
            }
            for (int i = 0; i < monsters.Count; i++) {
                int board = PickMonsterStop(night, stops, rng);
                drafted.Add(new Draft {
                    Spec = new RiderSpec { boardStopId = stops[board], monsterId = monsters[i] },
                    BoardIndex = board,
                    DestIndex = int.MaxValue,
                    Sequence = sequence++,
                });
            }
            return drafted;
        }

        static int PickMonsterStop(NightDefinition night, List<string> stops, Random rng) {
            int endIndex = stops.Count - 1;
            string first = night.monsterBoardFirstStop;
            string last = night.monsterBoardLastStop;
            int from = IndexOf(stops, first);
            int to = IndexOf(stops, last);
            if (from < 0) {
                from = 0;
            }
            if (to < 0) {
                to = endIndex - 1;
            }
            if (to < from) {
                int swap = from;
                from = to;
                to = swap;
            }
            if (to >= endIndex) {
                to = endIndex - 1;
            }
            if (from > to) {
                from = to;
            }
            if (from < 0) {
                from = 0;
            }
            return from >= to ? from : rng.Next(from, to + 1);
        }

        // Alight, then board. Fails when more than maxAboard are on the bus.
        static bool Simulate(List<Draft> riders, int stopCount, int maxAboard, out int peak) {
            List<Draft> aboard = new List<Draft>();
            peak = 0;
            for (int stop = 0; stop < stopCount; stop++) {
                for (int i = aboard.Count - 1; i >= 0; i--) {
                    if (aboard[i].DestIndex == stop) {
                        aboard.RemoveAt(i);
                    }
                }
                for (int i = 0; i < riders.Count; i++) {
                    if (riders[i].BoardIndex == stop) {
                        aboard.Add(riders[i]);
                    }
                }
                if (aboard.Count > peak) {
                    peak = aboard.Count;
                }
                if (aboard.Count > maxAboard) {
                    return false;
                }
            }
            return true;
        }

        static void Publish(Manifest manifest, List<Draft> drafted, int stopCount, string[] looks, int decoyCount, Random rng) {
            drafted.Sort((a, b) => {
                int byStop = a.BoardIndex.CompareTo(b.BoardIndex);
                return byStop != 0 ? byStop : a.Sequence.CompareTo(b.Sequence);
            });
            AssignLooks(drafted, stopCount, looks, rng);
            AssignDecoys(drafted, decoyCount, rng);
            for (int i = 0; i < drafted.Count; i++) {
                manifest.Riders.Add(drafted[i].Spec);
            }
        }

        static void AssignLooks(List<Draft> riders, int stopCount, string[] looks, Random rng) {
            if (looks.Length == 0) {
                return;
            }
            List<Draft> aboard = new List<Draft>();
            HashSet<string> used = new HashSet<string>();
            for (int stop = 0; stop < stopCount; stop++) {
                for (int i = aboard.Count - 1; i >= 0; i--) {
                    if (aboard[i].DestIndex == stop) {
                        used.Remove(aboard[i].Spec.lookId);
                        aboard.RemoveAt(i);
                    }
                }
                for (int i = 0; i < riders.Count; i++) {
                    if (riders[i].BoardIndex != stop) {
                        continue;
                    }
                    riders[i].Spec.lookId = PickLook(looks, used, rng);
                    used.Add(riders[i].Spec.lookId);
                    aboard.Add(riders[i]);
                }
            }
        }

        static string PickLook(string[] looks, HashSet<string> used, Random rng) {
            int free = 0;
            for (int i = 0; i < looks.Length; i++) {
                if (!used.Contains(looks[i])) {
                    free++;
                }
            }
            if (free == 0) {
                return looks[rng.Next(looks.Length)];
            }
            int pick = rng.Next(free);
            for (int i = 0; i < looks.Length; i++) {
                if (used.Contains(looks[i])) {
                    continue;
                }
                if (pick == 0) {
                    return looks[i];
                }
                pick--;
            }
            return looks[0];
        }

        static void AssignDecoys(List<Draft> riders, int decoyCount, Random rng) {
            if (decoyCount <= 0) {
                return;
            }
            List<Draft> humans = new List<Draft>();
            for (int i = 0; i < riders.Count; i++) {
                if (!riders[i].Spec.IsMonster) {
                    humans.Add(riders[i]);
                }
            }
            int count = decoyCount < humans.Count ? decoyCount : humans.Count;
            for (int i = 0; i < count; i++) {
                int swap = i + rng.Next(humans.Count - i);
                Draft picked = humans[swap];
                humans[swap] = humans[i];
                humans[i] = picked;
                picked.Spec.decoy = DecoyKinds[rng.Next(DecoyKinds.Length)];
            }
        }

        static List<string> StopsThroughEnd(RouteDefinition route, string endStopId) {
            List<string> stops = new List<string>();
            string end = string.IsNullOrEmpty(endStopId) ? route.terminusStopId : endStopId;
            for (int i = 0; i < route.stops.Length; i++) {
                RouteStop stop = route.stops[i];
                if (stop == null || string.IsNullOrEmpty(stop.stopId)) {
                    continue;
                }
                if (!string.IsNullOrEmpty(route.terminusStopId) && stop.stopId == route.terminusStopId && stop.stopId != end) {
                    continue;
                }
                stops.Add(stop.stopId);
                if (!string.IsNullOrEmpty(end) && stop.stopId == end) {
                    break;
                }
            }
            return stops;
        }

        static string[] CleanLooks(IReadOnlyList<string> lookIds) {
            if (lookIds == null || lookIds.Count == 0) {
                return new string[0];
            }
            List<string> looks = new List<string>();
            for (int i = 0; i < lookIds.Count; i++) {
                if (!string.IsNullOrEmpty(lookIds[i]) && !looks.Contains(lookIds[i])) {
                    looks.Add(lookIds[i]);
                }
            }
            return looks.ToArray();
        }

        static int IndexOf(List<string> stops, string stopId) {
            if (string.IsNullOrEmpty(stopId)) {
                return -1;
            }
            for (int i = 0; i < stops.Count; i++) {
                if (stops[i] == stopId) {
                    return i;
                }
            }
            return -1;
        }

        static int Inclusive(Random rng, int min, int max) {
            if (max < min) {
                int swap = min;
                min = max;
                max = swap;
            }
            if (min < 0) {
                min = 0;
            }
            if (max < min) {
                max = min;
            }
            return min >= max ? min : rng.Next(min, max + 1);
        }
    }
}
