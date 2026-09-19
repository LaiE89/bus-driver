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

    public void SetMode(PlayerMode mode) {
        Mode = mode;
        bool driving = mode == PlayerMode.Driving;
        busInput.enabled = driving;
        driverLook.enabled = driving;
        cctv.enabled = driving;
        if (!driving) {
            Debug.LogWarning("On foot mode is not implemented yet");
        }
        OnModeChanged?.Invoke(mode);
    }

    void Update() {
        if (Input.GetKeyDown(pauseKey)) {
            SetPaused(!ingameMenus.pausedGame);
        }else if (ingameMenus.pausedGame && Input.GetKeyDown(menuKey)) {
            BackToMainMenu();
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
