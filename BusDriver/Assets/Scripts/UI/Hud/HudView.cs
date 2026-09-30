using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.World;
using TMPro;
using UnityEngine;

namespace BusDriver.UI.Hud {
    // The HUD canvas (§4.13, was half of DrivingHUD): speed, gear and the controls line while
    // driving, the crosshair on foot, and the prompt and status lines for both. The CCTV feed's own
    // overlay is CctvOverlayView. Prompt keys come from the bindings (controller-ready rule 3).
    public sealed class HudView : MonoBehaviour, IShiftBindable {
        [Header("Panels")]
        [SerializeField] GameObject driverPanel;
        [SerializeField] GameObject onFootPanel;

        [Header("Texts")]
        [SerializeField] TMP_Text promptText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text speedText;
        [SerializeField] TMP_Text gearText;
        [SerializeField] TMP_Text controlsText;

        GameServices game;
        BusController bus;
        BusDoors doors;
        BusCabin cabin;
        CCTVSystem cctv;
        PlayerModeController mode;
        PlayerInteractor interactor;
        // Cached until a rebind
        string interactKey = "";
        string leaveSeatKey = "";
        string doorsKey = "";

        // Tests
        internal string ControlsHint { get { return controlsText != null ? controlsText.text : ""; } }

        public void Bind(ShiftServices shift) {
            game = shift.Game;
            bus = shift.Bus;
            doors = shift.Doors;
            cabin = shift.Cabin;
            cctv = shift.Cctv;
            mode = shift.Mode;
            interactor = shift.Interactor;
            game.Input.OnBindingsChanged += RefreshControlsHint;
            cctv.OnViewChanged += HandleViewChanged;
            mode.OnModeChanged += HandleModeChanged;
            RefreshControlsHint();
            RefreshPanels();
        }

        void OnDestroy() {
            if (game != null) {
                game.Input.OnBindingsChanged -= RefreshControlsHint;
            }
            if (cctv != null) {
                cctv.OnViewChanged -= HandleViewChanged;
            }
            if (mode != null) {
                mode.OnModeChanged -= HandleModeChanged;
            }
        }

        void HandleViewChanged(int index) {
            RefreshPanels();
        }

        void HandleModeChanged(PlayerMode newMode) {
            RefreshPanels();
        }

        void RefreshPanels() {
            bool onFoot = mode.Mode == PlayerMode.OnFoot;
            driverPanel.SetActive(!cctv.IsViewingCCTV && !onFoot);
            onFootPanel.SetActive(onFoot);
        }

        public void RefreshControlsHint() {
            if (game == null) {
                return;
            }
            interactKey = Key("Interact");
            leaveSeatKey = Key("LeaveSeat");
            doorsKey = Key("Doors");
            controlsText.text =
                $"{Key("Throttle")} DRIVE   {Key("Steer")} STEER   {Key("Handbrake")} HANDBRAKE   {Key("CycleCamera")} CAMERAS   {doorsKey} DOORS   {leaveSeatKey} LEAVE SEAT";
        }

        string Key(string actionId) {
            return game.Input.GetDisplayString(actionId).ToUpperInvariant();
        }

        void Update() {
            if (bus == null) {
                return;
            }
            promptText.text = BuildPrompt();
            statusText.text = doors.IsClosed ? "" : "DOORS OPEN";
            if (driverPanel.activeSelf) {
                speedText.text = $"{Mathf.RoundToInt(bus.SpeedKmh)} km/h";
                gearText.text = bus.IsParked ? "P" : GearLabel(bus.CurrentGear);
            }
        }

        // Only ever offers what would actually work right now
        string BuildPrompt() {
            if (game.Pause.IsPaused) {
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

        static string GearLabel(BusController.Gear gear) {
            switch (gear) {
                case BusController.Gear.Drive:
                    return "D";
                case BusController.Gear.Reverse:
                    return "R";
                default:
                    return "N";
            }
        }
    }
}
