using System;
using System.Collections.Generic;
using System.Text;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Attention {
    // Where the player's eyes are (§2.8)
    public enum AttentionMode : int { None = 0, Road = 1, Cctv = 2, OnFoot = 3 }

    // Observer evaluation (§2.8, D35). Every frame it works out the attention mode and, for every
    // rider of the night, which observers see them: the head anchor (or, for riders flagged with
    // SetObserveBodyToo, the body root) must be inside the camera's viewport with a 5 % margin,
    // within the observer's range, with no Occluder collider (the hull shell, the driver partition)
    // on the line from the camera. Only the observer the mode makes live counts: the active CCTV
    // camera, the driver camera, the on-foot camera, plus the mirror while on the road.
    // Monsters read ObservedBy and filter it by their own observer kinds (§2.9). Runs in
    // LateUpdate, after the cameras and riders have moved, and allocates nothing per frame.
    public sealed class PlayerAttention : MonoBehaviour {
        // Viewport margin on each side (§2.8)
        public const float ViewportMargin = 0.05f;
        const int KindCount = 4;
        static readonly ObserverKinds[] Kinds = { ObserverKinds.Cctv, ObserverKinds.Driver, ObserverKinds.OnFoot, ObserverKinds.Mirror };

        struct Observer {
            public Camera Camera;
            public float Range;
            public ObserverKinds Kind;
        }

        ShiftDirector director;
        PlayerModeController mode;
        CCTVSystem cctv;
        PassengerRegistry riders;
        Transform bus;
        Camera driverCamera;
        Camera onFootCamera;
        Camera mirrorCamera;
        BalanceConfig balance;
        readonly Observer[] live = new Observer[2];
        int liveCount;

        // Per rider, indexed by RiderRecord.Index
        ObserverKinds[] observedBy = new ObserverKinds[0];
        float[] streak = new float[0];
        float[] total = new float[0];
        float[] since = new float[0];
        // Since last observed by each kind, KindCount per rider
        float[] sinceByKind = new float[0];
        bool[] observeBody = new bool[0];
        int tracked;

        public AttentionMode Mode { get; private set; }
        // The CCTV camera on screen, or -1
        public int CctvIndex { get; private set; } = -1;
        // Mode is Road and the head is within roadYawTolerance of straight ahead
        public bool AttentionOnRoad { get; private set; }
        // The driver camera's yaw from the bus's forward, degrees, + to the right
        public float DriverYaw { get; private set; }
        // The observer kinds live this frame
        public ObserverKinds LiveObservers { get; private set; }
        public bool HasMirror { get { return mirrorCamera != null; } }

        // The mode or the CCTV camera changed
        public event Action<AttentionMode> OnModeChanged;

        // ShiftContext, step 4 of the Init order (§4.5)
        public void Init(ShiftServices shift) {
            director = shift.Director;
            mode = shift.Mode;
            cctv = shift.Cctv;
            riders = shift.Riders;
            bus = shift.Bus != null ? shift.Bus.transform : null;
            driverCamera = shift.DriverCamera;
            onFootCamera = shift.OnFootCamera;
            balance = shift.Balance;
            if (cctv != null) {
                cctv.OnViewChanged += HandleViewChanged;
            }
            if (mode != null) {
                mode.OnModeChanged += HandlePlayerModeChanged;
            }
            if (director != null) {
                director.OnStateChanged += HandleStateChanged;
            }
            RefreshMode();
            shift.Debug.Register("Attention", WriteDebug);
        }

        void OnDestroy() {
            if (cctv != null) {
                cctv.OnViewChanged -= HandleViewChanged;
            }
            if (mode != null) {
                mode.OnModeChanged -= HandlePlayerModeChanged;
            }
            if (director != null) {
                director.OnStateChanged -= HandleStateChanged;
            }
        }

        // The Mirror item's camera (§2.18, M7); null removes it
        public void SetMirror(Camera camera) {
            mirrorCamera = camera;
        }

        // The Weeping Angel counts as seen when its body root is in view too (§2.12b)
        public void SetObserveBodyToo(Passenger passenger, bool on) {
            int i = IndexOf(passenger);
            if (i < 0) {
                return;
            }
            EnsureCapacity(i + 1);
            observeBody[i] = on;
        }

        // -------------------------------------------------------------- queries

        public ObserverKinds ObservedBy(Passenger passenger) {
            int i = TrackedIndexOf(passenger);
            return i >= 0 ? observedBy[i] : ObserverKinds.None;
        }

        public ObserverKinds ObservedBy(RiderRecord record) {
            return record != null && record.Index < tracked ? observedBy[record.Index] : ObserverKinds.None;
        }

        // Seen by any of the given kinds this frame
        public bool IsObserved(Passenger passenger, ObserverKinds counted) {
            return (ObservedBy(passenger) & counted) != 0;
        }

        // How long it has been observed without a break, by any observer; 0 while unobserved
        public float TimeObserved(Passenger passenger) {
            int i = TrackedIndexOf(passenger);
            return i >= 0 ? streak[i] : 0f;
        }

        // All the time it has been observed tonight
        public float TotalTimeObserved(Passenger passenger) {
            int i = TrackedIndexOf(passenger);
            return i >= 0 ? total[i] : 0f;
        }

        // Since any observer last saw it (0 while observed); counted from when it was first tracked
        public float TimeSinceObserved(Passenger passenger) {
            int i = TrackedIndexOf(passenger);
            return i >= 0 ? since[i] : float.PositiveInfinity;
        }

        // Since any of the given kinds last saw it, for monsters that don't count every observer
        public float TimeSinceObserved(Passenger passenger, ObserverKinds counted) {
            int i = TrackedIndexOf(passenger);
            if (i < 0) {
                return float.PositiveInfinity;
            }
            float best = float.PositiveInfinity;
            for (int k = 0; k < KindCount; k++) {
                if ((counted & Kinds[k]) != 0 && sinceByKind[i * KindCount + k] < best) {
                    best = sinceByKind[i * KindCount + k];
                }
            }
            return best;
        }

        // The observation test itself (§2.8): viewport with margin, range, and no Occluder on the line
        internal static bool Sees(Camera camera, Vector3 point, float range) {
            if (camera == null) {
                return false;
            }
            Vector3 from = camera.transform.position;
            if ((point - from).sqrMagnitude > range * range) {
                return false;
            }
            Vector3 viewport = camera.WorldToViewportPoint(point);
            if (viewport.z <= 0f
                || viewport.x < ViewportMargin || viewport.x > 1f - ViewportMargin
                || viewport.y < ViewportMargin || viewport.y > 1f - ViewportMargin) {
                return false;
            }
            // The occluders are triggers so they never collide (§4.16); the linecast must still see them
            return !Physics.Linecast(from, point, Layers.Mask(Layers.Occluder), QueryTriggerInteraction.Collide);
        }

        // -------------------------------------------------------------- per frame

        void LateUpdate() {
            Tick(Time.deltaTime);
        }

        // One frame of evaluation; tests call it directly to check it allocates nothing
        internal void Tick(float dt) {
            if (riders == null) {
                return;
            }
            RefreshMode();
            GatherObservers();
            bool ticking = Mode != AttentionMode.None && dt > 0f;
            IReadOnlyList<RiderRecord> all = riders.All;
            EnsureCapacity(all.Count);
            tracked = all.Count;
            for (int i = 0; i < all.Count; i++) {
                ObserverKinds seen = Evaluate(all[i], i);
                observedBy[i] = seen;
                if (!ticking) {
                    continue;
                }
                if (seen != ObserverKinds.None) {
                    streak[i] += dt;
                    total[i] += dt;
                    since[i] = 0f;
                }else {
                    streak[i] = 0f;
                    since[i] += dt;
                }
                for (int k = 0; k < KindCount; k++) {
                    int slot = i * KindCount + k;
                    sinceByKind[slot] = (seen & Kinds[k]) != 0 ? 0f : sinceByKind[slot] + dt;
                }
            }
        }

        ObserverKinds Evaluate(RiderRecord record, int i) {
            Passenger passenger = record.Passenger;
            if (liveCount == 0 || passenger == null || !passenger.isActiveAndEnabled || passenger.Head == null) {
                return ObserverKinds.None;
            }
            ObserverKinds seen = ObserverKinds.None;
            Vector3 head = passenger.Head.position;
            for (int o = 0; o < liveCount; o++) {
                Observer observer = live[o];
                if (Sees(observer.Camera, head, observer.Range)
                    || (observeBody[i] && Sees(observer.Camera, passenger.transform.position, observer.Range))) {
                    seen |= observer.Kind;
                }
            }
            return seen;
        }

        void GatherObservers() {
            liveCount = 0;
            switch (Mode) {
                case AttentionMode.Cctv:
                    CctvCamera cam = cctv.ActiveCctv;
                    if (cam != null) {
                        AddObserver(cam.Camera, cam.ObserveRange > 0f ? cam.ObserveRange : Range(ObserverKinds.Cctv), ObserverKinds.Cctv);
                    }
                    break;
                case AttentionMode.Road:
                    AddObserver(driverCamera, Range(ObserverKinds.Driver), ObserverKinds.Driver);
                    if (mirrorCamera != null) {
                        AddObserver(mirrorCamera, Range(ObserverKinds.Mirror), ObserverKinds.Mirror);
                    }
                    break;
                case AttentionMode.OnFoot:
                    AddObserver(onFootCamera, Range(ObserverKinds.OnFoot), ObserverKinds.OnFoot);
                    break;
            }
            ObserverKinds liveKinds = ObserverKinds.None;
            for (int o = 0; o < liveCount; o++) {
                liveKinds |= live[o].Kind;
            }
            LiveObservers = liveKinds;
        }

        void AddObserver(Camera camera, float range, ObserverKinds kind) {
            if (camera == null) {
                return;
            }
            live[liveCount++] = new Observer { Camera = camera, Range = range, Kind = kind };
        }

        float Range(ObserverKinds kind) {
            if (balance == null) {
                return 0f;
            }
            switch (kind) {
                case ObserverKinds.Cctv: return balance.cctvRange;
                case ObserverKinds.Driver: return balance.driverRange;
                case ObserverKinds.OnFoot: return balance.onFootRange;
                case ObserverKinds.Mirror: return balance.mirrorRange;
                default: return 0f;
            }
        }

        // -------------------------------------------------------------- mode

        void HandleViewChanged(int index) { RefreshMode(); }
        void HandlePlayerModeChanged(PlayerMode playerMode) { RefreshMode(); }
        void HandleStateChanged(ShiftState state) { RefreshMode(); }

        void RefreshMode() {
            AttentionMode next;
            int cam = -1;
            if (director == null || director.State != ShiftState.Driving) {
                next = AttentionMode.None;
            }else if (mode != null && mode.Mode == PlayerMode.OnFoot) {
                next = AttentionMode.OnFoot;
            }else if (cctv != null && cctv.IsViewingCCTV) {
                next = AttentionMode.Cctv;
                cam = cctv.ActiveIndex;
            }else {
                next = AttentionMode.Road;
            }
            DriverYaw = ComputeDriverYaw();
            float tolerance = balance != null ? balance.roadYawTolerance : 35f;
            AttentionOnRoad = next == AttentionMode.Road && Mathf.Abs(DriverYaw) <= tolerance;
            if (next == Mode && cam == CctvIndex) {
                return;
            }
            Mode = next;
            CctvIndex = cam;
            if (OnModeChanged != null) {
                OnModeChanged(next);
            }
        }

        float ComputeDriverYaw() {
            if (driverCamera == null || bus == null) {
                return 0f;
            }
            Vector3 up = bus.up;
            Vector3 look = Vector3.ProjectOnPlane(driverCamera.transform.forward, up);
            if (look.sqrMagnitude < 1e-6f) {
                return 0f;
            }
            return Vector3.SignedAngle(Vector3.ProjectOnPlane(bus.forward, up), look, up);
        }

        // -------------------------------------------------------------- bookkeeping

        int IndexOf(Passenger passenger) {
            RiderRecord record = riders != null ? riders.For(passenger) : null;
            return record != null ? record.Index : -1;
        }

        int TrackedIndexOf(Passenger passenger) {
            int i = IndexOf(passenger);
            return i >= 0 && i < tracked ? i : -1;
        }

        // Grows only when riders are added, never per frame
        void EnsureCapacity(int count) {
            if (count <= observedBy.Length) {
                return;
            }
            int size = Math.Max(count, Math.Max(16, observedBy.Length * 2));
            Array.Resize(ref observedBy, size);
            Array.Resize(ref streak, size);
            Array.Resize(ref total, size);
            Array.Resize(ref since, size);
            Array.Resize(ref observeBody, size);
            Array.Resize(ref sinceByKind, size * KindCount);
        }

        void WriteDebug(StringBuilder text) {
            text.Append("mode ").Append(Mode);
            if (Mode == AttentionMode.Cctv) {
                text.Append('(').Append(CctvIndex).Append(')');
            }
            text.Append("  on road ").Append(AttentionOnRoad ? "yes" : "no")
                .Append("  yaw ").Append(DriverYaw.ToString("0")).Append('\n');
            text.Append("observers ").Append(LiveObservers).Append('\n');
            if (riders == null) {
                return;
            }
            IReadOnlyList<RiderRecord> all = riders.All;
            for (int i = 0; i < tracked && i < all.Count; i++) {
                if (observedBy[i] == ObserverKinds.None) {
                    continue;
                }
                text.Append(all[i].RiderId).Append(' ').Append(observedBy[i])
                    .Append(' ').Append(streak[i].ToString("0.0")).Append(" s\n");
            }
        }
    }
}
