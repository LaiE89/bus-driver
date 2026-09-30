using System;
using UnityEngine;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;

namespace BusDriver.Gameplay.Player {
    public enum PlayerMode { Driving, OnFoot }

    // The Driving ⇄ OnFoot switch, and nothing else (§4.6, D46). Pause, cursor, game over and NPC
    // spawning used to live here too (as SceneController); they moved to PauseService,
    // CursorService, LegacyGameOver and a rider spawner on the route (T-M1-15; removed in T-M2-07).
    public class PlayerModeController : MonoBehaviour {
        [Header("Driving")]
        [SerializeField] BusInput busInput;
        [SerializeField] DriverLook driverLook;
        [SerializeField] CCTVSystem cctv;

        [Header("On foot")]
        [SerializeField] BusController bus;
        [SerializeField] BusCabin cabin;
        [SerializeField] BusDoors doors;
        [SerializeField] OnFootController onFoot;
        [SerializeField] Camera driverCamera;
        [SerializeField] Camera onFootCamera;
        // The single AudioListener follows whichever body the player is in
        [SerializeField] Transform ears;
        [SerializeField] Transform earsSeatParent;
        [Tooltip("Seated body shown while driving so CCTV can see the player")]
        [SerializeField] GameObject driverAvatar;

        GameServices game;

        public PlayerMode Mode { get; private set; }
        public event Action<PlayerMode> OnModeChanged;
        public Camera OnFootCamera { get { return onFootCamera; } }
        public Camera DriverCamera { get { return driverCamera; } }
        bool IsViewingCCTV { get { return cctv != null && cctv.IsViewingCCTV; } }

        // Only from a complete stop, and not while the screen is showing a CCTV feed
        public bool CanLeaveSeat {
            get {
                return Mode == PlayerMode.Driving
                    && bus != null && bus.IsStopped
                    && !IsViewingCCTV;
            }
        }

        public bool CanUseDoors {
            get {
                return Mode == PlayerMode.Driving
                    && doors != null && doors.CanToggle
                    && !IsViewingCCTV;
            }
        }

        // Where the player's body is, for anything that walks toward it
        public Vector3 PlayerPosition {
            get {
                if (Mode == PlayerMode.OnFoot && onFoot != null) {
                    return onFoot.transform.position;
                }
                if (driverCamera != null) {
                    return driverCamera.transform.position;
                }
                return transform.position;
            }
        }

        void Awake() {
            PlayerAvatarVisuals.HideAvatarFromCamera(driverCamera);
            PlayerAvatarVisuals.HideAvatarFromCamera(onFootCamera);
            if (driverAvatar != null) {
                PlayerAvatarVisuals.ApplyCullLayer(driverAvatar.transform);
            }
        }

        // ShiftContext, step 3 of the Init order (§4.5). The night starts in the driver's seat.
        public void Init(ShiftServices shift) {
            game = shift.Game;
            SetMode(PlayerMode.Driving);
        }

        public bool TryLeaveSeat() {
            if (!CanLeaveSeat) {
                return false;
            }
            SetMode(PlayerMode.OnFoot);
            return true;
        }

        public bool TrySitDown() {
            if (Mode != PlayerMode.OnFoot) {
                return false;
            }
            SetMode(PlayerMode.Driving);
            return true;
        }

        // Order matters. Going on foot: the bus is frozen before the interior colliders
        // appear. Coming back is the exact reverse, so the colliders are gone again before
        // the bus turns back into a dynamic body.
        public void SetMode(PlayerMode mode) {
            if (busInput == null || driverLook == null || cctv == null || bus == null
                || cabin == null || onFoot == null || driverCamera == null || onFootCamera == null) {
                Log.Error(LogCat.Flow, "PlayerModeController.SetMode: missing scene refs, skipping mode change");
                return;
            }
            Mode = mode;
            if (mode == PlayerMode.OnFoot) {
                busInput.enabled = false;
                driverLook.enabled = false;
                cctv.enabled = false;
                bus.SetFrozen(true);
                cabin.SetWalkable(true);
                if (driverAvatar != null) {
                    driverAvatar.SetActive(false);
                }
                onFoot.Place(cabin.StandPointWorld + Vector3.up * 0.05f, bus.transform.eulerAngles.y + 180f);
                onFoot.gameObject.SetActive(true);
                cctv.SetHomeCamera(onFootCamera);
                if (ears != null) {
                    ears.SetParent(onFoot.Head, false);
                }
            }else {
                if (ears != null && earsSeatParent != null) {
                    ears.SetParent(earsSeatParent, false);
                }
                cctv.SetHomeCamera(driverCamera);
                onFoot.gameObject.SetActive(false);
                if (driverAvatar != null) {
                    driverAvatar.SetActive(true);
                }
                cabin.SetWalkable(false);
                bus.SetFrozen(false);
                busInput.enabled = true;
                driverLook.enabled = true;
                cctv.enabled = true;
            }
            if (game != null) {
                game.Input.SetContext(mode == PlayerMode.OnFoot ? InputContext.OnFoot : InputContext.Driving);
            }
            if (OnModeChanged != null) {
                OnModeChanged(mode);
            }
        }

        // The Driving map is only live in the Driving context, so a paused game, a screen or the
        // Game Over overlay reads as no input
        void Update() {
            if (game == null || game.Pause.IsPaused || Mode != PlayerMode.Driving) {
                return;
            }
            if (game.Input.Actions.LeaveSeat.WasPressedThisFrame()) {
                TryLeaveSeat();
            }else if (game.Input.Actions.Doors.WasPressedThisFrame() && CanUseDoors) {
                doors.TryToggle();
            }
        }
    }
}
