using BusDriver.Core.Data;
using BusDriver.UI.Debug;
using BusDriver.UI.Hud;
using BusDriver.UI.Screens;
using BusDriver.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BusDriver.Editor.Builders.BuilderUtil;

namespace BusDriver.Editor.Builders {
    // Part of BuildAll step 7 (§4.15): HUD.prefab (the HUD and CCTV canvases) and Screens.prefab
    // (the ScreenRouter and every modal screen), T-M1-16. The layouts are the MVP's.
    public static class UIPrefabBuilder {
        public const string HudPath = PrefabBuilder.Folder + "/HUD.prefab";
        public const string ScreensPath = PrefabBuilder.Folder + "/Screens.prefab";
        public const string DebugOverlayPath = PrefabBuilder.Folder + "/DebugOverlay.prefab";
        public const string LoadingPath = PrefabBuilder.Folder + "/Loading.prefab";
        public const string DashPath = DashPrefabBuilder.Path;

        // Canvas order (§4.13): CCTV under the HUD, screens over both, debug on top
        public const int CctvOrder = 5;
        public const int HudOrder = 10;
        // Scare flashes and hands: over the HUD, under the fade, so a blackout covers them
        public const int ScareOrder = 12;
        // Over the HUD, under the screens, so the pause menu stays readable in a fade
        public const int FadeOrder = 15;
        public const int ScreensOrder = 20;
        // Over every screen: a load covers whatever was on screen when it started
        public const int LoadingOrder = 30;
        public const int DebugOrder = 40;

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color ScreenBackground = new Color(0.02f, 0.02f, 0.03f, 0.94f);

        public static void Build() {
            BuildHud();
            BuildScreens();
            BuildDebugOverlay();
            BuildLoading();
            DashPrefabBuilder.Build();
        }

        // The one canvas that isn't part of a scene (§4.3): GameRoot instantiates it under itself
        // so it survives the scene change in the middle of a night load. The camera clears to
        // black for the moment when the menu has gone and the bus isn't switched on yet.
        static void BuildLoading() {
            GameObject root = new GameObject("Loading");

            GameObject cameraObject = new GameObject("Blank Camera");
            cameraObject.transform.SetParent(root.transform, false);
            Camera blank = cameraObject.AddComponent<Camera>();
            blank.clearFlags = CameraClearFlags.SolidColor;
            blank.backgroundColor = Color.black;
            blank.cullingMask = 0;
            // Behind anything a scene brings, so it only ever shows when it is the only camera
            blank.depth = -100f;
            blank.enabled = false;

            Canvas canvas = UIBuild.CreateCanvas("Loading Canvas", root.transform, LoadingOrder);
            Object.DestroyImmediate(canvas.GetComponent<GraphicRaycaster>());
            RectTransform panel = UIBuild.Panel("LoadingPanel", canvas.transform);
            UIBuild.Fill("Dim", panel, Color.black, false);
            UIBuild.Label("Loading Label", panel, "LOADING", ThemeRole.Screen, TextAlignmentOptions.Center,
                Center, new Vector2(0f, 60f), new Vector2(800f, 80f));
            Slider bar = UIBuild.CreateSlider("Progress Slider", panel, Center, new Vector2(0f, -20f), new Vector2(800f, 24f), 0f, 1f);
            bar.interactable = false;
            TMP_Text percent = UIBuild.Label("Progress Text", panel, "0%", ThemeRole.Body, TextAlignmentOptions.Center,
                Center, new Vector2(0f, -80f), new Vector2(400f, 50f));

            LoadingView view = root.AddComponent<LoadingView>();
            SetRef(view, "panel", panel.gameObject);
            SetRef(view, "canvas", canvas);
            SetRef(view, "blankCamera", blank);
            SetRef(view, "bar", bar);
            SetRef(view, "percentText", percent);
            panel.gameObject.SetActive(false);
            canvas.enabled = false;

            GameObject prefab = SaveOrOverwritePrefab(root, LoadingPath);
            Object.DestroyImmediate(root);

            // GameRoot reaches it through the one Resources asset (§4.2), and this builder runs
            // after DataSeeder has created that asset
            GameRootConfig config = AssetDatabase.LoadAssetAtPath<GameRootConfig>(GameRootConfig.AssetPath);
            if (config != null && config.loadingScreen != prefab) {
                config.loadingScreen = prefab;
                EditorUtility.SetDirty(config);
                // PrefabBuilder / BuildAll also save, but wire it now so a solo Prefabs bake sticks
                AssetDatabase.SaveAssets();
            }
        }

