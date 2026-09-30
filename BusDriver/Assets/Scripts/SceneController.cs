using System;
using UnityEngine;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using UnityEngine.SceneManagement;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.World;
using BusDriver.UI.Screens;

namespace BusDriver.Gameplay.Player {
    public enum PlayerMode { Driving, OnFoot }

    [System.Serializable]
    public class NpcSpawnEntry {
        [Tooltip("Must match an ObjectPooling pool id")]
        public string poolId = "Passenger";
        [Min(0f)] public float weight = 1f;
    }

    // Scene hub for the bus route: player mode, shared services, and NPC spawning. Pause and the
    // cursor belong to PauseService and CursorService (T-M1-08); this only says when pausing is
    // allowed and which input context the player mode wants.
    public class SceneController : MonoBehaviour, IGameBindable {
        public static SceneController Instance { get; private set; }

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

        [Header("Services")]
        public DialogueController dialogueController;
        public DialogueController objectivesController;

        [Header("NPC Spawning")]
        [SerializeField] ObjectPooling npcPool;
        [Tooltip("Weighted list of NPCs that can appear at bus stops. Edit this to change who spawns.")]
        [SerializeField] NpcSpawnEntry[] spawnableNpcs;
        [SerializeField] bool populateStopsOnStart = true;
        [Tooltip("If set, the first waiter at the first empty stop uses this pool id (useful for testing monsters).")]
        [SerializeField] string guaranteedFirstNpcId = "WeepingAngel";

        [Header("Screens")]
        [SerializeField] GameOverMenu gameOverMenu;

        GameServices game;

        public PlayerMode Mode { get; private set; }
        public bool IsGameOver { get; private set; }
        public event Action<PlayerMode> OnModeChanged;
        public Camera OnFootCamera { get { return onFootCamera; } }
        public Camera DriverCamera { get { return driverCamera; } }
        public bool IsViewingCCTV { get { return cctv != null && cctv.IsViewingCCTV; } }
        public ObjectPooling NpcPool { get { return npcPool; } }
        public NpcSpawnEntry[] SpawnableNpcs { get { return spawnableNpcs; } }

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

        void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CacheServices();
            if (npcPool == null) {
                npcPool = GetComponent<ObjectPooling>();
                if (npcPool == null) {
                    npcPool = gameObject.AddComponent<ObjectPooling>();
                }
            }
            EnsureDriverAvatar();
            PlayerAvatarVisuals.HideAvatarFromCamera(driverCamera);
            PlayerAvatarVisuals.HideAvatarFromCamera(onFootCamera);
            if (gameOverMenu == null) {
                gameOverMenu = FindAnyObjectByType<GameOverMenu>(FindObjectsInactive.Include);
            }
            if (gameOverMenu == null) {
                Log.Warn(LogCat.Flow, "SceneController: Game Over UI missing from scene. Run Tools/Bus Driver/Bake Overlay Menus Into Scene.");
            }
        }

        // From the scene root, before Start. Pausing is allowed in this scene until the game is over.
        public void Bind(GameServices services) {
            game = services;
            game.Pause.CanPause = () => !IsGameOver;
        }

        void CacheServices() {
            if (dialogueController == null) {
                GameObject go = GameObject.Find("Dialogue Controller");
                if (go != null) {
                    dialogueController = go.GetComponent<DialogueController>();
                }
            }
            if (objectivesController == null) {
                GameObject go = GameObject.Find("Objectives Controller");
                if (go != null) {
                    objectivesController = go.GetComponent<DialogueController>();
                }
            }
        }

        void Start() {
            SetMode(PlayerMode.Driving);
            if (populateStopsOnStart) {
                PopulateBusStops();
            }
        }

        // ---------------------------------------------------------- NPC spawning

        public void PopulateBusStops() {
            if (npcPool == null || spawnableNpcs == null || spawnableNpcs.Length == 0) {
                return;
            }
            BusStop[] stops = FindObjectsByType<BusStop>();
            System.Array.Sort(stops, (a, b) => b.SpawnCount.CompareTo(a.SpawnCount));
            bool placedGuaranteed = string.IsNullOrEmpty(guaranteedFirstNpcId);
            for (int i = 0; i < stops.Length; i++) {
                BusStop stop = stops[i];
                if (stop == null || stop.WaitingCount > 0) {
                    continue;
                }
                int count = stop.SpawnCount;
                for (int slot = 0; slot < count; slot++) {
                    string poolId;
                    if (!placedGuaranteed && slot == 0 && npcPool.HasPool(guaranteedFirstNpcId)) {
                        poolId = guaranteedFirstNpcId;
                        placedGuaranteed = true;
                    }else {
                        poolId = PickSpawnPoolId();
                    }
                    if (string.IsNullOrEmpty(poolId)) {
                        continue;
                    }
                    SpawnNpcAtStop(stop, poolId, slot);
                }
            }
        }

