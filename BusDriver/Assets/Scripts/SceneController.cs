using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum PlayerMode { Driving, OnFoot }

[System.Serializable]
public class NpcSpawnEntry {
    [Tooltip("Must match an ObjectPooling pool id")]
    public string poolId = "Passenger";
    [Min(0f)] public float weight = 1f;
}

// Scene hub for the bus route: player mode, pause, shared services, and NPC spawning.
public class SceneController : MonoBehaviour {
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
    public SoundController soundController;
    [SerializeField] string ambienceSound = "Wind Ambience";
    public DialogueController dialogueController;
    public DialogueController objectivesController;

    [Header("NPC Spawning")]
    [SerializeField] ObjectPooling npcPool;
    [Tooltip("Weighted list of NPCs that can appear at bus stops. Edit this to change who spawns.")]
    [SerializeField] NpcSpawnEntry[] spawnableNpcs;
    [SerializeField] bool populateStopsOnStart = true;
    [Tooltip("If set, the first waiter at the first empty stop uses this pool id (useful for testing monsters).")]
    [SerializeField] string guaranteedFirstNpcId = "WeepingAngel";

    [Header("Input")]
    [SerializeField] KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] PauseMenu pauseMenu;
    [SerializeField] GameOverMenu gameOverMenu;

    [Header("Game Over")]
    [SerializeField] float fatalCrashSpeedKmh = 50f;

    CrashDetector crashDetector;

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

        ingameMenus.pausedGame = false;
        Time.timeScale = 1;
        ApplySavedSettingsIfNeeded();
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
        if (pauseMenu == null) {
            pauseMenu = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        }
        if (gameOverMenu == null) {
            gameOverMenu = FindAnyObjectByType<GameOverMenu>(FindObjectsInactive.Include);
        }
        if (pauseMenu == null || gameOverMenu == null) {
            Debug.LogWarning("SceneController: Pause/Game Over UI missing from scene. Run Tools/Bus Driver/Bake Overlay Menus Into Scene.");
        }
        WireCrashDetector();
    }

    void WireCrashDetector() {
        if (bus == null) {
            return;
        }
        crashDetector = bus.GetComponent<CrashDetector>();
        if (crashDetector == null) {
            crashDetector = bus.gameObject.AddComponent<CrashDetector>();
        }
        crashDetector.OnCrash -= HandleCrash;
        crashDetector.OnCrash += HandleCrash;
    }

    void HandleCrash(float deltaV, bool isMajor, Collision collision) {
        if (IsGameOver) {
            return;
        }
        float impactSpeed = crashDetector != null
            ? crashDetector.PreCollisionSpeedKmh
            : (bus != null ? bus.SpeedKmh : 0f);
        if (impactSpeed > fatalCrashSpeedKmh) {
            TriggerGameOver();
        }
    }

    void CacheServices() {
        if (soundController == null) {
            soundController = FindAnyObjectByType<SoundController>();
        }
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
        ApplyCursor();
        if (soundController != null && !string.IsNullOrEmpty(ambienceSound)) {
            soundController.Play(ambienceSound);
        }
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
            Debug.LogWarning($"SceneController: pooled '{poolId}' has no Passenger component");
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
            Debug.LogWarning("SceneController.SetMode: missing scene refs, skipping mode change");
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
        Debug.LogWarning("SceneController: no DriverAvatar in the scene. Run Tools/Bus Driver/Build MVP Scene.");
    }

    void Update() {
        if (IsGameOver) {
            return;
        }
        if (Input.GetKeyDown(pauseKey)) {
            if (pauseMenu != null && pauseMenu.OptionsOpen) {
                pauseMenu.HandleEscapeFromSubmenu();
            }else {
                SetPaused(!ingameMenus.pausedGame);
            }
        }
        if (ingameMenus.pausedGame || Mode != PlayerMode.Driving) {
            return;
        }
        if (Input.GetKeyDown(GameKeys.leaveSeat)) {
            TryLeaveSeat();
        }else if (Input.GetKeyDown(GameKeys.doors) && CanUseDoors) {
            doors.TryToggle();
        }
    }

    public void TriggerGameOver() {
        if (IsGameOver) {
            return;
        }
        IsGameOver = true;
        ingameMenus.pausedGame = true;
        Time.timeScale = 0f;
        if (pauseMenu != null) {
            pauseMenu.Hide();
        }
        if (gameOverMenu != null) {
            gameOverMenu.Show();
        }
        ApplyCursor();
        if (soundController != null) {
            soundController.PauseAll();
        }
    }

    public void SetPaused(bool paused) {
        if (IsGameOver) {
            return;
        }
        ingameMenus.pausedGame = paused;
        Time.timeScale = paused ? 0 : 1;
        ApplyCursor();
        if (pauseMenu != null) {
            if (paused) {
                pauseMenu.Show();
            }else {
                pauseMenu.Hide();
            }
        }
        if (soundController == null) {
            return;
        }
        if (paused) {
            soundController.PauseAll();
        }else {
            soundController.UnPauseAll();
        }
    }

    public void BackToMainMenu() {
        SetPaused(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("Menu");
    }

    void ApplyCursor() {
        Cursor.lockState = ingameMenus.pausedGame ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = ingameMenus.pausedGame;
    }

    void OnApplicationFocus(bool hasFocus) {
        if (hasFocus) {
            ApplyCursor();
        }
    }

    void OnDestroy() {
        if (crashDetector != null) {
            crashDetector.OnCrash -= HandleCrash;
        }
        if (Instance == this) {
            Instance = null;
        }
        ingameMenus.pausedGame = false;
        Time.timeScale = 1;
    }

    // Settings normally load through the options menu in the Menu scene. When this
    // scene is played directly that never ran, so read the save file here.
    void ApplySavedSettingsIfNeeded() {
        float brightness = OptionsMenu.brightness;
        if (OptionsMenu.sens <= 0f && File.Exists(Application.persistentDataPath + "/settings.dat")) {
            OptionsData settings = OptionsSaveSystem.LoadSettings();
            if (settings != null) {
                OptionsMenu.sens = settings.sens;
                brightness = settings.brightness;
                if (settings.switchCameraKey != KeyCode.None) {
                    ControlsMenu.switchCameraKey = settings.switchCameraKey;
                }
                if (settings.handbrakeKey != KeyCode.None) {
                    GameKeys.handbrake = settings.handbrakeKey;
                }
                if (settings.doorsKey != KeyCode.None) {
                    GameKeys.doors = settings.doorsKey;
                }
                if (settings.leaveSeatKey != KeyCode.None) {
                    GameKeys.leaveSeat = settings.leaveSeatKey;
                }
                if (settings.interactKey != KeyCode.None) {
                    GameKeys.interact = settings.interactKey;
                }
            }
        }
        if (brightness > 0f) {
            RenderSettings.ambientLight = new Color(brightness, brightness, brightness, 1.0f);
        }
    }
}
