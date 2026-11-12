using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.Shift;

namespace BusDriver.Gameplay.Economy {
    // Turns rider events into ledger entries (§2.7, §4.6): the fare and any tip are paid when a
    // human steps off at the stop they asked for. Nothing is charged for stepping on. A missed
    // stop pays nothing, because the fare was never taken. A kick or a death before that also
    // has nothing to refund. A kicked monster still pays its bounty.
    // Subscriptions only; it holds no state of its own beyond what each rider paid.
    public sealed class EconomyRules {
        readonly Dictionary<string, int> paid = new Dictionary<string, int>();
        // Riders who said farewell before their stop was marked Served (doors still opening); tip
        // is decided once the stop is served
        readonly List<RiderRecord> awaitingRating = new List<RiderRecord>();
        readonly List<RiderRecord> ready = new List<RiderRecord>();
        readonly HashSet<string> settled = new HashSet<string>();
        ShiftLedger ledger;
        BalanceConfig balance;
        PassengerRegistry riders;
        RouteProgress progress;
        ShiftClockDriver clock;
        BusCabin cabin;

        // A monster's bounty by its id. MonsterSystem sets it from MonsterDefinition.bountyCents
        // (T-M4-03); until then every monster pays BalanceConfig.defaultBountyCents (D90).
        public Func<string, int> BountyFor;

        // ShiftContext, step 6 of the Init order (§4.5), after the ledger exists
        public void Init(ShiftServices shift) {
            ledger = shift.Ledger;
            balance = shift.Balance;
            riders = shift.Riders;
            progress = shift.Progress;
            clock = shift.Clock;
            cabin = shift.Cabin;
            BountyFor = monsterId => balance.defaultBountyCents;
            cabin.OnPassengerDropOff += HandleDropOff;
            riders.OnStatusChanged += HandleStatusChanged;
            progress.OnServed += HandleServed;
            shift.Debug.Register("Economy", ledger.WriteDebug);
        }

        // Fare and tip land with the farewell. A rider carried past their stop, or a monster, pays nothing.
        void HandleDropOff(Passenger passenger) {
            RiderRecord record = riders.For(passenger);
            if (record == null || !settled.Add(record.RiderId)) {
                return;
            }
            if (string.IsNullOrEmpty(record.ExitStopId) && cabin != null && cabin.CurrentStop != null) {
                record.ExitStopId = cabin.CurrentStop.StopId;
            }
            if (record.Retargeted || record.IsMonster) {
                return;
            }
            PayFare(record);
            StopRecord stop = progress.Find(record.ExitStopId);
            if (stop != null && stop.State == StopState.Pending) {
                awaitingRating.Add(record);
                return;
            }
            PayTip(record, stop);
        }

        void PayFare(RiderRecord record) {
            if (paid.ContainsKey(record.RiderId)) {
                return;
            }
            int fare = balance.fareCents;
            paid[record.RiderId] = fare;
            string stopId = string.IsNullOrEmpty(record.ExitStopId) ? record.Spec.boardStopId : record.ExitStopId;
            ledger.Record(new LedgerEntry(LedgerKind.Fare, fare, record.RiderId, stopId, Now()));
        }

        void HandleStatusChanged(RiderRecord record) {
            switch (record.Status) {
                case RiderStatus.Kicked:
                    if (record.IsMonster) {
                        ledger.Record(new LedgerEntry(LedgerKind.Bounty, BountyFor(record.MonsterId), record.RiderId, record.ExitStopId, Now()));
                    }else {
                        // They leave before paying, so there is no fare to hand back
                        RefundFare(record, LedgerKind.Refund);
                    }
                    break;
                case RiderStatus.Died:
                    RefundFare(record, LedgerKind.Lost);
                    break;
            }
        }

        void HandleServed(StopRecord stop) {
            ready.Clear();
            for (int i = awaitingRating.Count - 1; i >= 0; i--) {
                if (awaitingRating[i].ExitStopId == stop.StopId) {
                    ready.Add(awaitingRating[i]);
                    awaitingRating.RemoveAt(i);
                }
            }
            for (int i = ready.Count - 1; i >= 0; i--) {
                PayTip(ready[i], stop);
            }
        }

        void PayTip(RiderRecord record, StopRecord stop) {
            ArrivalRating rating = stop != null ? stop.Rating : ArrivalRating.None;
            if (!EconomyMath.EarnsTip(record.IsMonster, record.Retargeted, record.Spec.destinationStopId, record.ExitStopId, rating)) {
                return;
            }
            int fare = paid.TryGetValue(record.RiderId, out int amount) ? amount : balance.fareCents;
            ledger.Record(new LedgerEntry(LedgerKind.Tip, EconomyMath.Tip(fare, balance.tipPercent), record.RiderId, record.ExitStopId, Now()));
        }

        // D5: what the rider paid comes back off; a rider who never paid (placed aboard by a test)
        // costs nothing
        void RefundFare(RiderRecord record, LedgerKind kind) {
            if (!paid.TryGetValue(record.RiderId, out int fare)) {
                return;
            }
            paid.Remove(record.RiderId);
            ledger.Record(new LedgerEntry(kind, -fare, record.RiderId, record.ExitStopId, Now()));
        }

        double Now() {
            return clock != null ? clock.NowGameSeconds : 0.0;
        }
    }
}
