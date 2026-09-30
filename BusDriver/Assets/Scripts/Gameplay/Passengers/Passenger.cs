using System;
using System.Collections;
using UnityEngine;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Views;
using BusDriver.Gameplay.World;

namespace BusDriver.Gameplay.Passengers {
    public enum PassengerState { Waiting, Boarding, Seated, Leaving, Gone }

    // Base class for everyone who rides the bus. Monsters are passengers too (see Monster),
    // so anything a monster might want to do differently is a virtual hook here.
    // Movement is plain waypoint walking in bus-local space: the bus moves, so no NavMesh,
    // and passengers have no physics.
    // Logic only (§4.14): the root owns Anchor_Head (observation) and the interact collider; the
    // visuals are a PassengerViewBase child that ViewFactory creates at spawn.
    public class Passenger : MonoBehaviour, IInteractable {
        // Anchor_Head's local height, standing and seated (the MVP numbers, §4.14)
        public const float StandingHeadHeight = 1.62f;
        public const float SeatedHeadHeight = 0.94f;

        [Tooltip("Anchor_Head: what observers look at (§2.8)")]
        [SerializeField] Transform anchorHead;
        [SerializeField] Collider interactCollider;
        [SerializeField] float walkSpeed = 1.3f;
        [SerializeField] float turnSpeed = 360f;
        [SerializeField] float exitWalkDistance = 2.5f;
        [SerializeField] float sitTime = 0.4f;
        [SerializeField] string displayName = "Passenger";

        public PassengerState State { get; private set; }
        public BusSeat Seat { get; private set; }
        public BusCabin Cabin { get; private set; }
        public bool IsAboard { get; private set; }
        public bool WasKicked { get; private set; }
        public string DisplayName { get { return displayName; } }
        // The logic head, for observation linecasts and "is it on camera"
        public Transform Head { get { return anchorHead; } }
        public PassengerViewBase View { get { return view; } }
        public string LookId { get; private set; } = "";
        // The night this rider belongs to; null for one placed by hand (tests)
        protected ShiftServices Shift { get; private set; }

        PassengerViewBase view;
        bool seatedPose;
        BusStop homeStop;
        Vector3 waitPosition;
        Quaternion waitRotation;
        bool abortRequested;

        protected virtual void Awake() {
            State = PassengerState.Waiting;
            SetSeatedPose(false);
            RefreshInteractable();
        }

        void Update() {
            if (State != PassengerState.Gone) {
                Tick(Time.deltaTime);
            }
        }

        // ------------------------------------------------------------ hooks

        // Which seat to head for. A quirk could be "always sits right behind the driver".
        protected virtual BusSeat ChooseSeat(BusCabin cabin) { return cabin.FindFreeSeat(); }
        // Through the door and standing in the aisle
        protected virtual void OnBoarded() { }
        protected virtual void OnSeated() { }
        // The driver told this passenger to get off. Return false to refuse.
        protected virtual bool OnKickRequested() { return true; }
        protected virtual void OnLeaving(bool kicked) { }
        // Off the bus
        protected virtual void OnLeft() { }
        // Every frame in every state except Gone
        protected virtual void Tick(float deltaTime) { }
        // The driver, on foot, started or stopped looking straight at this passenger
        public virtual void SetFocused(bool focused) { }

        public virtual string Prompt { get { return "Kick out"; } }
        public virtual bool CanInteract { get { return State == PassengerState.Seated; } }

        public void Interact() {
            Kick();
        }

        // Rough visibility test with no occlusion, enough for "am I on camera right now"
        protected bool IsSeenBy(Camera cam) {
            if (cam == null || !cam.enabled || anchorHead == null) {
                return false;
            }
            Vector3 viewport = cam.WorldToViewportPoint(anchorHead.position);
            return viewport.z > 0f && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;
        }

        // -------------------------------------------------------------- API

        // From the spawner, before the rider does anything (§4.5)
        public virtual void Bind(ShiftServices shift) {
            Shift = shift;
        }

