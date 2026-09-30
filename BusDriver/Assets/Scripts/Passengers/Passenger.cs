using System;
using System.Collections;
using UnityEngine;

public enum PassengerState { Waiting, Boarding, Greeting, Seated, Leaving, Gone }

// Base class for everyone who rides the bus. Monsters are passengers too (see Monster),
// so anything a monster might want to do differently is a virtual hook here.
// Movement is plain waypoint walking in bus-local space: the bus moves, so no NavMesh,
// and passengers have no physics.
public class Passenger : MonoBehaviour, IInteractable {
    [SerializeField] protected Transform head;
    [SerializeField] protected Transform body;
    [SerializeField] Collider interactCollider;
    [SerializeField] float walkSpeed = 1.3f;
    [SerializeField] float turnSpeed = 360f;
    [SerializeField] float exitWalkDistance = 2.5f;
    [SerializeField] float sitTime = 0.4f;
    [SerializeField] string displayName = "Passenger";
    [Tooltip("Sound played when they ring for their stop")]
    [SerializeField] string stopRequestSound = "Stop Request";

    public PassengerState State { get; private set; }
    public BusSeat Seat { get; private set; }
    public BusCabin Cabin { get; private set; }
    public bool IsAboard { get; private set; }
    public bool WasKicked { get; private set; }
    public string DisplayName { get { return displayName; } }

    // Where they want off, and how the trip is going
    public BusStop Destination { get; private set; }
    public bool HasRequestedStop { get; private set; }
    public bool MissedDestination { get; private set; }
    // Standing in the stairwell, waiting for the driver to wave them on or turn them away
    public bool AwaitingBoardingDecision {
        get { return State == PassengerState.Greeting && decision == BoardingDecision.Pending; }
    }

    // Monsters ride along but never leave a review
    public virtual bool RatesRide { get { return true; } }
    // Monsters have nowhere to be: they ride until the driver kicks them off
    public virtual bool RidesToDestination { get { return true; } }
    // Picks which set of random lines this NPC speaks
    public virtual string DialogueKind { get { return "Passenger"; } }

    enum BoardingDecision { Pending, Accepted, Refused }

    BusStop homeStop;
    Vector3 waitPosition;
    Quaternion waitRotation;
    bool abortRequested;
    BoardingDecision decision;
    float rideStartTime;
    float rideStartDistance;
    int crashPoints;
    bool atDestination;

    protected virtual void Awake() {
        State = PassengerState.Waiting;
        SetPose(false);
        RefreshInteractable();
    }

    void Update() {
        if (State == PassengerState.Gone) {
            return;
        }
        UpdateRide();
        Tick(Time.deltaTime);
    }

    // ------------------------------------------------------------ hooks

    // Which seat to head for. A quirk could be "always sits right behind the driver".
    protected virtual BusSeat ChooseSeat(BusCabin cabin) { return cabin.FindFreeSeat(); }
    // Through the door and standing in the aisle
    protected virtual void OnBoarded() { }
    protected virtual void OnSeated() { }
    // The driver told this passenger to get off. Return false to refuse.
    protected virtual bool OnKickRequested() { return true; }
    // The driver just talked to this passenger in their seat
    protected virtual void OnChatted() { }
    protected virtual void OnLeaving(bool kicked) { }
    // Off the bus
    protected virtual void OnLeft() { }
    // Every frame in every state except Gone
    protected virtual void Tick(float deltaTime) { }
    // The driver, on foot, started or stopped looking straight at this passenger
    public virtual void SetFocused(bool focused) { }

    public virtual string Prompt { get { return "Talk"; } }
    public virtual bool CanInteract { get { return State == PassengerState.Seated; } }
    public virtual string AltPrompt { get { return "Kick out"; } }

    public void Interact() {
        Chat();
    }

    public void AltInteract() {
        Kick();
    }

    // Rough visibility test with no occlusion, enough for "am I on camera right now"
    protected bool IsSeenBy(Camera cam) {
        if (cam == null || !cam.enabled || head == null) {
            return false;
        }
        Vector3 viewport = cam.WorldToViewportPoint(head.position);
        return viewport.z > 0f && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;
    }

    // -------------------------------------------------------------- API

