using BusDriver.UI.Hud;
using BusDriver.UI.Screens;
using BusDriver.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BusDriver.Editor.Builders.BuilderUtil;

namespace BusDriver.Editor.Builders {
    // Part of BuildAll step 7 (§4.15): HUD.prefab (the HUD and CCTV canvases) and Screens.prefab
    // (the ScreenRouter and every modal screen), T-M1-16. The layouts are the MVP's.
    public static class UIPrefabBuilder {
        public const string HudPath = PrefabBuilder.Folder + "/HUD.prefab";
        public const string ScreensPath = PrefabBuilder.Folder + "/Screens.prefab";

        // Canvas order (§4.13): CCTV under the HUD, screens over both, debug on top
        public const int CctvOrder = 5;
        public const int HudOrder = 10;
        public const int ScreensOrder = 20;
        public const int GameOverOrder = 30;

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color ScreenBackground = new Color(0.02f, 0.02f, 0.03f, 0.94f);

        public static void Build() {
            BuildHud();
            BuildScreens();
        }

        // ================================================================== HUD

        static void BuildHud() {
            GameObject root = new GameObject("HUD");

            Canvas hudCanvas = UIBuild.CreateCanvas("HUD Canvas", root.transform, HudOrder);
            HudView hud = hudCanvas.gameObject.AddComponent<HudView>();
            RectTransform driverPanel = UIBuild.Panel("DriverPanel", hudCanvas.transform);
            TMP_Text speed = UIBuild.Label("SpeedText", driverPanel, "0 km/h", ThemeRole.Screen, TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(600f, 110f));
            TMP_Text gear = UIBuild.Label("GearText", driverPanel, "N", ThemeRole.Screen, TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(-60f, 150f), new Vector2(200f, 80f));
            TMP_Text controls = UIBuild.Label("ControlsText", driverPanel, "", ThemeRole.Caption, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(1500f, 40f));
            UIBuild.Theme(controls, ThemeRole.Caption).SetPaletteColor(ThemeColor.Disabled);

            RectTransform onFootPanel = UIBuild.Panel("OnFootPanel", hudCanvas.transform);
            GameObject crosshair = UIBuild.UIObject("Crosshair", onFootPanel);
            UIBuild.Place((RectTransform)crosshair.transform, Center, Vector2.zero, new Vector2(6f, 6f));
            Image dot = crosshair.AddComponent<Image>();
            dot.color = new Color(1f, 1f, 1f, 0.7f);
            dot.raycastTarget = false;
            onFootPanel.gameObject.SetActive(false);

            // Shared by the seat and on-foot views
            TMP_Text prompt = UIBuild.Label("PromptText", hudCanvas.transform, "", ThemeRole.Hud, TextAlignmentOptions.Bottom, new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(1200f, 110f));
            TMP_Text status = UIBuild.Label("StatusText", hudCanvas.transform, "", ThemeRole.Hud, TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(600f, 50f));
            UIBuild.Theme(status, ThemeRole.Hud).SetPaletteColor(ThemeColor.Highlight);

            SetRef(hud, "driverPanel", driverPanel.gameObject);
            SetRef(hud, "onFootPanel", onFootPanel.gameObject);
            SetRef(hud, "promptText", prompt);
            SetRef(hud, "statusText", status);
            SetRef(hud, "speedText", speed);
            SetRef(hud, "gearText", gear);
            SetRef(hud, "controlsText", controls);

            Canvas cctvCanvas = UIBuild.CreateCanvas("CCTV Canvas", root.transform, CctvOrder);
            // Nothing to click on a camera feed
            Object.DestroyImmediate(cctvCanvas.GetComponent<GraphicRaycaster>());
            CctvOverlayView overlay = cctvCanvas.gameObject.AddComponent<CctvOverlayView>();
            RectTransform cctvPanel = UIBuild.Panel("CCTVPanel", cctvCanvas.transform);
            RawImage scanlines = UIBuild.Panel("Scanlines", cctvPanel).gameObject.AddComponent<RawImage>();
            scanlines.raycastTarget = false;
            TMP_Text camLabel = UIBuild.Label("CamLabel", cctvPanel, "CAM 1", ThemeRole.Screen, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(60f, -50f), new Vector2(900f, 70f));
            TMP_Text rec = UIBuild.Label("RecText", cctvPanel, "REC", ThemeRole.Screen, TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(-60f, -50f), new Vector2(300f, 70f));
            UIBuild.Theme(rec, ThemeRole.Screen).SetPaletteColor(ThemeColor.Danger);
            TMP_Text timestamp = UIBuild.Label("Timestamp", cctvPanel, "02:13:00 AM", ThemeRole.Screen, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(700f, 60f));
            SetRef(overlay, "panel", cctvPanel.gameObject);
            SetRef(overlay, "camLabelText", camLabel);
            SetRef(overlay, "timestampText", timestamp);
            SetRef(overlay, "recText", rec);
            SetRef(overlay, "scanlines", scanlines);

            SaveOrOverwritePrefab(root, HudPath);
            Object.DestroyImmediate(root);
        }

