using System;
using System.Collections.Generic;
using System.Text;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.World;
using UnityEngine;

namespace BusDriver.Gameplay.Passengers {
    // One rider of the night: their manifest spec, their Passenger while it exists, and where they
    // are in the night (§2.6). Outlives the Passenger, so the Summary can still read it.
    public sealed class RiderRecord {
        public readonly string RiderId;
        // Position in PassengerRegistry.All, fixed for the night: per-rider arrays (PlayerAttention)
        // are keyed by it
        public readonly int Index;
        public readonly RiderSpec Spec;
        public Passenger Passenger { get; internal set; }
        public RiderStatus Status { get; internal set; }
        // The stop this rider gets off at; the night's end stop once theirs is missed (§2.4)
        public string DestinationStopId { get; internal set; }
        // Carried on to the end stop because their own stop was missed: fare kept, no tip (§2.4)
        public bool Retargeted { get; internal set; }
        // Where they left the bus (delivered or kicked), or "" if they never did
        public string ExitStopId { get; internal set; } = "";

        public bool IsMonster { get { return Spec.IsMonster; } }
        public string MonsterId { get { return Spec.monsterId; } }
        public bool IsAboard { get { return Status == RiderStatus.Aboard; } }

        public RiderRecord(string riderId, int index, RiderSpec spec) {
            RiderId = riderId;
            Index = index;
            Spec = spec;
            DestinationStopId = spec.destinationStopId;
        }
    }

    // A RiderRecord for every rider of the night (§4.6): who is waiting, aboard, delivered, kicked
    // or lost. It sets the cabin's alighting rule: a non-monster rider gets off at their
    // destination (§2.4); monsters never get off by themselves (§2.9). It also applies the stop
    // consequences of §2.4: a missed stop's waiting riders walk away (Lost), riders bound for it are
    // carried on to the end stop, and the end stop delivers every non-monster rider aboard.
    public sealed class PassengerRegistry : MonoBehaviour {
        readonly List<RiderRecord> all = new List<RiderRecord>();
        readonly List<RiderRecord> aboard = new List<RiderRecord>();
        readonly Dictionary<Passenger, RiderRecord> byPassenger = new Dictionary<Passenger, RiderRecord>();
        readonly List<Passenger> walkingAway = new List<Passenger>();
        readonly List<RiderRecord> delivering = new List<RiderRecord>();
        BusCabin cabin;
        RouteProgress progress;
        RouteSceneRoot route;

        public IReadOnlyList<RiderRecord> All { get { return all; } }
        public IReadOnlyList<RiderRecord> Aboard { get { return aboard; } }

        // Any rider's status changed (boarded, delivered, kicked, died, lost)
        public event Action<RiderRecord> OnStatusChanged;

        // ShiftContext, step 5 of the Init order (§4.5)
        public void Init(ShiftServices shift) {
            cabin = shift.Cabin;
            cabin.SetAlightingRule(AlightsAt);
            cabin.OnPassengerBoarded += HandleBoarded;
            cabin.OnPassengerKicked += HandleKicked;
            cabin.OnPassengerLeft += HandleLeft;
            route = shift.Route;
            progress = shift.Progress;
            if (progress != null) {
                progress.OnMissed += HandleMissed;
                progress.OnTerminus += HandleTerminus;
            }
            shift.Debug.Register("Riders", WriteDebug);
        }

        void OnDestroy() {
            if (cabin != null) {
                cabin.OnPassengerBoarded -= HandleBoarded;
                cabin.OnPassengerKicked -= HandleKicked;
                cabin.OnPassengerLeft -= HandleLeft;
            }
            if (progress != null) {
                progress.OnMissed -= HandleMissed;
                progress.OnTerminus -= HandleTerminus;
            }
        }

        // §2.4 Missed: no fare and no penalty for the riders left behind; the ones aboard who were
        // going there ride on to the end stop instead
        void HandleMissed(StopRecord stop) {
            BusStop busStop = route != null ? route.Stop(stop.StopId) : null;
            if (busStop != null) {
                walkingAway.Clear();
                busStop.TakeAllWaiting(walkingAway);
                for (int i = 0; i < walkingAway.Count; i++) {
                    Passenger passenger = walkingAway[i];
                    RiderRecord record = For(passenger);
                    if (record != null && record.Status == RiderStatus.Waiting) {
                        SetStatus(record, RiderStatus.Lost);
                    }
                    // Away from the road: the stop's +x points at the kerb
                    passenger.WalkAway(busStop.transform.right);
                }
            }
            StopRecord end = progress.EndStop;
            if (end == null) {
                return;
            }
            for (int i = 0; i < aboard.Count; i++) {
                RiderRecord record = aboard[i];
                if (!record.IsMonster && record.DestinationStopId == stop.StopId) {
                    record.DestinationStopId = end.StopId;
                    record.Retargeted = true;
                }
            }
        }

