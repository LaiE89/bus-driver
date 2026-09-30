using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Attention;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Scares;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // The Starer's ability and tells (§2.10). It punishes not watching the cabin:
    // - Advance: its target row closes from the row it first sat in to R1 as its threat rises, and
    //   whenever it has been unobserved for 1 s and sits behind that row it is suddenly in the
    //   target row (the free seat nearest its column; a full row passes it on to the next row
    //   forward; with nothing free it stays).
    // - Tells: the head always turns to the camera the player is looking through, it goes still
    //   by stage and its eyes widen with the threat.
    // - Lens scare: the first time it climbs into Aggressive, the next CCTV cycle within 20 s cuts
    //   to a camera that sees it and fills the lens with its face.
    // - Its kill sequence stands it in the aisle beside R1; an escape sits it back down in R2.
    [RequireComponent(typeof(MonsterBrain))]
    public sealed class StarerAdvance : MonoBehaviour, IMonsterPart {
        ShiftServices shift;
        MonsterBrain brain;
        Passenger passenger;
        StarerAdvanceConfig config;
        Transform looking;
        float lensWindow;
        bool lensArmed;
        bool lensDone;
        // Its seat's column when it stood up for the telegraph, to sit back down near it
        int standColumn = 1;

        // The row it first sat down in; 0 before that
        public int BoardRow { get; private set; }
        public int CurrentRow { get { return passenger != null && passenger.Seat != null ? passenger.Seat.Row : 0; } }
        public int TargetRow { get { return BoardRow > 0 ? StarerRules.TargetRow(BoardRow, brain.Threat) : 0; } }
        public int Advances { get; private set; }
        // Waiting for the next CCTV cycle to play the lens scare
        public bool LensArmed { get { return lensArmed; } }
        public bool LensPlayed { get; private set; }

        public void Bind(ShiftServices services, MonsterBrain owner) {
            shift = services;
            brain = owner;
            passenger = owner.Passenger;
            config = owner.Definition.Ability<StarerAdvanceConfig>() ?? new StarerAdvanceConfig();
            passenger.Seated += HandleSeated;
            brain.Meter.OnStageChanged += HandleStageChanged;
            if (brain.Kill != null) {
                brain.Kill.OnTelegraphStarted += HandleTelegraphStarted;
                brain.Kill.OnResolved += HandleResolved;
            }
            if (shift.Cctv != null) {
                shift.Cctv.OnViewChanged += HandleViewChanged;
            }
        }

        void OnDestroy() {
            if (passenger != null) {
                passenger.Seated -= HandleSeated;
            }
            if (brain != null) {
                brain.Meter.OnStageChanged -= HandleStageChanged;
                if (brain.Kill != null) {
                    brain.Kill.OnTelegraphStarted -= HandleTelegraphStarted;
                    brain.Kill.OnResolved -= HandleResolved;
                }
            }
            if (shift != null && shift.Cctv != null) {
                shift.Cctv.OnViewChanged -= HandleViewChanged;
            }
        }

        void HandleSeated(Passenger rider) {
            if (BoardRow == 0 && rider.Seat != null) {
                BoardRow = rider.Seat.Row;
            }
        }

        void Update() {
            if (shift == null || !brain.IsActive) {
                return;
            }
            UpdateTells();
            if (shift.Director.State != ShiftState.Driving || shift.Monsters.KillActive) {
                return;
            }
            if (lensArmed) {
                lensWindow -= Time.deltaTime;
                if (lensWindow <= 0f) {
                    lensArmed = false;
                    Log.Verbose(LogCat.Threat, "starer: no CCTV cycle within the window; the lens scare is skipped");
                }
            }
            TryAdvance();
        }

        void TryAdvance() {
            BusSeat seat = passenger.Seat;
            if (seat == null || BoardRow == 0) {
                return;
            }
            int target = TargetRow;
            if (!StarerRules.ShouldAdvance(seat.Row, target, brain.TimeSinceObserved, config.unobservedBeforeAdvance)) {
                return;
            }
            for (int row = target; row >= StarerRules.FrontRow; row--) {
                BusSeat next = NearestFreeSeat(row, seat.Column);
                if (next != null && passenger.MoveToSeat(next)) {
                    Advances++;
                    Log.Verbose(LogCat.Threat, $"starer advanced R{seat.Row} → R{row} (threat {brain.Threat:0.0})");
                    return;
                }
            }
        }

        // The free seat in the row nearest the column, or null when the row is full
        BusSeat NearestFreeSeat(int row, int column) {
            IReadOnlyList<BusSeat> seats = shift.Cabin.Seats;
            BusSeat best = null;
            int bestGap = int.MaxValue;
            for (int i = 0; i < seats.Count; i++) {
                BusSeat seat = seats[i];
                if (seat.Row != row || !seat.IsFree) {
                    continue;
                }
                int gap = Mathf.Abs(seat.Column - column);
                if (gap < bestGap) {
                    best = seat;
                    bestGap = gap;
                }
            }
            return best;
        }

        // HeadTrack always on toward the camera the player looks through, Stillness by stage,
        // EyesWide by threat (§2.10)
        void UpdateTells() {
            if (passenger.View == null) {
                return;
            }
            Camera camera = shift.Cctv != null ? shift.Cctv.ActiveCamera : shift.DriverCamera;
            Transform target = camera != null ? camera.transform : null;
            if (target != looking) {
                looking = target;
                passenger.View.SetLookAt(target, 1f);
            }
            passenger.View.SetTell(TellId.HeadTrack, 1f);
            passenger.View.SetTell(TellId.Stillness, StarerRules.Stillness((int)brain.Stage));
            passenger.View.SetTell(TellId.EyesWide, brain.Threat / 100f);
        }

        // Climbing into Aggressive (not falling back to it from Lethal) arms the lens scare once
        void HandleStageChanged(MonsterBrain owner, ThreatStage from, ThreatStage to) {
            if (to != ThreatStage.Aggressive || from >= ThreatStage.Aggressive || lensDone) {
                return;
            }
            lensDone = true;
            lensArmed = true;
            lensWindow = config.lensScareWindowSeconds;
        }

        void HandleViewChanged(int index) {
            if (!lensArmed || index < 0 || shift.Director.State != ShiftState.Driving || !brain.IsActive) {
                return;
            }
            lensArmed = false;
            ScareDefinition scare = brain.Definition.monsterScare;
            if (scare == null) {
                return;
            }
            LensPlayed = shift.Scares.Request(scare, ScareContext.AtCamera(CameraSeeing(index), brain));
        }

        // The camera just cycled to if it sees the Starer, else the first that does, else that one
        int CameraSeeing(int cycledTo) {
            IReadOnlyList<CctvCamera> cameras = shift.Cctv.Cameras;
            if (passenger.Head == null) {
                return cycledTo;
            }
            Vector3 head = passenger.Head.position;
            if (Sees(cameras[cycledTo], head)) {
                return cycledTo;
            }
            for (int i = 0; i < cameras.Count; i++) {
                if (Sees(cameras[i], head)) {
                    return i;
                }
            }
            return cycledTo;
        }

        static bool Sees(CctvCamera camera, Vector3 head) {
            return camera != null && PlayerAttention.Sees(camera.Camera, head, camera.ObserveRange);
        }

        // §2.10 Telegraph: it stands up in the aisle beside R1
        void HandleTelegraphStarted(MonsterBrain owner) {
            BusSeat front = NearestSeat(StarerRules.FrontRow);
            if (front == null) {
                return;
            }
            if (passenger.Seat != null) {
                standColumn = passenger.Seat.Column;
            }
            passenger.StandAt(shift.Cabin.SeatAisleLocal(front), Quaternion.identity);
        }

        // §2.10 Escape: it sits back down in R2 (or the nearest row behind it with a free seat)
        void HandleResolved(MonsterBrain owner, KillOutcome outcome) {
            if (outcome != KillOutcome.Escaped && outcome != KillOutcome.Spared) {
                return;
            }
            int column = passenger.Seat != null ? passenger.Seat.Column : standColumn;
            for (int row = StarerRules.EscapeRow; row <= 9; row++) {
                BusSeat seat = NearestFreeSeat(row, column);
                if (seat != null && passenger.MoveToSeat(seat)) {
                    return;
                }
            }
            BusSeat front = NearestFreeSeat(StarerRules.FrontRow, column);
            if (front != null) {
                passenger.MoveToSeat(front);
            }
        }

        BusSeat NearestSeat(int row) {
            IReadOnlyList<BusSeat> seats = shift.Cabin.Seats;
            for (int i = 0; i < seats.Count; i++) {
                if (seats[i].Row == row) {
                    return seats[i];
                }
            }
            return null;
        }
    }
}
