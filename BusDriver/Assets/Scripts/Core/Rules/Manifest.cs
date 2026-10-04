using System.Collections.Generic;
using BusDriver.Core.Data;

namespace BusDriver.Core.Rules {
    // A night's riders, in spawn order (§2.19). Built from the night's scripted list for now; the
    // generator (ManifestGenerator, T-M7-01) produces the same shape for nights 2–5.
    public sealed class Manifest {
        public readonly int NightIndex;
        public readonly List<RiderSpec> Riders = new List<RiderSpec>();

        public Manifest(int nightIndex) {
            NightIndex = nightIndex;
        }

        // The night's scripted riders, or the fallback night's when it has none (until T-M7-01,
        // nights 2–5 reuse night 1's list). Monsters keep no drop-off (§2.9); they stay aboard
        // until kicked. noMonsters drops every monster rider (the debug flag, T-M3-03).
        public static Manifest FromScripted(NightDefinition night, NightDefinition fallback, bool noMonsters) {
            Manifest manifest = new Manifest(night != null ? night.nightIndex : 0);
            NightDefinition source = night != null && night.IsScripted ? night : fallback;
            if (source == null || !source.IsScripted) {
                return manifest;
            }
            for (int i = 0; i < source.scripted.Length; i++) {
                RiderSpec spec = source.scripted[i];
                if (spec == null || (noMonsters && spec.IsMonster)) {
                    continue;
                }
                RiderSpec rider = spec.Clone();
                if (rider.IsMonster) {
                    rider.destinationStopId = "";
                }
                manifest.Riders.Add(rider);
            }
            return manifest;
        }

        // One waiting human at every stop before the night's end, so a long night never has an
        // empty kerb. Looks already used by the scripted list are skipped (D34).
        public void EnsureBoardingCoverage(RouteDefinition route, string endStopId, IReadOnlyList<string> lookIds) {
            if (route == null || route.stops == null || route.stops.Length == 0) {
                return;
            }
            float endDistance = EndDistance(route, endStopId);
            HashSet<string> boarded = new HashSet<string>();
            HashSet<string> usedLooks = new HashSet<string>();
            for (int i = 0; i < Riders.Count; i++) {
                RiderSpec rider = Riders[i];
                if (rider == null) {
                    continue;
                }
                if (!string.IsNullOrEmpty(rider.boardStopId)) {
                    boarded.Add(rider.boardStopId);
                }
                if (!string.IsNullOrEmpty(rider.lookId)) {
                    usedLooks.Add(rider.lookId);
                }
            }
            string destination = string.IsNullOrEmpty(endStopId) ? route.terminusStopId : endStopId;
            int lookCursor = 0;
            for (int i = 0; i < route.stops.Length; i++) {
                RouteStop stop = route.stops[i];
                if (stop == null || string.IsNullOrEmpty(stop.stopId) || stop.distance >= endDistance - 0.01f) {
                    continue;
                }
                if (boarded.Contains(stop.stopId)) {
                    continue;
                }
                string look = NextFreeLook(lookIds, usedLooks, ref lookCursor);
                if (look == null) {
                    break;
                }
                usedLooks.Add(look);
                boarded.Add(stop.stopId);
                Riders.Add(new RiderSpec {
                    lookId = look,
                    boardStopId = stop.stopId,
                    destinationStopId = destination,
                });
            }
        }

        // Drop anyone who boards at/after the end stop, and pull every destination back onto the
        // night so opening the doors at the end always finishes the shift (§2.1 / §2.4).
        public void ClampToNight(RouteDefinition route, string endStopId) {
            if (route == null || route.stops == null || route.stops.Length == 0) {
                return;
            }
            string endId = string.IsNullOrEmpty(endStopId) ? route.terminusStopId : endStopId;
            float endDistance = EndDistance(route, endId);
            for (int i = Riders.Count - 1; i >= 0; i--) {
                RiderSpec rider = Riders[i];
                if (rider == null) {
                    Riders.RemoveAt(i);
                    continue;
                }
                float boardDistance = StopDistance(route, rider.boardStopId);
                if (float.IsNaN(boardDistance) || boardDistance >= endDistance - 0.01f) {
                    Riders.RemoveAt(i);
                    continue;
                }
                if (rider.IsMonster) {
                    rider.destinationStopId = "";
                    continue;
                }
                float destDistance = StopDistance(route, rider.destinationStopId);
                // Past the end, unknown, or not ahead of the board stop → ride to the night's end
                if (float.IsNaN(destDistance) || destDistance > endDistance + 0.01f || destDistance <= boardDistance + 0.01f) {
                    rider.destinationStopId = endId;
                }
            }
        }

        static float StopDistance(RouteDefinition route, string stopId) {
            if (string.IsNullOrEmpty(stopId)) {
                return float.NaN;
            }
            for (int i = 0; i < route.stops.Length; i++) {
                if (route.stops[i] != null && route.stops[i].stopId == stopId) {
                    return route.stops[i].distance;
                }
            }
            return float.NaN;
        }

        static float EndDistance(RouteDefinition route, string endStopId) {
            if (!string.IsNullOrEmpty(endStopId)) {
                for (int i = 0; i < route.stops.Length; i++) {
                    if (route.stops[i] != null && route.stops[i].stopId == endStopId) {
                        return route.stops[i].distance;
                    }
                }
            }
            return float.PositiveInfinity;
        }

        static string NextFreeLook(IReadOnlyList<string> lookIds, HashSet<string> used, ref int cursor) {
            if (lookIds == null) {
                return null;
            }
            while (cursor < lookIds.Count) {
                string look = lookIds[cursor++];
                if (!string.IsNullOrEmpty(look) && !used.Contains(look)) {
                    return look;
                }
            }
            return null;
        }

        public int MonsterCount {
            get {
                int count = 0;
                for (int i = 0; i < Riders.Count; i++) {
                    if (Riders[i].IsMonster) {
                        count++;
                    }
                }
                return count;
            }
        }
    }
}
