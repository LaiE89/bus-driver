using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Attention;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Scares;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Monsters {
    // The Starer: after grace, while nobody is watching it, it periodically teleports one seat row
    // forward with no sound or FX. From the near-front row the next hop is an immediate kill scare
    // (no aisle stand). It never kills while the player is on foot. Rear seating is the definition's
    // seat preference.
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
        float unobservedTimer;
        bool killing;

        public int BoardRow { get; private set; }
        public int CurrentRow { get { return passenger != null && passenger.Seat != null ? passenger.Seat.Row : 0; } }
        public int Advances { get; private set; }
        public bool LensArmed { get { return lensArmed; } }
        public bool LensPlayed { get; private set; }

        public void Bind(ShiftServices services, MonsterBrain owner) {
            shift = services;
            brain = owner;
            passenger = owner.Passenger;
            config = owner.Definition.Ability<StarerAdvanceConfig>() ?? new StarerAdvanceConfig();
            passenger.Seated += HandleSeated;
            brain.Meter.OnStageChanged += HandleStageChanged;
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
            if (shift == null || !brain.IsActive || killing) {
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
            TickAdvance();
        }

        void TickAdvance() {
            BusSeat seat = passenger.Seat;
            if (seat == null || BoardRow == 0 || brain.Meter.Core.InGrace) {
                unobservedTimer = 0f;
                return;
            }
            if (brain.IsObserved) {
                unobservedTimer = 0f;
                return;
            }
            unobservedTimer += Time.deltaTime;
            if (!StarerRules.ShouldHop(unobservedTimer, config.advanceIntervalSeconds)) {
                return;
            }
            unobservedTimer = 0f;
            TryHop(seat);
        }

        void TryHop(BusSeat seat) {
            if (StarerRules.IsKillHop(seat.Row, config.killFromRow)) {
                if (PlayerOnFoot) {
                    // Never kill on foot; stay put until they sit back down
                    Log.Verbose(LogCat.Threat, "starer: kill hop skipped (player on foot)");
                    return;
                }
                StartCoroutine(KillPlayer());
                return;
            }
            int nextRow = StarerRules.NextRow(seat.Row);
            for (int row = nextRow; row >= StarerRules.FrontRow; row--) {
                BusSeat next = NearestFreeSeat(row, seat.Column);
                if (next != null && passenger.MoveToSeat(next)) {
                    Advances++;
                    Log.Verbose(LogCat.Threat, $"starer hopped R{seat.Row} → R{row} (silent)");
                    return;
                }
            }
        }

        bool PlayerOnFoot {
            get { return shift.Mode != null && shift.Mode.Mode == PlayerMode.OnFoot; }
        }

        IEnumerator KillPlayer() {
            if (killing) {
                yield break;
            }
            killing = true;
            Log.Info(LogCat.Threat, "starer: kill hop");
            ScareDefinition scare = brain.Definition != null ? brain.Definition.killScare : null;
            IDeathPreventer preventer = shift.Death != null
                ? shift.Death.PreventedBy(DeathCause.MonsterKill, MonsterIds.Starer) : null;
            if (preventer != null) {
                killing = false;
                yield break;
            }
            if (scare != null && shift.Scares != null) {
                ScareHandle handle;
                if (shift.Scares.Request(scare, ScareContext.For(brain), out handle)) {
                    while (!handle.IsNone && shift.ScarePlayer != null && shift.ScarePlayer.IsRunning(handle)) {
                        yield return null;
                    }
                }
            }
            if (shift.Death != null) {
                shift.Death.Die(DeathCause.MonsterKill, MonsterIds.Starer);
            }
        }

        BusSeat NearestFreeSeat(int row, int column) {
            IReadOnlyList<BusSeat> seats = shift.Cabin.Seats;
            BusSeat best = null;
            int bestGap = int.MaxValue;
            for (int i = 0; i < seats.Count; i++) {
                BusSeat candidate = seats[i];
                if (candidate.Row != row || !candidate.IsFree) {
                    continue;
                }
                int gap = Mathf.Abs(candidate.Column - column);
                if (gap < bestGap) {
                    best = candidate;
                    bestGap = gap;
                }
            }
            return best;
        }

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
    }
}
