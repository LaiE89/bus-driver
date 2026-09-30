using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Save;

namespace BusDriver.Core.Rules {
    // One stop of the night as the Summary shows it (§2.21)
    public sealed class NightArrival {
        public string stopId;
        public string displayName;
        public double scheduledGameSeconds;
        // NaN when the stop was missed
        public double arrivalGameSeconds = double.NaN;
        public ArrivalRating rating;

        public StopArrival ToSave() {
            return new StopArrival { stopId = stopId, rating = rating, arrivalGameSeconds = double.IsNaN(arrivalGameSeconds) ? 0.0 : arrivalGameSeconds };
        }
    }

    // What a completed night hands to the run (§2.1, §4.4): its money, arrivals, sanity and
    // counts. The Summary is drawn from it, and RunFlow.CompleteNight applies it to RunState.
    public sealed class NightResult {
        public int nightIndex;
        public LedgerTotals totals = new LedgerTotals();
        public int walletBeforeCents;
        public float sanityEnd;
        public readonly List<NightArrival> arrivals = new List<NightArrival>();
        // This night's share of the run stats (§4.9): money by kind and the rider counts
        public RunStats stats = new RunStats();

        public int NetCents { get { return totals.NetCents; } }
        public int WalletAfterCents { get { return walletBeforeCents + totals.NetCents; } }

        // The ledger's money by kind, as RunStats counts it (refunds and losses as positive sums)
        public void FillMoneyStats() {
            stats.faresCents = totals.Cents(LedgerKind.Fare);
            stats.tipsCents = totals.Cents(LedgerKind.Tip);
            stats.refundsCents = -totals.Cents(LedgerKind.Refund);
            stats.lostCents = -totals.Cents(LedgerKind.Lost);
            stats.bountiesCents = totals.Cents(LedgerKind.Bounty);
        }

        public NightHistory ToHistory() {
            NightHistory history = new NightHistory { night = nightIndex, walletDeltaCents = NetCents, sanityEnd = sanityEnd };
            for (int i = 0; i < arrivals.Count; i++) {
                history.stops.Add(arrivals[i].ToSave());
            }
            return history;
        }

        // Adds this night's counts to the run's totals
        public void AddTo(RunStats run) {
            run.faresCents += stats.faresCents;
            run.tipsCents += stats.tipsCents;
            run.refundsCents += stats.refundsCents;
            run.lostCents += stats.lostCents;
            run.bountiesCents += stats.bountiesCents;
            run.monstersKicked += stats.monstersKicked;
            run.innocentsKicked += stats.innocentsKicked;
            run.passengersLost += stats.passengersLost;
            run.ridersDelivered += stats.ridersDelivered;
        }
    }
}
