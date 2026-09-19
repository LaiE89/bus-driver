using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum PlayerMode { Driving, OnFoot }

// Owns which control scheme is live, the cursor, and a stand-in pause until the
// real ingameMenus UI is added to this scene. The stub writes the same
// ingameMenus.pausedGame flag, so gameplay scripts won't change when it's swapped.
public class PlayerModeController : MonoBehaviour {
    [SerializeField] BusInput busInput;
    [SerializeField] DriverLook driverLook;
    [SerializeField] CCTVSystem cctv;
    [SerializeField] KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] KeyCode menuKey = KeyCode.M;

    public PlayerMode Mode { get; private set; }
    public event Action<PlayerMode> OnModeChanged;

    void Awake() {
        ingameMenus.pausedGame = false;
        Time.timeScale = 1;
        ApplySavedSettingsIfNeeded();
    }

    void Start() {
        SetMode(PlayerMode.Driving);
        ApplyCursor();
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
            SetPaused(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene("Menu");
        }
    }

    void SetPaused(bool paused) {
        ingameMenus.pausedGame = paused;
        Time.timeScale = paused ? 0 : 1;
        ApplyCursor();
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