    // Reset a pooled passenger so they can wait at a stop again.
    public void PrepareForWaiting(BusStop stop, Vector3 position, Quaternion rotation) {
        StopAllCoroutines();
        Cabin = null;
        Seat = null;
        IsAboard = false;
        WasKicked = false;
        abortRequested = false;
        decision = BoardingDecision.Pending;
        Destination = null;
        HasRequestedStop = false;
        MissedDestination = false;
        atDestination = false;
        crashPoints = 0;
        homeStop = stop;
        waitPosition = position;
        waitRotation = rotation;
        transform.SetParent(stop != null ? stop.transform : null, true);
        transform.SetPositionAndRotation(position, rotation);
        State = PassengerState.Waiting;
        SetPose(false);
        RefreshInteractable();
        gameObject.SetActive(true);
    }

    // Walk from a stop onto the bus. False when there is no free seat.
    public bool Board(BusCabin cabin, BusStop stop) {
        if (State != PassengerState.Waiting) {
            return false;
        }
        BusSeat seat = ChooseSeat(cabin);
        if (seat == null || !seat.Reserve(this)) {
            return false;
        }
        Cabin = cabin;
        Seat = seat;
        homeStop = stop;
        waitPosition = transform.position;
        waitRotation = transform.rotation;
        abortRequested = false;
        decision = BoardingDecision.Pending;
        Destination = RidesToDestination ? RouteStops.PickDestination(stop) : null;
        HasRequestedStop = false;
        MissedDestination = false;
        atDestination = false;
        crashPoints = 0;
        State = PassengerState.Boarding;
        StartCoroutine(BoardRoutine());
        return true;
    }

    // Already riding when the scene starts
    public void PlaceSeated(BusCabin cabin, BusSeat seat) {
        if (!seat.Reserve(this)) {
            return;
        }
        Cabin = cabin;
        Seat = seat;
        IsAboard = true;
        cabin.Register(this);
        transform.SetParent(cabin.PassengerRoot, true);
        transform.localPosition = cabin.SeatLocal(seat);
        transform.localRotation = Quaternion.identity;
        SetPose(true);
        State = PassengerState.Seated;
        Destination = RidesToDestination ? RouteStops.PickDestination(cabin.CurrentStop) : null;
        BeginRide();
        RefreshInteractable();
        OnBoarded();
        OnSeated();
    }

    // The driver waves them aboard
    public void AcceptAboard() {
        if (AwaitingBoardingDecision) {
            decision = BoardingDecision.Accepted;
        }
    }

    // Turned away at the door. No review: refusing a fare is the driver's call.
    public void RefuseAtDoor() {
        if (AwaitingBoardingDecision) {
            decision = BoardingDecision.Refused;
        }
    }

    // The driver came back for a chat. Ignored while anyone else is mid-sentence so
    // holding the button cannot stack up a queue of lines.
    public bool Chat() {
        if (State != PassengerState.Seated) {
            return false;
        }
        DialogueController controller = SceneController.Instance != null
            ? SceneController.Instance.dialogueController
            : null;
        if (controller != null && controller.isPlaying) {
            return false;
        }
        Speak(PassengerDialogue.Seated(this));
        OnChatted();
        return true;
    }

    public bool Kick() {
        if (State != PassengerState.Seated || !OnKickRequested()) {
            return false;
        }
        WasKicked = true;
        Cabin.NotifyKicked(this);
        StartCoroutine(LeaveRoutine(true));
        return true;
    }

    // Told to get off here: their own stop, or the next one after the driver missed it.
    // Riders with no destination (monsters) only ever leave by being kicked off.
    public bool DropOff(bool missed) {
        if (State != PassengerState.Seated || !RidesToDestination) {
            return false;
        }
        if (missed) {
            MissedDestination = true;
        }
        StartCoroutine(LeaveRoutine(false));
        return true;
    }

    // Getting off by choice, same walk as being kicked
    public bool Leave() {
        if (State != PassengerState.Seated) {
            return false;
        }
        StartCoroutine(LeaveRoutine(false));
        return true;
    }

