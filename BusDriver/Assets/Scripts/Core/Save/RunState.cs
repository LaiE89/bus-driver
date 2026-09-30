using System.Collections.Generic;
using BusDriver.Core.Data;

namespace BusDriver.Core.Save {
    // run.json data, v1 (§4.9): everything a run keeps between nights. Wiped on death (D3).
    public sealed class RunState {
        public const int SlotCount = 3;
        public const float StartingSanity = 100f;

        public int seed;
        public int nightIndex = 1;
        public bool nightInProgress;
        public int walletCents;
        public float sanity = StartingSanity;
        // One consumable item id per slot, null when empty (§2.18)
        public string[] slots = new string[SlotCount];
        public List<string> owned = new List<string>();
        public RunStats stats = new RunStats();
        public List<NightHistory> nights = new List<NightHistory>();

        public static RunState NewRun(int seed) {
            return new RunState { seed = seed };
        }
    }

    public sealed class RunStats {
        public int faresCents;
        public int tipsCents;
        public int refundsCents;
        public int lostCents;
        public int bountiesCents;
        public int monstersKicked;
        public int innocentsKicked;
        public int passengersLost;
        public int ridersDelivered;
    }

    public sealed class NightHistory {
        public int night;
        public int walletDeltaCents;
        public float sanityEnd;
        public List<StopArrival> stops = new List<StopArrival>();
    }

    public sealed class StopArrival {
        public string stopId;
        public ArrivalRating rating;
        public double arrivalGameSeconds;
    }
}