        // ================================================================== HUD

        static void BuildHud() {
            GameObject root = new GameObject("HUD");

            Canvas hudCanvas = UIBuild.CreateCanvas("HUD Canvas", root.transform, HudOrder);
            HudView hud = hudCanvas.gameObject.AddComponent<HudView>();
            RectTransform driverPanel = UIBuild.Panel("DriverPanel", hudCanvas.transform);
            TMP_Text speed = UIBuild.Label("SpeedText", driverPanel, "0 km/h", ThemeRole.Screen, TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(600f, 110f));
            TMP_Text gear = UIBuild.Label("GearText", driverPanel, "N", ThemeRole.Screen, TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(-60f, 150f), new Vector2(200f, 80f));

            RectTransform onFootPanel = UIBuild.Panel("OnFootPanel", hudCanvas.transform);
            GameObject crosshair = UIBuild.UIObject("Crosshair", onFootPanel);
            UIBuild.Place((RectTransform)crosshair.transform, Center, Vector2.zero, new Vector2(6f, 6f));
            Image dot = crosshair.AddComponent<Image>();
            dot.color = new Color(1f, 1f, 1f, 0.7f);
            dot.raycastTarget = false;
            onFootPanel.gameObject.SetActive(false);

            // Interaction prompts sit a little below centre so they read as tooltips over the
            // world, not as a footer. The dialogue box owns the bottom strip instead.
            TMP_Text prompt = UIBuild.Label("PromptText", hudCanvas.transform, "", ThemeRole.Hud, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(1200f, 110f));
            TMP_Text status = UIBuild.Label("StatusText", hudCanvas.transform, "", ThemeRole.Hud, TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(900f, 100f));
            UIBuild.Theme(status, ThemeRole.Hud).SetPaletteColor(ThemeColor.Highlight);

            SetRef(hud, "driverPanel", driverPanel.gameObject);
            SetRef(hud, "onFootPanel", onFootPanel.gameObject);
            SetRef(hud, "promptText", prompt);
            SetRef(hud, "statusText", status);
            SetRef(hud, "speedText", speed);
            SetRef(hud, "gearText", gear);

            BuildDialogue(hudCanvas.transform);

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

            BuildScareOverlay(root.transform);

            Canvas fadeCanvas = UIBuild.CreateCanvas("Fade Canvas", root.transform, FadeOrder);
            Object.DestroyImmediate(fadeCanvas.GetComponent<GraphicRaycaster>());
            ScreenFadeView fade = fadeCanvas.gameObject.AddComponent<ScreenFadeView>();
            Image black = UIBuild.Fill("Black", fadeCanvas.transform, Color.black, false);
            black.enabled = false;
            SetRef(fade, "image", black);
            // The fall's "YOU WENT OVER THE EDGE" over the black (T-M4-09)
            TMP_Text fadeCaption = UIBuild.Label("Caption", fadeCanvas.transform, "", ThemeRole.Title, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 160f));
            UIBuild.Theme(fadeCaption, ThemeRole.Title).SetPaletteColor(ThemeColor.Danger);
            fadeCaption.enabled = false;
            SetRef(fade, "caption", fadeCaption);

            SaveOrOverwritePrefab(root, HudPath);
            Object.DestroyImmediate(root);
        }

        // Bottom-centre strip on the HUD canvas: it belongs to the world, not to a screen, so it
        // stays up while the player drives. Starts hidden; DialogueView shows it when somebody
        // speaks. Prompts live higher (near screen centre) so the two never share a band.
        static void BuildDialogue(Transform parent) {
            // The view sits on a holder that stays active, so it can switch the box back on
            GameObject holder = UIBuild.UIObject("Dialogue", parent);
            UIBuild.Stretch((RectTransform)holder.transform);
            GameObject box = UIBuild.UIObject("DialoguePanel", holder.transform);
            RectTransform rect = (RectTransform)box.transform;
            UIBuild.Place(rect, new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(1500f, 200f));
            Image background = box.AddComponent<Image>();
            background.color = Dim;
            background.raycastTarget = false;

            TMP_Text speaker = UIBuild.Label("SpeakerText", rect, "", ThemeRole.Caption, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(40f, -24f), new Vector2(700f, 44f));
            UIBuild.Theme(speaker, ThemeRole.Caption).SetPaletteColor(ThemeColor.Highlight);
            TMP_Text line = UIBuild.Label("LineText", rect, "", ThemeRole.Hud, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(40f, -70f), new Vector2(1420f, 110f));
            // Lines are full sentences, so unlike the rest of the HUD this one wraps
            line.textWrappingMode = TextWrappingModes.Normal;

            DialogueView view = holder.AddComponent<DialogueView>();
            SetRef(view, "panel", box);
            SetRef(view, "speakerText", speaker);
            SetRef(view, "lineText", line);
            box.SetActive(false);
        }