        public void PopulateStop(BusStop stop) {
            if (stop == null || stop.WaitingCount > 0) {
                return;
            }
            int count = stop.SpawnCount;
            for (int slot = 0; slot < count; slot++) {
                string poolId = PickSpawnPoolId();
                if (string.IsNullOrEmpty(poolId)) {
                    continue;
                }
                SpawnNpcAtStop(stop, poolId, slot);
            }
        }

        public Passenger SpawnNpcAtStop(BusStop stop, string poolId, int slot) {
            if (stop == null || npcPool == null) {
                return null;
            }
            Vector3 local = stop.WaitLocalPosition(slot);
            Vector3 worldPos = stop.transform.TransformPoint(local);
            Quaternion worldRot = stop.transform.rotation * Quaternion.Euler(0f, -90f, 0f);
            GameObject go = npcPool.Spawn(poolId, worldPos, worldRot, stop.transform);
            if (go == null) {
                return null;
            }
            go.name = poolId;
            Passenger passenger = go.GetComponent<Passenger>();
            if (passenger == null) {
                npcPool.Despawn(go);
                Log.Warn(LogCat.Flow, $"SceneController: pooled '{poolId}' has no Passenger component");
                return null;
            }
            passenger.PrepareForWaiting(stop, worldPos, worldRot);
            stop.AddWaiting(passenger);
            return passenger;
        }

        public void DespawnNpc(Passenger passenger) {
            if (passenger == null) {
                return;
            }
            // Kicked riders never come back (§2.13), so they are not returned to the pool
            if (passenger.WasKicked) {
                Destroy(passenger.gameObject);
                return;
            }
            if (npcPool != null && npcPool.Despawn(passenger.gameObject)) {
                return;
            }
            Destroy(passenger.gameObject);
        }

        public string PickSpawnPoolId() {
            if (spawnableNpcs == null || spawnableNpcs.Length == 0) {
                return null;
            }
            float total = 0f;
            for (int i = 0; i < spawnableNpcs.Length; i++) {
                NpcSpawnEntry entry = spawnableNpcs[i];
                if (entry != null && entry.weight > 0f && npcPool != null && npcPool.HasPool(entry.poolId)) {
                    total += entry.weight;
                }
            }
            if (total <= 0f) {
                return null;
            }
            float roll = UnityEngine.Random.Range(0f, total);
            for (int i = 0; i < spawnableNpcs.Length; i++) {
                NpcSpawnEntry entry = spawnableNpcs[i];
                if (entry == null || entry.weight <= 0f || npcPool == null || !npcPool.HasPool(entry.poolId)) {
                    continue;
                }
                if (roll < entry.weight) {
                    return entry.poolId;
                }
                roll -= entry.weight;
            }
            return spawnableNpcs[0].poolId;
        }

        // ---------------------------------------------------------- player mode

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

        // Order matters. Going on foot: the bus is frozen before the interior colliders
        // appear. Coming back is the exact reverse, so the colliders are gone again before
        // the bus turns back into a dynamic body.
        public void SetMode(PlayerMode mode) {
            if (busInput == null || driverLook == null || cctv == null || bus == null
                || cabin == null || onFoot == null || driverCamera == null || onFootCamera == null) {
                Log.Warn(LogCat.Flow, "SceneController.SetMode: missing scene refs, skipping mode change");
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
            OnModeChanged?.Invoke(mode);
        }

        void EnsureDriverAvatar() {
            if (driverAvatar == null && earsSeatParent != null) {
                Transform existing = earsSeatParent.Find("DriverAvatar");
                if (existing != null) {
                    driverAvatar = existing.gameObject;
                }
            }
            if (driverAvatar != null) {
                PlayerAvatarVisuals.ApplyCullLayer(driverAvatar.transform);
                return;
            }
            Log.Warn(LogCat.Flow, "SceneController: no DriverAvatar in the scene. Run Tools/Bus Driver/Build MVP Scene.");
        }

        void Update() {
            if (IsGameOver || (game != null && game.Pause.IsPaused) || Mode != PlayerMode.Driving) {
                return;
            }
            if (game == null) {
                return;
            }
            if (game.Input.Actions.LeaveSeat.WasPressedThisFrame()) {
                TryLeaveSeat();
            }else if (game.Input.Actions.Doors.WasPressedThisFrame() && CanUseDoors) {
                doors.TryToggle();
            }
        }

        // The game stops under the Game Over screen: not a pause, since it can't be resumed
        public void TriggerGameOver() {
            if (IsGameOver) {
                return;
            }
            IsGameOver = true;
            if (game != null) {
                game.Pause.TrySetPaused(false);
                game.Input.SetContext(InputContext.Screen);
            }
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (gameOverMenu != null) {
                gameOverMenu.Show();
            }
        }

        void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }
        }
    }
}
