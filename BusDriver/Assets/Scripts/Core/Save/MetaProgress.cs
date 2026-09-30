using System.Collections.Generic;
using BusDriver.Core.Data;

namespace BusDriver.Core.Save {
    // meta.json data, v1 (§4.9): the only progress that outlives a run (D6)
    public sealed class MetaProgress {
        // Keyed by monster id: starer, whisperer, mimic, weeping_angel
        public Dictionary<string, JournalEntryState> journal = new Dictionary<string, JournalEntryState>();
        public int runsStarted;
        public int runsWon;
        public int runsLost;
        public int bestNight;
        public Dictionary<DeathCause, int> deathsByCause = new Dictionary<DeathCause, int>();
    }

    public sealed class JournalEntryState {
        public bool seen;
        public int timesKicked;
        public int timesKilledBy;
    }
}
