using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Input;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.World;

namespace BusDriver.UI.Hud {
    public class DrivingHUD : MonoBehaviour, IGameBindable {
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
        [SerializeField] TMP_Text statusText;

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
        GameServices game;
        // Prompt keys come from the bindings (controller-ready rule 3, §4.10), cached until a rebind
        string interactKey = "";
        string leaveSeatKey = "";
        string doorsKey = "";

        // From the scene root, before OnEnable of anything activated later and before Start
        public void Bind(GameServices services) {
            game = services;
            game.Pause.OnPauseChanged += HandlePauseChanged;
            game.Input.OnBindingsChanged += RefreshControlsHint;
            RefreshControlsHint();
        }

        void OnDestroy() {
            if (game != null) {
                game.Pause.OnPauseChanged -= HandlePauseChanged;
                game.Input.OnBindingsChanged -= RefreshControlsHint;
            }
        }

        // Rebinds happen on the pause screen, so the hint is rebuilt on the way out
        void HandlePauseChanged(bool paused) {
            RefreshControlsHint();
        }

        void Awake() {
            clock = clockStart;
            if (scanlines != null) {
                scanlines.texture = CreateScanlineTexture();
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

        void Start() {
            RefreshControlsHint();
        }

        public void RefreshControlsHint() {
            if (game == null) {
                return;
            }
            interactKey = Key("Interact");
            leaveSeatKey = Key("LeaveSeat");
            doorsKey = Key("Doors");
            if (controlsText == null) {
                return;
            }
            controlsText.text =
                $"{Key("Throttle")} DRIVE   {Key("Steer")} STEER   {Key("Handbrake")} HANDBRAKE   {Key("CycleCamera")} CAMERAS   {doorsKey} DOORS   {leaveSeatKey} LEAVE SEAT";
        }

        string Key(string actionId) {
            return game.Input.GetDisplayString(actionId).ToUpperInvariant();
        }

        // Tests
        internal string ControlsHint { get { return controlsText != null ? controlsText.text : ""; } }

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
            statusText.text = doors.IsClosed ? "" : "DOORS OPEN";

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
                gearText.text = bus.IsParked ? "P" : GearLabel(bus.CurrentGear);
            }
        }

        // Only ever offers what would actually work right now
        string BuildPrompt() {
            if (game != null && game.Pause.IsPaused) {
                return "";
            }
            if (mode.Mode == PlayerMode.OnFoot) {
                string prompt = interactor != null ? interactor.CurrentPrompt : "";
                return prompt == "" ? "" : $"{interactKey}   {prompt}";
            }
            string text = "";
            if (mode.CanLeaveSeat) {
                text = $"{leaveSeatKey}   Leave seat";
            }
            if (mode.CanUseDoors) {
                string doorLine;
                if (doors.IsOpenWanted) {
                    doorLine = $"{doorsKey}   Close doors";
                }else {
                    BusStop stop = cabin != null ? cabin.CurrentStop : null;
                    doorLine = $"{doorsKey}   Open doors"
                        + (stop != null && stop.WaitingCount > 0 ? $"   ({stop.WaitingCount} waiting)" : "");
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
    }
}
