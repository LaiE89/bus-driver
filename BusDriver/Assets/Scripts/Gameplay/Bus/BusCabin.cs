using System;
using System.Collections.Generic;
using UnityEngine;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.World;

namespace BusDriver.Gameplay.Bus {
    // Everything passengers need to know about the inside of the bus: seats, the path
    // through the door, who is aboard. Game rules (penalties, monster kills, route score)
    // should subscribe to the events here and test `passenger is Monster`.
    public class BusCabin : MonoBehaviour {
        [SerializeField] BusController bus;
        [SerializeField] BusDoors doors;
        [SerializeField] BusSeat[] seats;
        [SerializeField] Transform passengerRoot;
        [SerializeField] GameObject interiorColliders;
        [SerializeField] DriverSeat driverSeat;
        // Already sitting on the bus when the scene starts
        [SerializeField] Passenger[] initialPassengers;

        [Header("Path nodes")]
        [Tooltip("Space left between people queueing for the door, on the kerb and in the aisle")]
        [SerializeField] float queueGap = 0.9f;
        [SerializeField] Transform aisleAtDoor;
        [SerializeField] Transform doorStep;
        [SerializeField] Transform doorOutside;
        [SerializeField] Transform standPoint;

        public event Action<Passenger> OnPassengerBoarded;
        public event Action<Passenger> OnPassengerSeated;
        public event Action<Passenger> OnPassengerKicked;
        // Fired from the doorstep with their farewell line — tip / missed-stop refund land here
        public event Action<Passenger> OnPassengerDropOff;
        public event Action<Passenger> OnPassengerLeft;

        public BusController Bus { get { return bus; } }
        public BusDoors Doors { get { return doors; } }
        public Transform PassengerRoot { get { return passengerRoot; } }
        public IReadOnlyList<Passenger> Passengers { get { return passengers; } }
        public bool IsWalkable { get; private set; }

        // Nobody still registered aboard (seated, walking the aisle, or on the step). The night
        // waits for this at the end stop before the Summary opens.
        public bool IsEmpty {
            get {
                for (int i = 0; i < passengers.Count; i++) {
                    if (passengers[i] != null) {
                        return false;
                    }
                }
                return true;
            }
        }

        // Seated riders who have rung for their stop (the HUD's STOP REQUESTED line)
        public int StopRequestCount {
            get {
                int count = 0;
                for (int i = 0; i < passengers.Count; i++) {
                    Passenger passenger = passengers[i];
                    if (passenger != null && passenger.State == PassengerState.Seated && passenger.HasRequestedStop) {
                        count++;
                    }
                }
                return count;
            }
        }

        public Vector3 AisleAtDoorLocal { get { return ToLocal(aisleAtDoor); } }
        public Vector3 DoorStepLocal { get { return ToLocal(doorStep); } }
        public Vector3 DoorStepWorld { get { return doorStep.position; } }
        public Vector3 StandPointLocal { get { return ToLocal(standPoint); } }
        public Vector3 StandPointWorld { get { return standPoint.position; } }
        public Vector3 ExitDirection { get { return transform.right; } }

        readonly List<Passenger> passengers = new List<Passenger>();
        readonly List<BusSeat> freeBuffer = new List<BusSeat>();
        // Single file on the kerb: everyone from Board() until they are through the door
        readonly List<Passenger> doorQueue = new List<Passenger>();
        // Single file down the aisle, whoever is closest to the door first
        readonly List<Passenger> exitQueue = new List<Passenger>();
        Vector3 aisleInward;
        bool aisleInwardFound;
        // The route's stops, handed over by ShiftContext (the bus prefab can't hold scene objects)
        IReadOnlyList<BusStop> stops = new BusStop[0];
        // The run's seating stream (§2.6); a fixed fallback before Init, for tests that build a cabin alone
        System.Random seating;
        // Who gets off at a stop (§2.4). Riders get destinations with the manifest (T-M3-02); until
        // then nobody alights by choice.
        Func<Passenger, BusStop, bool> alightsAt;

