using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.World;
using TMPro;
using UnityEngine;

namespace BusDriver.UI.Hud {
    // The HUD canvas (§4.13): speed and gear while driving, the crosshair on foot, STOP REQUESTED
    // / last-stop note at the top, and the prompt for both. The CCTV feed's own overlay is
    // CctvOverlayView. Prompt keys come from the bindings (controller-ready rule 3).
    public sealed class HudView : MonoBehaviour, IShiftBindable {
        [Header("Panels")]
        [SerializeField] GameObject driverPanel;
        [SerializeField] GameObject onFootPanel;

        [Header("Texts")]
        [SerializeField] TMP_Text promptText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text speedText;
        [SerializeField] TMP_Text gearText;

        GameServices game;
        BusController bus;
        BusDoors doors;
        BusCabin cabin;
        RouteProgress progress;
        CCTVSystem cctv;
        PlayerModeController mode;
        PlayerInteractor interactor;
        // Cached until a rebind
        string interactKey = "";
        string kickKey = "";
        string leaveSeatKey = "";
        string doorsKey = "";
        string acceptRiderKey = "";
        string refuseRiderKey = "";

        public void Bind(ShiftServices shift) {
            game = shift.Game;
            bus = shift.Bus;
            doors = shift.Doors;
            cabin = shift.Cabin;
            progress = shift.Progress;
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
            kickKey = Key("Kick");
            leaveSeatKey = Key("LeaveSeat");
            doorsKey = Key("Doors");
            acceptRiderKey = Key("AcceptRider");
            refuseRiderKey = Key("RefuseRider");
        }

        string Key(string actionId) {
            return game.Input.GetDisplayString(actionId).ToUpperInvariant();
        }

        void Update() {
            if (bus == null) {
                return;
            }
            promptText.text = BuildPrompt();
            statusText.text = BuildStatus();
            if (driverPanel.activeSelf) {
                speedText.text = $"{Mathf.RoundToInt(bus.SpeedKmh)} km/h";
                gearText.text = bus.IsParked ? "P" : GearLabel(bus.CurrentGear);
            }
        }

        // Top-of-HUD status: stop requests, and a last-stop callout when the next kerb ends the night
        string BuildStatus() {
            int requests = cabin != null ? cabin.StopRequestCount : 0;
            string request = requests == 0 ? "" : (requests > 1 ? $"STOP REQUESTED  x{requests}" : "STOP REQUESTED");
            string last = "";
            if (progress != null && progress.Next != null && progress.Next.IsEndStop) {
                last = "LAST STOP, STOP HERE";
            }
            if (request != "" && last != "") {
                return request + "\n" + last;
            }
            return request != "" ? request : last;
        }

        string BuildPrompt() {
            if (game.Pause.IsPaused) {
                return "";
            }
            if (mode.Mode == PlayerMode.OnFoot) {
                string prompt = interactor != null ? interactor.CurrentPrompt : "";
                if (prompt == "") {
                    return "";
                }
                string lines = $"{interactKey}   {prompt}";
                string alt = interactor.CurrentAltPrompt;
                if (alt != "") {
                    lines += $"\n{kickKey}   {alt}";
                }
                return lines;
            }
            // Somebody on the step outranks everything else: the bus can't move until they are
            // dealt with, so the doors and the seat are no use yet (§2.4)
            if (cabin != null && cabin.PassengerAtDoor != null) {
                return $"{acceptRiderKey}   Let aboard\n{refuseRiderKey}   Turn away";
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
