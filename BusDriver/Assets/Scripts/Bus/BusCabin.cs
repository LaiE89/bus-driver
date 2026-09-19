using System;
using System.Collections.Generic;
using UnityEngine;

public enum SeatPreference { Random, FrontFirst, RearFirst }

// Everything passengers need to know about the inside of the bus: seats, the path
// through the door, who is aboard. Game rules (penalties, monster kills, route score)
// should subscribe to the events here and test `passenger is Monster`.
public class BusCabin : MonoBehaviour {
    [SerializeField] BusController bus;
    [SerializeField] BusDoors doors;
    [SerializeField] CCTVSystem cctv;
    [SerializeField] BusSeat[] seats;
    [SerializeField] Transform passengerRoot;
    [SerializeField] GameObject interiorColliders;
    [SerializeField] BusStop[] stops;
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
    // The camera currently looking into the bus: a CCTV camera, the driver, or the player on foot
    public Camera ViewCamera { get { return cctv != null ? cctv.ActiveCamera : null; } }

    public Vector3 AisleAtDoorLocal { get { return ToLocal(aisleAtDoor); } }
    public Vector3 DoorStepLocal { get { return ToLocal(doorStep); } }
    public Vector3 DoorStepWorld { get { return doorStep.position; } }
    public Vector3 StandPointWorld { get { return standPoint.position; } }
    public Vector3 ExitDirection { get { return transform.right; } }

    readonly List<Passenger> passengers = new List<Passenger>();

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
            return free[UnityEngine.Random.Range(0, free.Count)];
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
            foreach (BusStop stop in stops) {
                if (stop != null && stop.Contains(this)) {
                    return stop;
                }
            }
            return null;
        }
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