        // ViewFactory: the rider's visuals, replacing any previous view (the Mimic changes looks)
        public void AttachView(PassengerViewBase newView, string lookId) {
            if (view != null && view != newView) {
                Destroy(view.gameObject);
            }
            view = newView;
            LookId = lookId ?? "";
            if (view != null) {
                view.SetPose(seatedPose ? PassengerPose.Seated : PassengerPose.Standing);
            }
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
            SetSeatedPose(true);
            State = PassengerState.Seated;
            RefreshInteractable();
            OnBoarded();
            OnSeated();
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

        // Getting off by choice, same walk as being kicked
        public bool Leave() {
            if (State != PassengerState.Seated) {
                return false;
            }
            StartCoroutine(LeaveRoutine(false));
            return true;
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

        // --------------------------------------------------------- routines

        IEnumerator BoardRoutine() {
            BusDoors doors = Cabin.Doors;
            yield return WalkWorld(() => Cabin.DoorOutsideWorld, () => abortRequested);
            while (!abortRequested && !doors.TryEnter(this)) {
                yield return null;
            }
            if (abortRequested) {
                doors.Exit(this);
                yield return ReturnToStop();
                yield break;
            }

            transform.SetParent(Cabin.PassengerRoot, true);
            IsAboard = true;
            Cabin.Register(this);
            yield return WalkLocal(Cabin.DoorStepLocal);
            yield return WalkLocal(Cabin.AisleAtDoorLocal);
            doors.Exit(this);
            OnBoarded();
            Cabin.NotifyBoarded(this);

            yield return WalkLocal(Cabin.SeatAisleLocal(Seat));
            yield return Slide(Cabin.SeatLocal(Seat), Quaternion.identity);
            SetSeatedPose(true);
            State = PassengerState.Seated;
            RefreshInteractable();
            OnSeated();
            Cabin.NotifySeated(this);
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
            // Hunting monsters may already have vacated their seat
            Vector3 aisle = Seat != null
                ? Cabin.SeatAisleLocal(Seat)
                : new Vector3(Cabin.AisleAtDoorLocal.x, transform.localPosition.y, transform.localPosition.z);
            State = PassengerState.Leaving;
            if (Seat != null) {
                Seat.Release(this);
                Seat = null;
            }
            RefreshInteractable();
            OnLeaving(kicked);
            // The bus stays put from now until this passenger is off
            doors.Hold(this);

            SetSeatedPose(false);
            yield return Slide(aisle, transform.localRotation);
            yield return WalkLocal(Cabin.AisleAtDoorLocal);
            while (!doors.TryEnter(this)) {
                yield return null;
            }
            yield return WalkLocal(Cabin.DoorStepLocal);
            transform.SetParent(null, true);
            yield return WalkWorld(() => Cabin.DoorOutsideWorld, null);

            Vector3 away = transform.position + Cabin.ExitDirection * exitWalkDistance;
            BusCabin cabin = Cabin;
            IsAboard = false;
            doors.Exit(this);
            doors.Release(this);
            cabin.Unregister(this);
            OnLeft();
            cabin.NotifyLeft(this);

            yield return WalkWorld(() => away, null);
            State = PassengerState.Gone;
            // Nights reload their scenes, so nothing is pooled; kicked riders never come back (D50)
            Destroy(gameObject);
        }

        IEnumerator WalkWorld(Func<Vector3> target, Func<bool> cancel) {
            SetLocomotion(walkSpeed);
            while (cancel == null || !cancel()) {
                Vector3 goal = target();
                if ((goal - transform.position).sqrMagnitude < 0.0004f) {
                    break;
                }
                Face(goal - transform.position, false);
                transform.position = Vector3.MoveTowards(transform.position, goal, walkSpeed * Time.deltaTime);
                yield return null;
            }
            SetLocomotion(0f);
        }

        // Bus-local, so it stays right even if the bus drives off mid-walk
        IEnumerator WalkLocal(Vector3 goal) {
            SetLocomotion(walkSpeed);
            while ((goal - transform.localPosition).sqrMagnitude >= 0.0004f) {
                Face(goal - transform.localPosition, true);
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, goal, walkSpeed * Time.deltaTime);
                yield return null;
            }
            SetLocomotion(0f);
        }

        void SetLocomotion(float metresPerSecond) {
            if (view != null) {
                view.SetLocomotion(metresPerSecond);
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

        // Seated, the root sits on the cushion; the logic head moves with the pose and the view
        // draws it (§4.14)
        protected void SetSeatedPose(bool seated) {
            seatedPose = seated;
            if (anchorHead != null) {
                anchorHead.localPosition = new Vector3(0f, seated ? SeatedHeadHeight : StandingHeadHeight, 0f);
            }
            if (view != null) {
                view.SetPose(seated ? PassengerPose.Seated : PassengerPose.Standing);
            }
        }

        // Stand up and free the seat so the angel (or similar) can walk the aisle.
        protected void VacateSeat() {
            if (Seat != null) {
                Seat.Release(this);
                Seat = null;
            }
            SetSeatedPose(false);
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
}