        // §2.4 End stop: the doors are fully open, so every non-monster rider aboard is delivered
        // here, whatever their stop was. They still walk off through the doors (the alighting rule
        // matches them), but the night is already won. Monsters still aboard simply stay behind:
        // no penalty, no bounty.
        void HandleTerminus() {
            StopRecord end = progress.EndStop;
            delivering.Clear();
            for (int i = 0; i < aboard.Count; i++) {
                if (!aboard[i].IsMonster) {
                    delivering.Add(aboard[i]);
                }
            }
            for (int i = 0; i < delivering.Count; i++) {
                RiderRecord record = delivering[i];
                if (end != null && record.DestinationStopId != end.StopId) {
                    // Only a rider whose stop lies past the night's end (not in any manifest) gets here
                    record.DestinationStopId = end.StopId;
                    record.Retargeted = true;
                }
                record.ExitStopId = end != null ? end.StopId : CurrentStopId();
                SetStatus(record, RiderStatus.Delivered);
            }
        }

        // The spawner registers every rider it creates, in manifest order
        public RiderRecord Register(RiderSpec spec, Passenger passenger) {
            RiderRecord record = new RiderRecord("r" + (all.Count + 1).ToString("00"), all.Count, spec) {
                Passenger = passenger,
                Status = RiderStatus.Waiting,
            };
            all.Add(record);
            if (passenger != null) {
                byPassenger[passenger] = record;
            }
            return record;
        }

        public RiderRecord For(Passenger passenger) {
            if (passenger == null) {
                return null;
            }
            byPassenger.TryGetValue(passenger, out RiderRecord record);
            return record;
        }

        // Every non-monster rider sitting down right now (the Mimic's templates). Allocates.
        public List<RiderRecord> SeatedNormals() {
            List<RiderRecord> seated = new List<RiderRecord>();
            for (int i = 0; i < aboard.Count; i++) {
                RiderRecord record = aboard[i];
                if (!record.IsMonster && record.Passenger != null && record.Passenger.State == PassengerState.Seated) {
                    seated.Add(record);
                }
            }
            return seated;
        }

        // A rider placed straight into a seat (tests, debug) counts as aboard without boarding
        public void MarkAboard(Passenger passenger) {
            RiderRecord record = For(passenger);
            if (record != null && record.Status == RiderStatus.Waiting) {
                SetStatus(record, RiderStatus.Aboard);
            }
        }

        // A monster the Salt charm expelled (§2.14): off the bus, fare kept, no bounty
        public void MarkExpelled(Passenger passenger) {
            RiderRecord record = For(passenger);
            if (record != null) {
                record.ExitStopId = CurrentStopId();
                SetStatus(record, RiderStatus.Expelled);
            }
        }

        // The rule BusStop runs when the doors open: this stop is their destination
        bool AlightsAt(Passenger passenger, BusStop stop) {
            RiderRecord record = For(passenger);
            return record != null && !record.IsMonster && record.DestinationStopId == stop.StopId;
        }

        void HandleBoarded(Passenger passenger) {
            RiderRecord record = For(passenger);
            if (record != null) {
                SetStatus(record, RiderStatus.Aboard);
            }
        }

        void HandleKicked(Passenger passenger) {
            RiderRecord record = For(passenger);
            if (record != null) {
                record.ExitStopId = CurrentStopId();
                SetStatus(record, RiderStatus.Kicked);
            }
        }

        void HandleLeft(Passenger passenger) {
            RiderRecord record = For(passenger);
            if (record == null || record.Status == RiderStatus.Kicked || record.Status == RiderStatus.Delivered) {
                RemoveAboard(record);
                return;
            }
            record.ExitStopId = CurrentStopId();
            SetStatus(record, RiderStatus.Delivered);
        }

        string CurrentStopId() {
            BusStop stop = cabin != null ? cabin.CurrentStop : null;
            return stop != null ? stop.StopId : "";
        }

        internal void SetStatus(RiderRecord record, RiderStatus status) {
            if (record.Status == status) {
                return;
            }
            record.Status = status;
            if (status == RiderStatus.Aboard) {
                if (!aboard.Contains(record)) {
                    aboard.Add(record);
                }
            }else {
                RemoveAboard(record);
            }
            if (OnStatusChanged != null) {
                OnStatusChanged(record);
            }
        }

        void RemoveAboard(RiderRecord record) {
            if (record != null) {
                aboard.Remove(record);
            }
        }

        void WriteDebug(StringBuilder text) {
            text.Append(aboard.Count).Append(" aboard of ").Append(all.Count).Append('\n');
            for (int i = 0; i < all.Count; i++) {
                RiderRecord record = all[i];
                text.Append(record.RiderId).Append(' ').Append(record.Spec.lookId).Append(' ')
                    .Append(record.Spec.boardStopId).Append("->").Append(record.DestinationStopId).Append(' ')
                    .Append(record.Status);
                if (record.Retargeted) {
                    text.Append(" (stop missed)");
                }
                Passenger passenger = record.Passenger;
                if (passenger != null && passenger.Seat != null) {
                    text.Append(" R").Append(passenger.Seat.Row);
                }
                if (record.Spec.decoy != DecoyKind.None) {
                    text.Append(" decoy ").Append(record.Spec.decoy);
                }
                if (record.IsMonster) {
                    text.Append(" [").Append(record.MonsterId).Append(']');
                }
                text.Append('\n');
            }
        }
    }
}