        // The scare overlay (T-M4-05): the flash, the CCTV static and the two hands, over the HUD
        // and under the screen fade
        static void BuildScareOverlay(Transform root) {
            Canvas canvas = UIBuild.CreateCanvas("Scare Canvas", root, ScareOrder);
            Object.DestroyImmediate(canvas.GetComponent<GraphicRaycaster>());
            ScareOverlayView view = canvas.gameObject.AddComponent<ScareOverlayView>();
            RawImage staticNoise = UIBuild.Panel("Static", canvas.transform).gameObject.AddComponent<RawImage>();
            staticNoise.texture = ScareFxBuilder.StaticNoiseTexture();
            staticNoise.raycastTarget = false;
            staticNoise.enabled = false;
            RawImage overlay = UIBuild.Panel("Overlay", canvas.transform).gameObject.AddComponent<RawImage>();
            overlay.raycastTarget = false;
            overlay.enabled = false;
            RectTransform[] hands = new RectTransform[2];
            for (int i = 0; i < 2; i++) {
                Image hand = UIBuild.Fill(i == 0 ? "Hand Left" : "Hand Right", canvas.transform, new Color(0.015f, 0.012f, 0.012f, 1f), false);
                hand.sprite = UIBuild.Builtin("UISprite");
                hand.type = Image.Type.Sliced;
                RectTransform rect = hand.rectTransform;
                UIBuild.Place(rect, Center, Vector2.zero, new Vector2(1050f, 1500f));
                rect.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -28f : 28f);
                hand.gameObject.SetActive(false);
                hands[i] = rect;
            }
            SetRef(view, "overlay", overlay);
            SetRef(view, "staticNoise", staticNoise);
            SetRef(view, "handLeft", hands[0]);
            SetRef(view, "handRight", hands[1]);
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
            BuildSummary(canvas.transform, router);
            BuildGameOver(canvas.transform, router);
            IntroCardScreen intro = BuildIntro(canvas.transform);
            // The intro card and the pause screen are drawn under the screens the pause screen opens
            intro.transform.SetAsFirstSibling();
            pause.transform.SetAsFirstSibling();
            SetRef(router, "pauseScreen", pause);