    // Counted while they are aboard, so a rough shift shows up in their review
    public void NotifyCrash(bool major) {
        if (State == PassengerState.Seated || State == PassengerState.Greeting) {
            crashPoints += major ? 2 : 1;
        }
    }

    // Doors are closing. Ignored once aboard.
    public void AbortBoarding() {
        if (State == PassengerState.Boarding && !IsAboard) {
            abortRequested = true;
        }
    }

    public void RefreshInteractable() {
        if (interactCollider != null) {
            // Never a live collider while the bus is driving
            interactCollider.enabled = State == PassengerState.Seated && Cabin != null && Cabin.IsWalkable;
        }
    }

    // ------------------------------------------------------------- the ride

    void BeginRide() {
        rideStartTime = Time.time;
        rideStartDistance = DistanceToDestination();
    }

    // Halfway there they ring for their stop; driving past it costs the driver the fare.
    void UpdateRide() {
        if (State != PassengerState.Seated || Destination == null || Cabin == null) {
            return;
        }
        if (!HasRequestedStop && rideStartDistance > 1f
            && DistanceToDestination() <= rideStartDistance * 0.5f) {
            HasRequestedStop = true;
            if (SceneController.Instance != null && SceneController.Instance.soundController != null) {
                SceneController.Instance.soundController.PlayOneShot(stopRequestSound);
            }
        }

        BusStop current = Cabin.CurrentStop;
        if (current == Destination) {
            atDestination = true;
        }else if (atDestination) {
            // Their stop is behind us and they are still aboard
            atDestination = false;
            MissedDestination = true;
        }
    }

    // Straight line: the road is built as a loop in the editor with no runtime spline to
    // measure along, and half the gap to the stop is close enough to ring the bell.
    float DistanceToDestination() {
        if (Destination == null || Cabin == null) {
            return 0f;
        }
        return Vector3.Distance(Cabin.transform.position, Destination.transform.position);
    }

    // One review per finished ride: zero for a kick or a missed stop, otherwise scored
    // on how long the trip took and how rough it was. Drop-offs also tick the shift quota.
    void ReportRide(bool kicked) {
        bool missed = MissedDestination;
        RideRatings ratings = SceneController.Instance != null ? SceneController.Instance.Ratings : null;
        if (!RatesRide) {
            Speak(PassengerDialogue.Farewell(this, -1, kicked, missed));
        }else {
            int stars;
            if (kicked || missed) {
                stars = 0;
            }else if (ratings != null) {
                stars = ratings.ScoreRide(rideStartDistance, Time.time - rideStartTime, crashPoints);
            }else {
                stars = 5;
            }
            Speak(PassengerDialogue.Farewell(this, stars, kicked, missed));
            if (ratings != null) {
                ratings.AddReview(stars);
            }
        }
        // Kicked passengers do not count toward the drop-off quota
        if (!kicked && ratings != null) {
            ratings.RecordDropOff();
        }
    }

    void Speak(Dialogue dialogue) {
        if (dialogue == null || SceneController.Instance == null) {
            return;
        }
        DialogueController controller = SceneController.Instance.dialogueController;
        if (controller != null) {
            controller.QueueDialogue(dialogue);
        }
    }

    // --------------------------------------------------------- routines

