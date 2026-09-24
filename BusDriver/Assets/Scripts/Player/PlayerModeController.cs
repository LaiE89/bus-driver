using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum PlayerMode { Driving, OnFoot }

// Scene hub for the bus route: control scheme, pause, cursor, and shared services
// (sound / optional dialogue) that menus and triggers used to get from SceneController.
public class PlayerModeController : MonoBehaviour {
    public static PlayerModeController Instance { get; private set; }

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

    [Header("Services")]
    public SoundController soundController;
    [SerializeField] string ambienceSound = "Wind Ambience";
    public DialogueController dialogueController;
    public DialogueController objectivesController;

    [Header("Input")]
    [SerializeField] KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] KeyCode menuKey = KeyCode.M;

    public PlayerMode Mode { get; private set; }
    public event Action<PlayerMode> OnModeChanged;
    public Camera OnFootCamera { get { return onFootCamera; } }
    // Only from a complete stop, and not while the screen is showing a CCTV feed
    public bool CanLeaveSeat { get { return Mode == PlayerMode.Driving && bus.IsStopped && !cctv.IsViewingCCTV; } }
    public bool CanUseDoors { get { return Mode == PlayerMode.Driving && doors.CanToggle && !cctv.IsViewingCCTV; } }

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
    public Vector3 PlayerPosition
    {
        get
        {
            if (Mode == PlayerMode.OnFoot)
                return onFoot.transform.position;

            return driverCamera.transform.position;
        }
    }

    // Order matters. Going on foot: the bus is frozen before the interior colliders
    // appear. Coming back is the exact reverse, so the colliders are gone again before
    // the bus turns back into a dynamic body.
    public void SetMode(PlayerMode mode) {
        Mode = mode;
        if (mode == PlayerMode.OnFoot) {
            busInput.enabled = false;
            driverLook.enabled = false;
            cctv.enabled = false;
            bus.SetFrozen(true);
            cabin.SetWalkable(true);
            onFoot.Place(cabin.StandPointWorld + Vector3.up * 0.05f, bus.transform.eulerAngles.y + 180f);
            onFoot.gameObject.SetActive(true);
            cctv.SetHomeCamera(onFootCamera);
            ears.SetParent(onFoot.Head, false);
        }else {
            ears.SetParent(earsSeatParent, false);
            cctv.SetHomeCamera(driverCamera);
            onFoot.gameObject.SetActive(false);
            cabin.SetWalkable(false);
            bus.SetFrozen(false);
            busInput.enabled = true;
            driverLook.enabled = true;
            cctv.enabled = true;
        }
        OnModeChanged?.Invoke(mode);
    }

    void Update() {
        if (Input.GetKeyDown(pauseKey)) {
            SetPaused(!ingameMenus.pausedGame);
        }else if (ingameMenus.pausedGame && Input.GetKeyDown(menuKey)) {
            BackToMainMenu();
        }
        if (ingameMenus.pausedGame || Mode != PlayerMode.Driving) {
            return;
        }
        // Seated there is nothing to point at, so these two don't go through the interactor
        if (Input.GetKeyDown(GameKeys.interact)) {
            TryLeaveSeat();
        }else if (Input.GetKeyDown(GameKeys.doors) && CanUseDoors) {
            doors.TryToggle();
        }
    }

    void SetPaused(bool paused) {
        ingameMenus.pausedGame = paused;
        Time.timeScale = paused ? 0 : 1;
        ApplyCursor();
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
            }
        }
        if (brightness > 0f) {
            RenderSettings.ambientLight = new Color(brightness, brightness, brightness, 1.0f);
        }
    }
}