            SaveOrOverwritePrefab(canvas.gameObject, ScreensPath);
            Object.DestroyImmediate(canvas.gameObject);
        }

        // ======================================================= debug overlay

        // T-M1-18 (§4.18): a dim left column with the section readouts and a column of cheat buttons
        // cloned from a template. It is in every build's scene; release builds remove it on Awake.
        static void BuildDebugOverlay() {
            Canvas canvas = UIBuild.CreateCanvas("DebugOverlay", null, DebugOrder);
            DebugOverlay overlay = canvas.gameObject.AddComponent<DebugOverlay>();
            RectTransform panel = UIBuild.Panel("Panel", canvas.transform);
            RectTransform column = UIBuild.Fill("Readouts", panel, new Color(0f, 0f, 0f, 0.7f), false).rectTransform;
            UIBuild.Place(column, new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(760f, 1040f));
            TMP_Text body = UIBuild.Label("Body", column, "", ThemeRole.Caption, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(728f, 1016f));
            body.textWrappingMode = TextWrappingModes.Normal;
            body.richText = true;

            RectTransform cheats = (RectTransform)UIBuild.UIObject("Cheats", panel).transform;
            UIBuild.Place(cheats, new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(420f, 1040f));
            VerticalLayoutGroup layout = cheats.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Button template = UIBuild.CreateButton("Cheat Template", cheats, "CHEAT", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(420f, 40f));
            UIBuild.Theme(template.GetComponentInChildren<TMP_Text>(), ThemeRole.Caption);

            panel.gameObject.SetActive(false);
            SetRef(overlay, "panel", panel.gameObject);
            SetRef(overlay, "body", body);
            SetRef(overlay, "cheatList", cheats);
            SetRef(overlay, "cheatTemplate", template);
            SaveOrOverwritePrefab(canvas.gameObject, DebugOverlayPath);
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
            // A dropdown shows its caption in the 32pt Body role, which needs more height than a
            // slider's bar does; the rows are 80 apart, so this still leaves a gap between them
            Vector2 list = new Vector2(420f, 60f);
            Slider sens = Row(root, "Mouse Sensitivity", 250f, p => UIBuild.CreateSlider("Sensitivity Slider", root, Center, p, control, 10f, 200f));
            Slider volume = Row(root, "Master Volume", 170f, p => UIBuild.CreateSlider("Volume Slider", root, Center, p, control, 0f, 1f));
            Slider brightness = Row(root, "Brightness", 90f, p => UIBuild.CreateSlider("Brightness Slider", root, Center, p, control, 0f, 1f));
            TMP_Dropdown quality = Row(root, "Quality", 10f, p => UIBuild.CreateDropdown("Quality Dropdown", root, Center, p, list));
            TMP_Dropdown resolution = Row(root, "Resolution", -70f, p => UIBuild.CreateDropdown("Resolution Dropdown", root, Center, p, list));
            Toggle fullscreen = Row(root, "Fullscreen", -150f, p => UIBuild.CreateToggle("Fullscreen Toggle", root, Center, p - new Vector2(190f, 0f)));
            TMP_Dropdown fps = Row(root, "Target FPS", -230f, p => UIBuild.CreateDropdown("Target FPS Dropdown", root, Center, p, list));

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
            UIBuild.Label(ControlsScreen.LabelTemplateName, root, "ACTION", ThemeRole.Body, TextAlignmentOptions.Right, Center, Vector2.zero, new Vector2(380f, 44f));
            Button rowTemplate = UIBuild.CreateButton(ControlsScreen.ButtonTemplateName, root, "KEY", Center, Vector2.zero, new Vector2(240f, 44f));
            // A binding row reads as a table, not as a menu button, and the 48pt Button role
            // overflows a row either way; the 32pt Body role fits the ones ControlsScreen clones
            UIBuild.Theme(rowTemplate.GetComponentInChildren<TMP_Text>(true), ThemeRole.Body);
            UIBuild.CreateButton(ControlsScreen.ResetButtonName, root, UIText.ResetAll, Center, new Vector2(0f, -110f), new Vector2(440f, 84f));
            UIBuild.CreateButton(ControlsScreen.BackButtonName, root, UIText.Back, Center, new Vector2(0f, -200f), new Vector2(320f, 84f));
            SetRef(controls, "router", router);
            return controls;
        }

        // Black, with the three centred lines of §2.21; an overlay, not a ScreenView (IntroCardScreen)
        static IntroCardScreen BuildIntro(Transform parent) {
            RectTransform root = UIBuild.Panel("IntroCard", parent);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            IntroCardScreen intro = root.gameObject.AddComponent<IntroCardScreen>();
            UIBuild.Fill("Background", root, Color.black, false);
            TMP_Text night = UIBuild.Label("NightText", root, "NIGHT 1", ThemeRole.Title, TextAlignmentOptions.Center, Center, new Vector2(0f, 90f), new Vector2(1220f, 160f));
            TMP_Text clock = UIBuild.Label("ClockText", root, "12:30 AM", ThemeRole.Screen, TextAlignmentOptions.Center, Center, new Vector2(0f, -20f), new Vector2(900f, 80f));
            TMP_Text route = UIBuild.Label("RouteText", root, UIText.RouteName, ThemeRole.Body, TextAlignmentOptions.Center, Center, new Vector2(0f, -100f), new Vector2(900f, 60f));
            SetRef(intro, "group", group);
            SetRef(intro, "nightText", night);
            SetRef(intro, "clockText", clock);
            SetRef(intro, "routeText", route);
            return intro;
        }

        // §2.21 (T-M3-06): the headline over two halves, the ledger and wallet on the left and the
        // arrivals table on the right, with the counts and Continue underneath
        static SummaryScreen BuildSummary(Transform parent, ScreenRouter router) {
            SummaryScreen summary;
            RectTransform root = ScreenRoot("SummaryScreen", parent, ScreenBackground, out summary);
            TMP_Text title = UIBuild.Label("Title", root, "NIGHT 1 COMPLETE", ThemeRole.Title, TextAlignmentOptions.Center, Center, new Vector2(0f, 420f), new Vector2(1600f, 160f));
            // Left: x −840..−140; right: x −100..920 (the stop names need the room)
            TMP_Text ledgerLabels = Column("Ledger Labels", root, ThemeRole.Body, TextAlignmentOptions.TopLeft, -600f, 480f);
            TMP_Text ledgerAmounts = Column("Ledger Amounts", root, ThemeRole.Body, TextAlignmentOptions.TopRight, -250f, 220f);
            TMP_Text wallet = UIBuild.Label("Wallet", root, "", ThemeRole.Body, TextAlignmentOptions.Left, Center, new Vector2(-490f, -90f), new Vector2(700f, 50f));
            UIBuild.Theme(wallet, ThemeRole.Body).SetPaletteColor(ThemeColor.Highlight);
            TMP_Text stops = Column("Arrival Stops", root, ThemeRole.Body, TextAlignmentOptions.TopLeft, 125f, 450f);
            TMP_Text scheduled = Column("Arrival Scheduled", root, ThemeRole.Body, TextAlignmentOptions.TopRight, 450f, 200f);
            TMP_Text actual = Column("Arrival Actual", root, ThemeRole.Body, TextAlignmentOptions.TopRight, 825f, 180f);
            TMP_Text counts = UIBuild.Label("Counts", root, "", ThemeRole.Body, TextAlignmentOptions.Center, Center, new Vector2(0f, -250f), new Vector2(1700f, 50f));
            Button next = UIBuild.CreateButton("Continue Button", root, UIText.Continue, Center, new Vector2(0f, -380f), new Vector2(380f, 80f));
            SetRef(summary, "firstSelected", next);
            SetRef(summary, "router", router);
            SetRef(summary, "titleText", title);
            SetRef(summary, "ledgerLabels", ledgerLabels);
            SetRef(summary, "ledgerAmounts", ledgerAmounts);
            SetRef(summary, "walletText", wallet);
            SetRef(summary, "arrivalStops", stops);
            SetRef(summary, "arrivalScheduled", scheduled);
            SetRef(summary, "arrivalActual", actual);
            SetRef(summary, "countsText", counts);
            SetRef(summary, "continueButton", next);
            return summary;
        }

        // §2.21 (T-M4-06): the cause over its hint, the run's totals, New Run and Main Menu
        static GameOverScreen BuildGameOver(Transform parent, ScreenRouter router) {
            GameOverScreen screen;
            RectTransform root = ScreenRoot("GameOverScreen", parent, ScreenBackground, out screen);
            TMP_Text cause = UIBuild.Label("Cause", root, UIText.GameOverTitle, ThemeRole.Title, TextAlignmentOptions.Center, Center, new Vector2(0f, 300f), new Vector2(1700f, 160f));
            UIBuild.Theme(cause, ThemeRole.Title).SetPaletteColor(ThemeColor.Danger);
            TMP_Text hint = UIBuild.Label("Hint", root, "", ThemeRole.Body, TextAlignmentOptions.Center, Center, new Vector2(0f, 170f), new Vector2(1400f, 80f));
            hint.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text stats = UIBuild.Label("Stats", root, "", ThemeRole.Body, TextAlignmentOptions.Center, Center, new Vector2(0f, -20f), new Vector2(900f, 220f));
            Button newRun = UIBuild.CreateButton("New Run Button", root, UIText.NewRun, Center, new Vector2(0f, -240f), new Vector2(420f, 80f));
            Button mainMenu = UIBuild.CreateButton("Main Menu Button", root, UIText.MainMenu, Center, new Vector2(0f, -340f), new Vector2(420f, 80f));
            SetRef(screen, "firstSelected", newRun);
            SetRef(screen, "router", router);
            SetRef(screen, "causeText", cause);
            SetRef(screen, "hintText", hint);
            SetRef(screen, "statsText", stats);
            SetRef(screen, "newRunButton", newRun);
            SetRef(screen, "mainMenuButton", mainMenu);
            return screen;
        }

        // A top-aligned, unwrapped text column of the Summary, centred at x
        static TMP_Text Column(string name, Transform root, ThemeRole role, TextAlignmentOptions alignment, float x, float width) {
            TMP_Text text = UIBuild.Label(name, root, "", role, alignment, Center, new Vector2(x, 130f), new Vector2(width, 380f));
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
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
