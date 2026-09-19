using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DrivingHUD : MonoBehaviour {
    [SerializeField] BusController bus;
    [SerializeField] CCTVSystem cctv;

    [Header("Panels")]
    [SerializeField] GameObject driverPanel;
    [SerializeField] GameObject cctvPanel;
    [SerializeField] GameObject pausePanel;

    [Header("Driver")]
    [SerializeField] TMP_Text speedText;
    [SerializeField] TMP_Text gearText;
    [SerializeField] TMP_Text controlsText;

    [Header("CCTV")]
    [SerializeField] TMP_Text camLabelText;
    [SerializeField] TMP_Text timestampText;
    [SerializeField] TMP_Text recText;
    [SerializeField] RawImage scanlines;

    // Seconds since midnight, the shift starts a little after 2 AM
    [SerializeField] float clockStart = 2 * 3600 + 13 * 60;

    float clock;

    void Awake() {
        clock = clockStart;
        if (scanlines != null) {
            scanlines.texture = CreateScanlineTexture();
        }
    }

    void OnEnable() {
        cctv.OnViewChanged += HandleViewChanged;
        HandleViewChanged(cctv.ActiveIndex);
    }

    void OnDisable() {
        cctv.OnViewChanged -= HandleViewChanged;
    }

    void Start() {
        controlsText.text = $"W/S DRIVE   A/D STEER   SHIFT HANDBRAKE   {ControlsMenu.switchCameraKey.ToString().ToUpper()} CAMERAS";
    }

    void HandleViewChanged(int index) {
        bool viewingCCTV = index >= 0;
        driverPanel.SetActive(!viewingCCTV);
        cctvPanel.SetActive(viewingCCTV);
        camLabelText.text = cctv.ActiveLabel;
    }

    void Update() {
        pausePanel.SetActive(ingameMenus.pausedGame);
        clock += Time.deltaTime;

        if (cctv.IsViewingCCTV) {
            int total = (int)clock % 86400;
            int hours = total / 3600;
            int hours12 = hours % 12 == 0 ? 12 : hours % 12;
            timestampText.text = $"{hours12:00}:{total / 60 % 60:00}:{total % 60:00} {(hours < 12 ? "AM" : "PM")}";
            recText.enabled = Time.unscaledTime % 1f < 0.5f;
            // One texture repeat per 4 screen pixels
            scanlines.uvRect = new Rect(0f, 0f, 1f, Screen.height / 4f);
        }else {
            speedText.text = $"{Mathf.RoundToInt(bus.SpeedKmh)} km/h";
            gearText.text = GearLabel(bus.CurrentGear);
        }
    }

    string GearLabel(BusController.Gear gear) {
        switch (gear) {
            case BusController.Gear.Drive:
                return "D";
            case BusController.Gear.Reverse:
                return "R";
            default:
                return "N";
        }
    }

    Texture2D CreateScanlineTexture() {
        Texture2D texture = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Repeat;
        for (int y = 0; y < 4; y++) {
            texture.SetPixel(0, y, new Color(0f, 0f, 0f, y == 0 ? 0.35f : 0f));
        }
        texture.Apply();
        return texture;
    }
}
