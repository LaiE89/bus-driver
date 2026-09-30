using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DrivingHUD : MonoBehaviour {
    [SerializeField] BusController bus;
    [SerializeField] CCTVSystem cctv;
    [SerializeField] SceneController mode;
    [SerializeField] PlayerInteractor interactor;
    [SerializeField] BusDoors doors;
    [SerializeField] BusCabin cabin;

    [Header("Panels")]
    [SerializeField] GameObject driverPanel;
    [SerializeField] GameObject cctvPanel;
    [SerializeField] GameObject pausePanel;
    [SerializeField] GameObject onFootPanel;

    [Header("Prompts")]
    [SerializeField] TMP_Text promptText;

    [Header("Driver")]
    [SerializeField] TMP_Text ratingText;
    [SerializeField] Image ratingStar;
    [SerializeField] TMP_Text quotaText;
    [SerializeField] TMP_Text speedText;
    [SerializeField] TMP_Text requestText;

    [Header("CCTV")]
    [SerializeField] TMP_Text camLabelText;
    [SerializeField] TMP_Text timestampText;
    [SerializeField] TMP_Text recText;
    [SerializeField] RawImage scanlines;

    // Seconds since midnight, the shift starts a little after 2 AM
    [SerializeField] float clockStart = 2 * 3600 + 13 * 60;

    // Star glyph handled by ratingStar Image so LiberationSans missing ★ is fine
    float clock;

    void Awake() {
        clock = clockStart;
        if (scanlines != null) {
            scanlines.texture = CreateScanlineTexture();
        }
        if (ratingStar != null && ratingStar.sprite == null) {
            ratingStar.sprite = CreateStarSprite();
            ratingStar.color = new Color(1f, 0.82f, 0.2f, 1f);
        }
    }

    void OnEnable() {
        cctv.OnViewChanged += HandleViewChanged;
        mode.OnModeChanged += HandleModeChanged;
        HandleViewChanged(cctv.ActiveIndex);
    }

    void OnDisable() {
        cctv.OnViewChanged -= HandleViewChanged;
        mode.OnModeChanged -= HandleModeChanged;
    }

    void HandleModeChanged(PlayerMode newMode) {
        HandleViewChanged(cctv.ActiveIndex);
    }

    void HandleViewChanged(int index) {
        bool viewingCCTV = index >= 0;
        bool onFoot = mode.Mode == PlayerMode.OnFoot;
        driverPanel.SetActive(!viewingCCTV && !onFoot);
        cctvPanel.SetActive(viewingCCTV);
        onFootPanel.SetActive(onFoot);
        camLabelText.text = cctv.ActiveLabel;
    }

    void Update() {
        clock += Time.deltaTime;
        promptText.text = BuildPrompt();
        UpdateRatingText();
        UpdateQuotaText();
        UpdateRequestText();

        if (cctv.IsViewingCCTV) {
            int total = (int)clock % 86400;
            int hours = total / 3600;
            int hours12 = hours % 12 == 0 ? 12 : hours % 12;
            timestampText.text = $"{hours12:00}:{total / 60 % 60:00}:{total % 60:00} {(hours < 12 ? "AM" : "PM")}";
            recText.enabled = Time.unscaledTime % 1f < 0.5f;
            // One texture repeat per 4 screen pixels
            scanlines.uvRect = new Rect(0f, 0f, 1f, Screen.height / 4f);
        }else if (speedText != null) {
            string gear = bus.IsParked ? "P" : GearLabel(bus.CurrentGear);
            speedText.text = $"{Mathf.RoundToInt(bus.SpeedKmh)} km/h  {gear}";
        }
    }

    void UpdateRatingText() {
        if (ratingText == null) {
            return;
        }
        RideRatings ratings = mode != null ? mode.Ratings : null;
        ratingText.text = ratings != null ? $"{ratings.Average:0.0}" : "";
        if (ratingStar != null) {
            ratingStar.enabled = ratings != null;
        }
    }

    void UpdateQuotaText() {
        if (quotaText == null) {
            return;
        }
        RideRatings ratings = mode != null ? mode.Ratings : null;
        quotaText.text = ratings != null
            ? $"{ratings.DropOffsCompleted}/{ratings.DropOffQuota}"
            : "";
    }

    void UpdateRequestText() {
        if (requestText == null) {
            return;
        }
        int requests = cabin != null ? cabin.StopRequestCount : 0;
        if (requests == 0) {
            requestText.text = "";
            return;
        }
        requestText.text = requests > 1 ? $"STOP REQUESTED  x{requests}" : "STOP REQUESTED";
    }

    // Only ever offers what would actually work right now
    string BuildPrompt() {
        if (ingameMenus.pausedGame) {
            return "";
        }
        if (mode.Mode == PlayerMode.OnFoot) {
            string prompt = interactor != null ? interactor.CurrentPrompt : "";
            if (prompt == "") {
                return "";
            }
            string lines = $"{GameKeys.Label(GameKeys.interact)}   {prompt}";
            string alt = interactor.CurrentAltPrompt;
            if (alt != "") {
                lines += $"\n{GameKeys.Label(GameKeys.kickOut)}   {alt}";
            }
            return lines;
        }
        // Someone on the step outranks everything else: the bus cannot move until they are dealt with
        Passenger atDoor = cabin != null ? cabin.PassengerAtDoor : null;
        if (atDoor != null) {
            return $"{GameKeys.Label(GameKeys.interact)}   Let aboard"
                + $"\n{GameKeys.Label(GameKeys.kickOut)}   Turn away";
        }
        string text = "";
        if (mode.CanLeaveSeat) {
            text = $"{GameKeys.Label(GameKeys.leaveSeat)}   Leave seat";
        }
        if (mode.CanUseDoors) {
            string doorLine;
            if (doors.IsOpenWanted) {
                doorLine = $"{GameKeys.Label(GameKeys.doors)}   Close doors";
            }else {
                // Everyone this stop would move: the queue outside plus riders due off here
                BusStop stop = cabin != null ? cabin.CurrentStop : null;
                int moving = stop == null ? 0 : stop.WaitingCount + cabin.DropOffCountAt(stop);
                doorLine = $"{GameKeys.Label(GameKeys.doors)}   Open doors"
                    + (moving > 0 ? $"   ({moving})" : "");
            }
            text = text == "" ? doorLine : text + "\n" + doorLine;
        }
        return text;
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

    // Simple 5-point star; avoids relying on a font glyph LiberationSans may not include
    public static Sprite CreateStarSprite() {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outer = size * 0.48f;
        float inner = outer * 0.38f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                Vector2 p = new Vector2(x, y) - center;
                float angle = Mathf.Atan2(p.x, p.y); // tip faces up
                float sector = Mathf.PI * 2f / 5f;
                float local = Mathf.Repeat(angle + Mathf.PI * 2f, sector);
                float fromTip = Mathf.Min(local, sector - local);
                float t = fromTip / (sector * 0.5f);
                float edge = Mathf.Lerp(outer, inner, t);
                texture.SetPixel(x, y, p.magnitude <= edge ? Color.white : Color.clear);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