    IEnumerator BoardRoutine() {
        BusDoors doors = Cabin.Doors;
        Cabin.JoinDoorQueue(this);
        // Shuffle up the kerb in single file. Only the front of the line takes the door.
        yield return QueueWorld(
            () => Cabin.DoorQueueWorld(this),
            // Front of the line only, and never while somebody is still getting off
            () => Cabin.IsDoorQueueHead(this) && !Cabin.AnyLeaving && doors.TryEnter(this),
            () => abortRequested);
        if (!abortRequested) {
            yield return WalkWorld(() => Cabin.DoorOutsideWorld, () => abortRequested);
        }
        if (abortRequested) {
            Cabin.LeaveDoorQueue(this);
            doors.Exit(this);
            yield return ReturnToStop();
            yield break;
        }

        transform.SetParent(Cabin.PassengerRoot, true);
        IsAboard = true;
        Cabin.Register(this);
        yield return WalkLocal(Cabin.DoorStepLocal);
        yield return WalkLocal(Cabin.AisleAtDoorLocal);

        // Stand in the stairwell and talk until the driver decides. The hold keeps the
        // bus parked while somebody is still standing on the step.
        State = PassengerState.Greeting;
        decision = BoardingDecision.Pending;
        doors.Hold(this);
        Speak(PassengerDialogue.Greeting(this, Destination));
        while (decision == BoardingDecision.Pending) {
            FaceDriver();
            yield return null;
        }

        // Decided either way, so the next in line can start walking up. The doorway token
        // still keeps them off the step until this one is out of it.
        Cabin.LeaveDoorQueue(this);
        if (decision == BoardingDecision.Refused) {
            yield return RefusedRoutine(doors);
            yield break;
        }

        doors.Release(this);
        doors.Exit(this);
        OnBoarded();
        Cabin.NotifyBoarded(this);

        yield return WalkLocal(Cabin.SeatAisleLocal(Seat));
        yield return Slide(Cabin.SeatLocal(Seat), Quaternion.identity);
        SetPose(true);
        State = PassengerState.Seated;
        BeginRide();
        RefreshInteractable();
        OnSeated();
        Cabin.NotifySeated(this);
    }

    // Turned away at the door: back down the step and off into the night, no review
    IEnumerator RefusedRoutine(BusDoors doors) {
        State = PassengerState.Leaving;
        if (Seat != null) {
            Seat.Release(this);
            Seat = null;
        }
        RefreshInteractable();
        Speak(PassengerDialogue.Refused(this));
        yield return WalkLocal(Cabin.DoorStepLocal);
        yield return StepOffAndVanish(doors);
    }

    IEnumerator ReturnToStop() {
        Seat.Release(this);
        Seat = null;
        yield return WalkWorld(() => waitPosition, null);
        transform.rotation = waitRotation;
        State = PassengerState.Waiting;
        BusStop stop = homeStop;
        Cabin = null;
        if (stop != null) {
            stop.AddWaiting(this);
        }
    }

    IEnumerator LeaveRoutine(bool kicked) {
        BusDoors doors = Cabin.Doors;
        State = PassengerState.Leaving;
        RefreshInteractable();
        OnLeaving(kicked);
        // The bus stays put from now until this passenger is off
        doors.Hold(this);
        // Stay in the seat while anyone is still coming through the door. The aisle is
        // one person wide, so the two groups never share it.
        while (Cabin.AnyBoarding) {
            yield return null;
        }

        // Hunting monsters may already have vacated their seat
        Vector3 row = Seat != null
            ? Cabin.SeatAisleLocal(Seat)
            : new Vector3(Cabin.AisleAtDoorLocal.x, transform.localPosition.y, transform.localPosition.z);
        if (Seat != null) {
            Seat.Release(this);
            Seat = null;
        }
        Cabin.JoinExitQueue(this);
        SetPose(false);
        yield return Slide(Cabin.ExitStepOutLocal(this, row), transform.localRotation);
        // Follow the person in front down the aisle until the doorway is ours
        yield return QueueLocal(
            () => Cabin.ExitQueueLocal(this),
            () => Cabin.IsExitQueueHead(this) && doors.TryEnter(this));
        yield return WalkLocal(Cabin.DoorStepLocal);
        // They have their say from the step, where the driver can still hear them
        FaceDriver();
        ReportRide(kicked);
        yield return StepOffAndVanish(doors);
    }

    // Shared tail of every exit: off the step, clear of the bus, then back to the pool
    IEnumerator StepOffAndVanish(BusDoors doors) {
        transform.SetParent(null, true);
        yield return WalkWorld(() => Cabin.DoorOutsideWorld, null);

        Vector3 away = transform.position + Cabin.ExitDirection * exitWalkDistance;
        BusCabin cabin = Cabin;
        IsAboard = false;
        // Clear of the door, so the queues behind can both move up
        cabin.LeaveExitQueue(this);
        cabin.LeaveDoorQueue(this);
        doors.Exit(this);
        doors.Release(this);
        cabin.Unregister(this);
        OnLeft();
        cabin.NotifyLeft(this);

        yield return WalkWorld(() => away, null);
        State = PassengerState.Gone;
        if (SceneController.Instance != null) {
            SceneController.Instance.DespawnNpc(this);
        }else {
            Destroy(gameObject);
        }
    }