        // ShiftContext, step 3 of the Init order (§4.5)
        public void Init(ShiftServices shift) {
            stops = shift.Route.Stops;
            seating = shift.Rng.Get(RngStreams.Seating);
            if (driverSeat != null) {
                driverSeat.Bind(shift.Mode);
            }
        }

        void OnEnable() {
            doors.OnChanged += HandleDoorsChanged;
        }

        void OnDisable() {
            doors.OnChanged -= HandleDoorsChanged;
        }

        void Start() {
            foreach (Passenger passenger in initialPassengers) {
                if (passenger == null) {
                    continue;
                }
                BusSeat seat = NearestFreeSeat(ToLocal(passenger.transform));
                if (seat != null) {
                    passenger.PlaceSeated(this, seat);
                }
            }
        }

        // Just outside the door, on whatever the bus pulled up next to: road or kerb
        public Vector3 DoorOutsideWorld {
            get {
                Vector3 point = doorOutside.position;
                Vector3 origin = new Vector3(point.x, transform.position.y + 1.5f, point.z);
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore)) {
                    point.y = hit.point.y;
                }else {
                    point.y = 0f;
                }
                return point;
            }
        }

        public Vector3 SeatLocal(BusSeat seat) {
            return ToLocal(seat.transform);
        }

        // The spot in the aisle level with a seat
        public Vector3 SeatAisleLocal(BusSeat seat) {
            Vector3 local = SeatLocal(seat);
            return new Vector3(AisleAtDoorLocal.x, AisleAtDoorLocal.y, local.z);
        }

        Vector3 ToLocal(Transform node) {
            return passengerRoot.InverseTransformPoint(node.position);
        }

        // Which way down the aisle leads away from the door. Read off the seats so it holds
        // whichever end of the bus the door is on, and no scene needs a node for it.
        public Vector3 AisleInwardLocal {
            get {
                if (!aisleInwardFound) {
                    Vector3 door = AisleAtDoorLocal;
                    Vector3 furthest = Vector3.zero;
                    foreach (BusSeat seat in seats) {
                        if (seat == null) {
                            continue;
                        }
                        Vector3 offset = SeatAisleLocal(seat) - door;
                        offset.y = 0f;
                        if (offset.sqrMagnitude > furthest.sqrMagnitude) {
                            furthest = offset;
                        }
                    }
                    aisleInward = furthest.sqrMagnitude > 0.0001f ? furthest.normalized : Vector3.back;
                    aisleInwardFound = true;
                }
                return aisleInward;
            }
        }

        // --------------------------------------------------- boarding queue

        // Somebody is still walking to the door, so the queue outside has to wait
        public bool AnyLeaving {
            get {
                foreach (Passenger passenger in passengers) {
                    if (passenger != null && passenger.State == PassengerState.Leaving) {
                        return true;
                    }
                }
                return false;
            }
        }

        // Somebody is on the step or walking the aisle to a seat, so riders getting off stay put:
        // the aisle only fits one person. `passengers` holds nobody but those already aboard, so
        // the line still out on the kerb does not count here. They are held outside by AnyLeaving
        // instead, which keeps the two rules from blocking each other.
        public bool AnyBoarding {
            get {
                foreach (Passenger passenger in passengers) {
                    if (passenger != null && (passenger.State == PassengerState.Boarding
                            || passenger.State == PassengerState.Greeting)) {
                        return true;
                    }
                }
                return false;
            }
        }

        // The one standing in the stairwell waiting on a yes or no from the driver (§2.4)
        public Passenger PassengerAtDoor {
            get {
                foreach (Passenger passenger in passengers) {
                    if (passenger != null && passenger.AwaitingBoardingDecision) {
                        return passenger;
                    }
                }
                return null;
            }
        }

        public void JoinDoorQueue(Passenger passenger) {
            if (passenger != null && !doorQueue.Contains(passenger)) {
                doorQueue.Add(passenger);
            }
        }

        public void LeaveDoorQueue(Passenger passenger) {
            doorQueue.Remove(passenger);
        }

        public bool IsDoorQueueHead(Passenger passenger) {
            PruneDoorQueue();
            return doorQueue.Count > 0 && doorQueue[0] == passenger;
        }

        // Line up along the side of the bus behind whoever is next through the door. The spot
        // right outside the door stays clear for people getting off.
        public Vector3 DoorQueueWorld(Passenger passenger) {
            PruneDoorQueue();
            int place = 0;
            foreach (Passenger other in doorQueue) {
                if (other == passenger) {
                    break;
                }
                // Whoever is already on the step has left the line
                if (!other.IsAboard) {
                    place++;
                }
            }
            return DoorOutsideWorld - transform.forward * (queueGap * (place + 1));
        }

        // ------------------------------------------------------- exit queue

        public void JoinExitQueue(Passenger passenger) {
            if (passenger != null && !exitQueue.Contains(passenger)) {
                exitQueue.Add(passenger);
            }
        }

        public void LeaveExitQueue(Passenger passenger) {
            exitQueue.Remove(passenger);
        }

        public bool IsExitQueueHead(Passenger passenger) {
            SortExitQueue();
            return exitQueue.Count > 0 && exitQueue[0] == passenger;
        }

        // Where to wait: the aisle spot by the door for whoever is next off, otherwise a gap
        // behind the person ahead. Following the person in front means nobody ever has to walk
        // through anybody.
        public Vector3 ExitQueueLocal(Passenger passenger) {
            SortExitQueue();
            int index = exitQueue.IndexOf(passenger);
            if (index <= 0) {
                return AisleAtDoorLocal;
            }
            return AisleAtDoorLocal + AisleInwardLocal * (AisleProgress(exitQueue[index - 1]) + queueGap);
        }

        // Stepping out of a seat: the aisle level with their own row, or further back when
        // somebody ahead is standing there already. Both seats in a row share one aisle spot.
        public Vector3 ExitStepOutLocal(Passenger passenger, Vector3 rowLocal) {
            float row = AisleProgressAt(rowLocal);
            float queued = AisleProgressAt(ExitQueueLocal(passenger));
            return AisleAtDoorLocal + AisleInwardLocal * Mathf.Max(row, queued);
        }

        void SortExitQueue() {
            PruneExitQueue();
            exitQueue.Sort(CompareAisleProgress);
        }

        // Closest to the door leaves first. Entity ids break the tie for two people in one row,
        // so the order cannot flip from frame to frame.
        int CompareAisleProgress(Passenger a, Passenger b) {
            int compare = AisleProgress(a).CompareTo(AisleProgress(b));
            return compare != 0 ? compare : a.GetEntityId().CompareTo(b.GetEntityId());
        }

        // How far down the aisle from the door somebody is, in metres
        float AisleProgress(Passenger passenger) {
            return AisleProgressAt(passengerRoot.InverseTransformPoint(passenger.transform.position));
        }

        float AisleProgressAt(Vector3 local) {
            Vector3 offset = local - AisleAtDoorLocal;
            offset.y = 0f;
            return Vector3.Dot(offset, AisleInwardLocal);
        }

        // Only until the driver has decided about them: past that they give up their place in the
        // line even though they are still walking to a seat or back off the step.
        void PruneDoorQueue() {
            for (int i = doorQueue.Count - 1; i >= 0; i--) {
                Passenger passenger = doorQueue[i];
                if (passenger == null || (passenger.State != PassengerState.Boarding
                        && passenger.State != PassengerState.Greeting)) {
                    doorQueue.RemoveAt(i);
                }
            }
        }

        void PruneExitQueue() {
            for (int i = exitQueue.Count - 1; i >= 0; i--) {
                if (exitQueue[i] == null || exitQueue[i].State != PassengerState.Leaving) {
                    exitQueue.RemoveAt(i);
                }
            }
        }

        public int FreeSeatCount {
            get {
                int count = 0;
                foreach (BusSeat seat in seats) {
                    if (seat.IsFree) {
                        count++;
                    }
                }
                return count;
            }
        }

        // A random free seat from the seating stream (§2.6): in the first zone if it has one, else
        // the second, else anywhere. Scattered passengers are what make scanning the CCTV worth it.
        public BusSeat FindFreeSeat(SeatZone first = SeatZone.Any, SeatZone second = SeatZone.Any) {
            BusSeat seat = RandomFreeSeat(first);
            if (seat == null && second != first) {
                seat = RandomFreeSeat(second);
            }
            if (seat == null && first != SeatZone.Any && second != SeatZone.Any) {
                seat = RandomFreeSeat(SeatZone.Any);
            }
            return seat;
        }

        BusSeat RandomFreeSeat(SeatZone zone) {
            freeBuffer.Clear();
            foreach (BusSeat seat in seats) {
                if (seat.IsFree && (zone == SeatZone.Any || seat.Zone == zone)) {
                    freeBuffer.Add(seat);
                }
            }
            if (freeBuffer.Count == 0) {
                return null;
            }
            if (seating == null) {
                seating = new System.Random(0);
            }
            return freeBuffer[seating.Next(freeBuffer.Count)];
        }

        public IReadOnlyList<BusSeat> Seats { get { return seats; } }

        public BusSeat NearestFreeSeat(Vector3 busLocal) {
            BusSeat best = null;
            float bestDistance = float.MaxValue;
            foreach (BusSeat seat in seats) {
                float distance = (SeatLocal(seat) - busLocal).sqrMagnitude;
                if (seat.IsFree && distance < bestDistance) {
                    best = seat;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // The bus stop the door is lined up with, if any
        public BusStop CurrentStop {
            get {
                for (int i = 0; i < stops.Count; i++) {
                    if (stops[i] != null && stops[i].Contains(this)) {
                        return stops[i];
                    }
                }
                return null;
            }
        }

        // The rule that picks the riders who get off at a stop (PassengerRegistry, T-M3-02)
        public void SetAlightingRule(Func<Passenger, BusStop, bool> rule) {
            alightsAt = rule;
        }

        // The seated riders whose stop this is, in boarding order; they get off before anyone
        // boards (§2.4). Allocates, once per stop visit.
        public List<Passenger> AlightingAt(BusStop stop) {
            List<Passenger> alighting = new List<Passenger>();
            if (alightsAt == null) {
                return alighting;
            }
            for (int i = 0; i < passengers.Count; i++) {
                Passenger passenger = passengers[i];
                if (passenger != null && passenger.State == PassengerState.Seated && alightsAt(passenger, stop)) {
                    alighting.Add(passenger);
                }
            }
            return alighting;
        }

        // Interior colliders and passenger interaction only exist while the bus is frozen for walking
        public void SetWalkable(bool walkable) {
            IsWalkable = walkable;
            if (interiorColliders != null) {
                interiorColliders.SetActive(walkable);
            }
            foreach (Passenger passenger in passengers) {
                passenger.RefreshInteractable();
            }
        }

        void HandleDoorsChanged(bool opening) {
            if (!opening) {
                return;
            }
            BusStop stop = CurrentStop;
            if (stop != null) {
                stop.BeginBoarding(this);
            }
        }

        public void Register(Passenger passenger) {
            if (!passengers.Contains(passenger)) {
                passengers.Add(passenger);
            }
        }

        public void Unregister(Passenger passenger) {
            passengers.Remove(passenger);
        }

        public void NotifyBoarded(Passenger passenger) { OnPassengerBoarded?.Invoke(passenger); }
        public void NotifySeated(Passenger passenger) { OnPassengerSeated?.Invoke(passenger); }
        public void NotifyKicked(Passenger passenger) { OnPassengerKicked?.Invoke(passenger); }
        public void NotifyDropOff(Passenger passenger) { OnPassengerDropOff?.Invoke(passenger); }
        public void NotifyLeft(Passenger passenger) { OnPassengerLeft?.Invoke(passenger); }
    }
}
