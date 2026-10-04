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
    // Turns rider events into ledger entries (§2.7, §4.6): a fare when a rider boards (monsters
    // too, D34), a tip or missed-stop refund when they speak their farewell on the step, a refund
    // when an innocent is kicked or a rider is killed, and a bounty for a kicked monster.
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
            cabin.OnPassengerBoarded += HandleBoarded;
            cabin.OnPassengerDropOff += HandleDropOff;
            riders.OnStatusChanged += HandleStatusChanged;
            progress.OnServed += HandleServed;
            shift.Debug.Register("Economy", ledger.WriteDebug);
        }

        void HandleBoarded(Passenger passenger) {
            RiderRecord record = riders.For(passenger);
            if (record == null || paid.ContainsKey(record.RiderId)) {
                return;
            }
            int fare = balance.fareCents;
            paid[record.RiderId] = fare;
            ledger.Record(new LedgerEntry(LedgerKind.Fare, fare, record.RiderId, record.Spec.boardStopId, Now()));
        }

        // Same moment as the drop-off dialogue: tip for an Early stop, fare back if their stop was missed
        void HandleDropOff(Passenger passenger) {
            RiderRecord record = riders.For(passenger);
            if (record == null || !settled.Add(record.RiderId)) {
                return;
            }
            if (string.IsNullOrEmpty(record.ExitStopId) && cabin != null && cabin.CurrentStop != null) {
                record.ExitStopId = cabin.CurrentStop.StopId;
            }
            if (record.Retargeted) {
                RefundFare(record, LedgerKind.Refund);
                return;
            }
            StopRecord stop = progress.Find(record.ExitStopId);
            if (stop != null && stop.State == StopState.Pending) {
                awaitingRating.Add(record);
                return;
            }
            PayTip(record, stop);
        }

        void HandleStatusChanged(RiderRecord record) {
            switch (record.Status) {
                case RiderStatus.Kicked:
                    if (record.IsMonster) {
                        // The fare is kept (§2.7)
                        ledger.Record(new LedgerEntry(LedgerKind.Bounty, BountyFor(record.MonsterId), record.RiderId, record.ExitStopId, Now()));
                    }else {
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