        // ============================================================== screens

        static void BuildScreens() {
            Canvas canvas = UIBuild.CreateCanvas("Screens", null, ScreensOrder);
            ScreenRouter router = canvas.gameObject.AddComponent<ScreenRouter>();

            OptionsScreen options;
            ControlsScreen controls = BuildControls(canvas.transform, router);
            options = BuildOptions(canvas.transform, router, controls);
            PauseScreen pause = BuildPause(canvas.transform, router, options, controls);
            BuildConfirm(canvas.transform);
            // The pause screen is drawn under the others it opens
            pause.transform.SetAsFirstSibling();
            SetRef(router, "pauseScreen", pause);

            SaveOrOverwritePrefab(canvas.gameObject, ScreensPath);
            Object.DestroyImmediate(canvas.gameObject);
        }

        static RectTransform ScreenRoot<T>(string name, Transform parent, Color background, out T view) where T : ScreenView {
            RectTransform rect = UIBuild.Panel(name, parent);
            view = rect.gameObject.AddComponent<T>();
            UIBuild.Fill("Background", rect, background, true);
            rect.gameObject.SetActive(false);
            return rect;
        }

        static PauseScreen BuildPause(Transform parent, ScreenRouter router, OptionsScreen options, ControlsScreen controls) {
            PauseScreen pause;
            RectTransform root = ScreenRoot("PauseScreen", parent, Dim, out pause);
            UIBuild.Label("Title", root, UIText.Paused, ThemeRole.Title, TextAlignmentOptions.Center, Center, new Vector2(0f, 330f), new Vector2(1220f, 200f));
            Button resume = MenuButton("Resume Button", root, UIText.Resume, 150f);
            Button optionsButton = MenuButton("Options Button", root, UIText.Options, 50f);
            Button controlsButton = MenuButton("Controls Button", root, UIText.Controls, -50f);
            Button quitToMenu = MenuButton("Quit To Menu Button", root, UIText.QuitToMenu, -150f);
            Button quitGame = MenuButton("Quit Game Button", root, UIText.QuitGame, -250f);
            SetRef(pause, "firstSelected", resume);
            SetRef(pause, "router", router);
            SetRef(pause, "options", options);
            SetRef(pause, "controls", controls);
            SetRef(pause, "resumeButton", resume);
            SetRef(pause, "optionsButton", optionsButton);
            SetRef(pause, "controlsButton", controlsButton);
            SetRef(pause, "quitToMenuButton", quitToMenu);
            SetRef(pause, "quitGameButton", quitGame);
            return pause;
        }

        static Button MenuButton(string name, Transform parent, string text, float y) {
            return UIBuild.CreateButton(name, parent, text, Center, new Vector2(0f, y), new Vector2(520f, 80f));
        }

