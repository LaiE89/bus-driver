using System;
using System.Collections.Generic;
using UnityEngine;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.World;

namespace BusDriver.Gameplay.Bus {
    public enum SeatPreference { Random, FrontFirst, RearFirst }

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
        [SerializeField] Transform aisleAtDoor;
        [SerializeField] Transform doorStep;
        [SerializeField] Transform doorOutside;
        [SerializeField] Transform standPoint;

        public event Action<Passenger> OnPassengerBoarded;
        public event Action<Passenger> OnPassengerSeated;
        public event Action<Passenger> OnPassengerKicked;
        public event Action<Passenger> OnPassengerLeft;

        public BusController Bus { get { return bus; } }
        public BusDoors Doors { get { return doors; } }
        public Transform PassengerRoot { get { return passengerRoot; } }
        public IReadOnlyList<Passenger> Passengers { get { return passengers; } }
        public bool IsWalkable { get; private set; }

        public Vector3 AisleAtDoorLocal { get { return ToLocal(aisleAtDoor); } }
        public Vector3 DoorStepLocal { get { return ToLocal(doorStep); } }
        public Vector3 DoorStepWorld { get { return doorStep.position; } }
        public Vector3 StandPointLocal { get { return ToLocal(standPoint); } }
        public Vector3 StandPointWorld { get { return standPoint.position; } }
        public Vector3 ExitDirection { get { return transform.right; } }

        readonly List<Passenger> passengers = new List<Passenger>();
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

        // Random by default, scattered passengers are what make scanning the CCTV worth it
        public BusSeat FindFreeSeat(SeatPreference preference = SeatPreference.Random) {
            List<BusSeat> free = new List<BusSeat>();
            foreach (BusSeat seat in seats) {
                if (seat.IsFree) {
                    free.Add(seat);
                }
            }
            if (free.Count == 0) {
                return null;
            }
            if (preference == SeatPreference.Random) {
                if (seating == null) {
                    seating = new System.Random(0);
                }
                return free[seating.Next(free.Count)];
            }
            BusSeat best = free[0];
            foreach (BusSeat seat in free) {
                bool further = SeatLocal(seat).z > SeatLocal(best).z;
                if (further == (preference == SeatPreference.FrontFirst)) {
                    best = seat;
                }
            }
            return best;
        }

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
        public void NotifyLeft(Passenger passenger) { OnPassengerLeft?.Invoke(passenger); }
    }
}
