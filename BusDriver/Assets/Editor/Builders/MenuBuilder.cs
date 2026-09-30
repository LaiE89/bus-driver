using BusDriver.Core.Util;
using BusDriver.UI.Menu;
using BusDriver.UI.Screens;
using BusDriver.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static BusDriver.Editor.Builders.BuilderUtil;

namespace BusDriver.Editor.Builders {
    // BuildAll step 10 (§4.15): Generated/Scenes/Menu.unity, v0 (T-M1-16): a dark screen with the
    // title, New Run / Options / Controls / Quit, the build label and the loading bar, the
    // MenuContext and the Screens prefab for Options and Controls. The diorama arrives in T-M2-15
    // and the full menu in T-M8-01.
    public static class MenuBuilder {
        public const string ScenePath = SceneIds.GeneratedFolder + "/" + SceneIds.Menu + ".unity";

        static readonly Color Background = new Color(0.015f, 0.015f, 0.02f, 1f);

        [MenuItem("Tools/Bus Driver/Builders/Menu")]
        public static void Build() {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RouteBuilder.ApplyNightRenderSettings();

            GameObject contextObject = new GameObject("Menu Context");
            MenuContext context = contextObject.AddComponent<MenuContext>();

            // The UI click needs a listener; it lives on the camera here (there's no player)
            Camera cam = new GameObject("Menu Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            cam.gameObject.AddComponent<AudioListener>();

            Canvas canvas = UIBuild.CreateCanvas("Menu Canvas", null, 0);
            MainMenu menu = canvas.gameObject.AddComponent<MainMenu>();
            UIBuild.Fill("Background", canvas.transform, Background, false);

            // Left-aligned on the dark side of the frame (§2.22)
            Vector2 left = new Vector2(0f, 0.5f);
            UIBuild.Label("Title", canvas.transform, "BUS DRIVER", ThemeRole.Title, TextAlignmentOptions.Left, left, new Vector2(140f, 250f), new Vector2(1200f, 180f));
            Button newRun = MenuButton("New Run Button", canvas.transform, "NEW RUN", 60f);
            Button options = MenuButton("Options Button", canvas.transform, UIText.Options, -30f);
            Button controls = MenuButton("Controls Button", canvas.transform, UIText.Controls, -120f);
            Button quit = MenuButton("Quit Button", canvas.transform, "QUIT", -210f);

            TMP_Text label = UIBuild.Label("Build Label", canvas.transform, "0.0.0 (dev)", ThemeRole.Caption, TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(-24f, 16f), new Vector2(600f, 36f));
            UIBuild.Theme(label, ThemeRole.Caption).SetPaletteColor(ThemeColor.Disabled);
            BuildLabelView labelView = label.gameObject.AddComponent<BuildLabelView>();

            RectTransform loading = UIBuild.Panel("Loading Screen", canvas.transform);
            UIBuild.Fill("Dim", loading, Background, true);
            Vector2 center = new Vector2(0.5f, 0.5f);
            UIBuild.Label("Loading Label", loading, "LOADING", ThemeRole.Screen, TextAlignmentOptions.Center, center, new Vector2(0f, 60f), new Vector2(800f, 80f));
            Slider slider = UIBuild.CreateSlider("Progress Slider", loading, center, new Vector2(0f, -20f), new Vector2(800f, 24f), 0f, 1f);
            slider.interactable = false;
            TMP_Text progress = UIBuild.Label("Progress Text", loading, "0%", ThemeRole.Body, TextAlignmentOptions.Center, center, new Vector2(0f, -80f), new Vector2(400f, 50f));
            loading.gameObject.SetActive(false);

            GameObject screensPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UIPrefabBuilder.ScreensPath);
            GameObject screens = (GameObject)PrefabUtility.InstantiatePrefab(screensPrefab);
            screens.name = "Screens";
            UIInputModuleSetup.Configure(new GameObject("EventSystem"));

            SetRef(menu, "router", screens.GetComponent<ScreenRouter>());
            SetRef(menu, "options", screens.GetComponentInChildren<OptionsScreen>(true));
            SetRef(menu, "controls", screens.GetComponentInChildren<ControlsScreen>(true));
            SetRef(menu, "newRunButton", newRun);
            SetRef(menu, "optionsButton", options);
            SetRef(menu, "controlsButton", controls);
            SetRef(menu, "quitButton", quit);
            SetRef(menu, "loadingScreen", loading.gameObject);
            SetRef(menu, "slider", slider);
            SetRef(menu, "progressText", progress);

            SetRef(context, "buildLabel", labelView);
            SetRefArray(context, "bindables", NightSystemsBuilder.Bindables(scene).ToArray());
            SaveScene(scene, ScenePath);
        }

        static Button MenuButton(string name, Transform parent, string text, float y) {
            Button button = UIBuild.CreateButton(name, parent, text, new Vector2(0f, 0.5f), new Vector2(140f, y), new Vector2(420f, 76f));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.Left;
            return button;
        }
    }
}
