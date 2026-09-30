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
        // nights 2–5 reuse night 1's list). A monster always rides to the night's end stop (§2.9).
        // noMonsters drops every monster rider (the debug flag, T-M3-03).
        public static Manifest FromScripted(NightDefinition night, NightDefinition fallback, bool noMonsters) {
            Manifest manifest = new Manifest(night != null ? night.nightIndex : 0);
            NightDefinition source = night != null && night.IsScripted ? night : fallback;
            if (source == null || !source.IsScripted) {
                return manifest;
            }
            string endStop = night != null ? night.endStopId : source.endStopId;
            for (int i = 0; i < source.scripted.Length; i++) {
                RiderSpec spec = source.scripted[i];
                if (spec == null || (noMonsters && spec.IsMonster)) {
                    continue;
                }
                RiderSpec rider = spec.Clone();
                if (rider.IsMonster) {
                    rider.destinationStopId = endStop;
                }
                manifest.Riders.Add(rider);
            }
            return manifest;
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