        static OptionsScreen BuildOptions(Transform parent, ScreenRouter router, ControlsScreen controls) {
            OptionsScreen options;
            RectTransform root = ScreenRoot("OptionsScreen", parent, ScreenBackground, out options);
            UIBuild.Label("Title", root, UIText.Options, ThemeRole.Title, TextAlignmentOptions.Center, Center, new Vector2(0f, 400f), new Vector2(1220f, 160f));

            Vector2 control = new Vector2(420f, 44f);
            Slider sens = Row(root, "Mouse Sensitivity", 250f, p => UIBuild.CreateSlider("Sensitivity Slider", root, Center, p, control, 10f, 200f));
            Slider volume = Row(root, "Master Volume", 170f, p => UIBuild.CreateSlider("Volume Slider", root, Center, p, control, 0f, 1f));
            Slider brightness = Row(root, "Brightness", 90f, p => UIBuild.CreateSlider("Brightness Slider", root, Center, p, control, 0f, 1f));
            TMP_Dropdown quality = Row(root, "Quality", 10f, p => UIBuild.CreateDropdown("Quality Dropdown", root, Center, p, control));
            TMP_Dropdown resolution = Row(root, "Resolution", -70f, p => UIBuild.CreateDropdown("Resolution Dropdown", root, Center, p, control));
            Toggle fullscreen = Row(root, "Fullscreen", -150f, p => UIBuild.CreateToggle("Fullscreen Toggle", root, Center, p - new Vector2(190f, 0f)));
            TMP_Dropdown fps = Row(root, "Target FPS", -230f, p => UIBuild.CreateDropdown("Target FPS Dropdown", root, Center, p, control));

            Button apply = UIBuild.CreateButton("Apply Button", root, UIText.Apply, Center, new Vector2(-300f, -380f), new Vector2(260f, 70f));
            Button controlsButton = UIBuild.CreateButton("Controls Button", root, UIText.Controls, Center, new Vector2(0f, -380f), new Vector2(300f, 70f));
            Button back = UIBuild.CreateButton("Back Button", root, UIText.Back, Center, new Vector2(300f, -380f), new Vector2(260f, 70f));

            SetRef(options, "firstSelected", sens);
            SetRef(options, "router", router);
            SetRef(options, "controls", controls);
            SetRef(options, "sensSlider", sens);
            SetRef(options, "volumeSlider", volume);
            SetRef(options, "brightnessSlider", brightness);
            SetRef(options, "qualityDropdown", quality);
            SetRef(options, "resolutionDropdown", resolution);
            SetRef(options, "fullScreenToggle", fullscreen);
            SetRef(options, "targetFPSDropdown", fps);
            SetRef(options, "applyButton", apply);
            SetRef(options, "controlsButton", controlsButton);
            SetRef(options, "backButton", back);
            return options;
        }

        // A right-aligned label left of centre, and the control right of centre
        static T Row<T>(Transform root, string label, float y, System.Func<Vector2, T> control) {
            UIBuild.Label(label + " Label", root, label.ToUpperInvariant(), ThemeRole.Body, TextAlignmentOptions.Right, Center, new Vector2(-260f, y), new Vector2(460f, 44f));
            return control(new Vector2(240f, y));
        }

        static ControlsScreen BuildControls(Transform parent, ScreenRouter router) {
            ControlsScreen controls;
            RectTransform root = ScreenRoot("ControlsScreen", parent, ScreenBackground, out controls);
            UIBuild.Label("Title", root, UIText.Controls, ThemeRole.Title, TextAlignmentOptions.Center, Center, new Vector2(0f, 460f), new Vector2(1220f, 140f));
            // The template row the screen clones for every binding (ControlsScreen.Build)
            UIBuild.Label(ControlsScreen.LabelTemplateName, root, "ACTION", ThemeRole.Body, TextAlignmentOptions.Right, Center, Vector2.zero, new Vector2(320f, 30f));
            UIBuild.CreateButton(ControlsScreen.ButtonTemplateName, root, "KEY", Center, Vector2.zero, new Vector2(180f, 30f));
            UIBuild.CreateButton(ControlsScreen.ResetButtonName, root, UIText.ResetAll, Center, new Vector2(0f, -110f), new Vector2(320f, 60f));
            UIBuild.CreateButton(ControlsScreen.BackButtonName, root, UIText.Back, Center, new Vector2(0f, -200f), new Vector2(260f, 60f));
            SetRef(controls, "router", router);
            return controls;
        }

        static void BuildConfirm(Transform parent) {
            ConfirmDialog dialog;
            RectTransform root = ScreenRoot("ConfirmDialog", parent, Dim, out dialog);
            RectTransform box = UIBuild.Fill("Box", root, ScreenBackground, true).rectTransform;
            UIBuild.Place(box, Center, Vector2.zero, new Vector2(900f, 360f));
            TMP_Text message = UIBuild.Label("Message", box, UIText.LeaveEndsRun, ThemeRole.Body, TextAlignmentOptions.Center, Center, new Vector2(0f, 70f), new Vector2(820f, 120f));
            message.textWrappingMode = TextWrappingModes.Normal;
            Button confirm = UIBuild.CreateButton("Confirm Button", box, UIText.Leave, Center, new Vector2(-180f, -100f), new Vector2(260f, 70f));
            Button cancel = UIBuild.CreateButton("Cancel Button", box, UIText.Stay, Center, new Vector2(180f, -100f), new Vector2(260f, 70f));
            SetRef(dialog, "firstSelected", cancel);
            SetRef(dialog, "message", message);
            SetRef(dialog, "confirmButton", confirm);
            SetRef(dialog, "cancelButton", cancel);
            SetRef(dialog, "confirmLabel", confirm.GetComponentInChildren<TMP_Text>());
            SetRef(dialog, "cancelLabel", cancel.GetComponentInChildren<TMP_Text>());
        }
    }
}