    IEnumerator WalkWorld(Func<Vector3> target, Func<bool> cancel) {
        while (cancel == null || !cancel()) {
            Vector3 goal = target();
            if ((goal - transform.position).sqrMagnitude < 0.0004f) {
                yield break;
            }
            Face(goal - transform.position, false);
            transform.position = Vector3.MoveTowards(transform.position, goal, walkSpeed * Time.deltaTime);
            yield return null;
        }
    }

    // Bus-local, so it stays right even if the bus drives off mid-walk
    IEnumerator WalkLocal(Vector3 goal) {
        while ((goal - transform.localPosition).sqrMagnitude >= 0.0004f) {
            Face(goal - transform.localPosition, true);
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, goal, walkSpeed * Time.deltaTime);
            yield return null;
        }
    }

    // Standing in a queue: the spot keeps moving as the line shuffles forward, and we
    // only leave once we are standing on it and `claim` succeeds.
    IEnumerator QueueWorld(Func<Vector3> spot, Func<bool> claim, Func<bool> cancel) {
        while (cancel == null || !cancel()) {
            Vector3 goal = spot();
            Vector3 offset = goal - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude >= 0.0004f) {
                Face(offset, false);
                transform.position = Vector3.MoveTowards(transform.position, goal, walkSpeed * Time.deltaTime);
            }else if (claim()) {
                yield break;
            }
            yield return null;
        }
    }

    IEnumerator QueueLocal(Func<Vector3> spot, Func<bool> claim) {
        while (true) {
            Vector3 goal = spot();
            Vector3 offset = goal - transform.localPosition;
            offset.y = 0f;
            if (offset.sqrMagnitude >= 0.0004f) {
                Face(offset, true);
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, goal, walkSpeed * Time.deltaTime);
            }else if (claim()) {
                yield break;
            }
            yield return null;
        }
    }

    // Sitting down and standing up
    IEnumerator Slide(Vector3 localGoal, Quaternion localRotation) {
        Vector3 from = transform.localPosition;
        Quaternion fromRotation = transform.localRotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / sitTime) {
            transform.localPosition = Vector3.Lerp(from, localGoal, t);
            transform.localRotation = Quaternion.Slerp(fromRotation, localRotation, t);
            yield return null;
        }
        transform.localPosition = localGoal;
        transform.localRotation = localRotation;
    }

    void Face(Vector3 direction, bool local) {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) {
            return;
        }
        Quaternion look = Quaternion.LookRotation(direction);
        float step = turnSpeed * Time.deltaTime;
        if (local) {
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, look, step);
        }else {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, step);
        }
    }

    void FaceDriver() {
        if (SceneController.Instance == null || Cabin == null) {
            return;
        }
        Transform space = Cabin.PassengerRoot != null ? Cabin.PassengerRoot : Cabin.transform;
        Vector3 driverLocal = space.InverseTransformPoint(SceneController.Instance.PlayerPosition);
        Face(driverLocal - transform.localPosition, true);
    }

    // Greybox poses. Seated, the root sits on the cushion, so the body is the short
    // capsule the CCTV cameras were framed around.
    protected virtual void SetPose(bool seated) {
        if (body != null) {
            body.localScale = seated ? new Vector3(0.42f, 0.42f, 0.42f) : new Vector3(0.42f, 0.75f, 0.42f);
            body.localPosition = new Vector3(0f, seated ? 0.42f : 0.75f, 0f);
        }
        if (head != null) {
            head.localPosition = new Vector3(0f, seated ? 0.94f : 1.62f, 0f);
        }
    }

    // Stand up and free the seat so the angel (or similar) can walk the aisle.
    protected void VacateSeat() {
        if (Seat != null) {
            Seat.Release(this);
            Seat = null;
        }
        SetPose(false);
        RefreshInteractable();
    }

    protected virtual void OnDestroy() {
        if (Seat != null) {
            Seat.Release(this);
        }
        if (Cabin != null) {
            if (Cabin.Doors != null) {
                Cabin.Doors.Exit(this);
                Cabin.Doors.Release(this);
            }
            Cabin.Unregister(this);
        }
    }
}
