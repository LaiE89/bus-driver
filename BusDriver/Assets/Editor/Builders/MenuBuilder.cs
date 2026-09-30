using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Views;
using BusDriver.Gameplay.World;
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
    // BuildAll step 10 (§4.15): Generated/Scenes/Menu.unity. v1 (T-M2-15): a live diorama built from
    // the game's own generated pieces (§2.22, D17): a 60 m road piece with a guardrail on the near
    // side, the trailhead stop and its flickering, buzzing lamp across the road, forest trees, one
    // waiting figure, the night lighting preset and a fixed camera with a slow drift. Over it, on the
    // dark left of the frame with no panel: the title, New Run / Options / Controls / Quit, the build
    // label and the loading bar, the MenuContext and the Screens prefab for Options and Controls.
    // The full menu (Continue, Journal, Credits, idle events, the arrival sequence) is M8.
    public static class MenuBuilder {
        public const string ScenePath = SceneIds.GeneratedFolder + "/" + SceneIds.Menu + ".unity";

        static readonly Color Background = new Color(0.015f, 0.015f, 0.02f, 1f);
        public const string MeshFolder = GeneratedRoot + "/Meshes/Menu";
        public const string FigureName = "Waiting Figure";
        public const string StopName = "Stop trailhead";

        // The diorama's layout, in the road piece's frame (+Z along the road, +X toward the stop's kerb)
        const float RoadLength = 60f;
        const float StopDistance = 32f;
        static readonly Vector3 CameraPosition = new Vector3(-8.2f, 1.7f, 23.5f);
        static readonly Vector3 CameraTarget = new Vector3(-0.8f, 1.5f, 33.5f);
        const float CameraFov = 55f;

        [MenuItem("Tools/Bus Driver/Builders/Menu")]
        public static void Build() {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // The night scene's own preset (§2.22)
            LightingBuild.CreateApplier(null);

            GameObject contextObject = new GameObject("Menu Context");
            MenuContext context = contextObject.AddComponent<MenuContext>();
            SceneAmbience ambience = contextObject.AddComponent<SceneAmbience>();
            SetStringArray(ambience, "loops", new[] { SoundIds.AmbForestNight, SoundIds.AmbWind });

            BuildDiorama();

            // The UI click and the ambience need a listener; it lives on the camera here (there's no player)
            Camera cam = new GameObject("Menu Camera").AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(CameraPosition, Quaternion.LookRotation(CameraTarget - CameraPosition, Vector3.up));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fog ? RenderSettings.fogColor : Background;
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 250f;
            cam.tag = "MainCamera";
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            cam.gameObject.AddComponent<AudioListener>();
            cam.gameObject.AddComponent<CameraDrift>();

            Canvas canvas = UIBuild.CreateCanvas("Menu Canvas", null, 0);
            MainMenu menu = canvas.gameObject.AddComponent<MainMenu>();

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

        // The road piece is a one-segment RouteDefinition built in memory from Route 1's cross-section
        // and generation numbers, so the diorama's road, guardrail and forest are exactly the game's
        static void BuildDiorama() {
            GameRootConfig config = AssetDatabase.LoadAssetAtPath<GameRootConfig>(GameRootConfig.AssetPath);
            RouteDefinition route1 = config != null ? config.Route(RouteSeed.RouteId) : null;
            if (route1 == null) {
                throw new System.InvalidOperationException("no route '" + RouteSeed.RouteId + "' in GameRootConfig; DataSeeder runs before MenuBuilder");
            }
            RouteDefinition piece = ScriptableObject.CreateInstance<RouteDefinition>();
            piece.id = "menu_diorama";
            piece.roadWidth = route1.roadWidth;
            piece.shoulderWidth = route1.shoulderWidth;
            piece.cliffRoadWidth = route1.cliffRoadWidth;
            piece.cliffOuterShoulder = route1.cliffOuterShoulder;
            piece.generation = route1.generation;
            piece.schedule = route1.schedule;
            // The guardrail on the camera's side, the forest behind the stop
            piece.segments = new[] { RouteSegment.Straight(RoadLength, 0f, SideProfile.Drop, SideProfile.Forest) };
            RoutePath path = new RoutePath(piece);

            EnsureFolder(MeshFolder);
            Transform world = Group("Diorama", null).transform;
            RoadMeshBuilder.Build(piece, path, Group("Road", world).transform, MeshFolder);
            ProfileBuildResult profiles = ProfileBuilder.Build(piece, path, Group("Profiles", world).transform, new List<RouteOpening>(), MeshFolder);
            Transform dressing = Group("Dressing", world).transform;
            foreach (DressingSpot spot in profiles.Trees) {
                GameObject tree = EnvironmentPrefabBuilder.Place(EnvironmentKinds.Tree(spot.Variant), dressing, spot.Position, Quaternion.Euler(0f, spot.YawDeg, 0f));
                tree.transform.localScale = Vector3.one * spot.Scale;
            }
            float segment = piece.generation.guardrailSegmentLength;
            foreach (RailSpot spot in profiles.Guardrails) {
                GameObject rail = EnvironmentPrefabBuilder.Place(EnvironmentKinds.GuardrailSegment, dressing, spot.Position, spot.Rotation);
                if (spot.Length < segment - 0.01f) {
                    rail.transform.localScale = new Vector3(1f, 1f, spot.Length / segment);
                }
            }
            foreach (RailSpot spot in profiles.EndCaps) {
                EnvironmentPrefabBuilder.Place(EnvironmentKinds.GuardrailEndCap, dressing, spot.Position, spot.Rotation);
            }
            Object.DestroyImmediate(piece);

            // The trailhead stop (§2.22), its lamp humming with its flicker
            RoutePose pose = path.Evaluate(StopDistance);
            GameObject stop = EnvironmentPrefabBuilder.Place(EnvironmentKinds.Stop(StopKind.Trailhead), world, pose.Position, pose.Rotation, 1);
            stop.name = StopName;
            LightFlicker flicker = stop.GetComponentInChildren<LightFlicker>(true);
            if (flicker != null) {
                LampBuzz buzz = flicker.gameObject.AddComponent<LampBuzz>();
                SetRef(buzz, "flicker", flicker);
            }
            BuildFigure(stop.transform, stop.GetComponent<BusStop>());
        }

        // The look the menu's waiting figure wears: the tall one in the long dark coat
        public const string FigureLookId = "look06";

        // One waiting passenger, view only (§2.22): the generated GreyboxPassengerView in a look,
        // with no logic, standing where the stop's first rider waits and facing the road
        static void BuildFigure(Transform stop, BusStop busStop) {
            Vector3 wait = busStop != null ? busStop.WaitLocalPosition(0) : new Vector3(5.2f, 0f, -1.2f);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBuilder.GreyboxPassengerViewPath);
            if (prefab == null) {
                throw new System.InvalidOperationException("no greybox passenger view; PrefabBuilder runs before MenuBuilder");
            }
            GameObject figure = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stop);
            figure.name = FigureName;
            figure.transform.localPosition = wait;
            figure.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            GreyboxPassengerView view = figure.GetComponent<GreyboxPassengerView>();
            GameRootConfig config = AssetDatabase.LoadAssetAtPath<GameRootConfig>(GameRootConfig.AssetPath);
            view.Configure(config != null ? config.Look(FigureLookId) : null);
            view.SetPose(PassengerPose.Standing);
            ViewFactory.AttachFlicker(view, 1);
            EditorUtility.SetDirty(view);
        }

        static Button MenuButton(string name, Transform parent, string text, float y) {
            Button button = UIBuild.CreateButton(name, parent, text, new Vector2(0f, 0.5f), new Vector2(140f, y), new Vector2(420f, 76f));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.Left;
            return button;
        }
    }
}
