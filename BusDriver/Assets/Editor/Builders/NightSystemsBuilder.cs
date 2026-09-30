using System.Collections.Generic;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Attention;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.Scares;
using BusDriver.Gameplay.Shift;
using BusDriver.Gameplay.Views;
using BusDriver.UI.Screens;
using BusDriver.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static BusDriver.Editor.Builders.BuilderUtil;

namespace BusDriver.Editor.Builders {
    // BuildAll step 9 (§4.15): Generated/Scenes/Night_Systems.unity, the night's systems (§4.3):
    // ShiftContext on the scene root, the Bus, OnFootRig, FallCamera, HUD and Screens instances,
    // the mode switch and interactor, the debug rider hook (T-M2-07), the monster, scare and death
    // systems (M4) and the EventSystem. The bus is saved inactive: this scene has no ground, so ShiftContext.AttachRoute
    // moves it to the route's spawn point and switches it on.
    public static class NightSystemsBuilder {
        public const string ScenePath = SceneIds.GeneratedFolder + "/" + SceneIds.NightSystems + ".unity";

        [MenuItem("Tools/Bus Driver/Builders/Night_Systems")]
        public static void Build() {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Dark until the route scene, whose lighting is the night's, becomes active
            LightingBuild.BakeRenderSettings();

            GameObject contextObject = new GameObject("Shift Context");
            ShiftContext context = contextObject.AddComponent<ShiftContext>();
            ShiftDirector director = contextObject.AddComponent<ShiftDirector>();
            RouteTracker tracker = contextObject.AddComponent<RouteTracker>();
            RouteProgress progress = contextObject.AddComponent<RouteProgress>();
            ShiftClockDriver clock = contextObject.AddComponent<ShiftClockDriver>();
            PassengerRegistry riders = contextObject.AddComponent<PassengerRegistry>();
            PlayerAttention attention = contextObject.AddComponent<PlayerAttention>();
            ViewFactory views = contextObject.AddComponent<ViewFactory>();
            ManifestSpawner manifest = contextObject.AddComponent<ManifestSpawner>();
            MonsterSystem monsters = contextObject.AddComponent<MonsterSystem>();
            ScarePlayer scarePlayer = contextObject.AddComponent<ScarePlayer>();
            ScareDirector scares = contextObject.AddComponent<ScareDirector>();
            DeathDirector death = contextObject.AddComponent<DeathDirector>();
            MonsterKillPresenter killPresenter = contextObject.AddComponent<MonsterKillPresenter>();
            BlackoutPresenter blackoutPresenter = contextObject.AddComponent<BlackoutPresenter>();
            SetRefArray(death, "presenters", new Object[] { killPresenter, blackoutPresenter });
            SetRef(scarePlayer, "scareHeadPrefab", ScareFxBuilder.ScareHead());
            SetRef(scarePlayer, "defaultOverlay", ScareFxBuilder.FaceOverlayTexture());
            SetRef(manifest, "riderPrefab", LoadRider(PrefabBuilder.PassengerPath));
            SetRef(views, "greyboxView", LoadGreyboxView());

            GameObject bus = Instantiate(PrefabBuilder.BusPath, "Bus");
            GameObject rig = Instantiate(PrefabBuilder.OnFootRigPath, "OnFootRig");
            Instantiate(PrefabBuilder.FallCameraPath, "FallCamera");
            Instantiate(UIPrefabBuilder.HudPath, "HUD");
            Instantiate(UIPrefabBuilder.DashPath, "Dash");
            Instantiate(UIPrefabBuilder.ScreensPath, "Screens");
            Instantiate(UIPrefabBuilder.DebugOverlayPath, "DebugOverlay");
            UIInputModuleSetup.Configure(new GameObject("EventSystem"));

            Transform head = bus.transform.Find(PrefabBuilder.DriverHeadName);
            DriverLook look = head.Find(PrefabBuilder.DriverPivotName).GetComponent<DriverLook>();
            Camera driverCamera = look.transform.Find(PrefabBuilder.DriverCameraName).GetComponent<Camera>();
            OnFootController onFoot = rig.GetComponent<OnFootController>();
            // The hull is one solid box around the whole interior (see OnFootController)
            SetRefArray(onFoot, "ignoredColliders", new Object[] { bus.GetComponent<BoxCollider>() });
            rig.SetActive(false);

            GameObject systems = new GameObject("Game Systems");
            PlayerModeController mode = systems.AddComponent<PlayerModeController>();
            PlayerInteractor interactor = systems.AddComponent<PlayerInteractor>();
            DebugRiders debugRiders = systems.AddComponent<DebugRiders>();
            AutoPilot autoPilot = systems.AddComponent<AutoPilot>();
            SetRef(mode, "busInput", bus.GetComponent<BusInput>());
            SetRef(mode, "driverLook", look);
            SetRef(mode, "cctv", bus.GetComponentInChildren<CCTVSystem>(true));
            SetRef(mode, "bus", bus.GetComponent<BusController>());
            SetRef(mode, "cabin", bus.GetComponent<BusCabin>());
            SetRef(mode, "doors", bus.GetComponent<BusDoors>());
            SetRef(mode, "onFoot", onFoot);
            SetRef(mode, "driverCamera", driverCamera);
            SetRef(mode, "onFootCamera", rig.GetComponentInChildren<Camera>(true));
            SetRef(mode, "ears", head.Find(PrefabBuilder.EarsName));
            SetRef(mode, "earsSeatParent", head);
            SetRef(mode, "driverAvatar", head.Find(PrefabBuilder.DriverAvatarName).gameObject);

            SetRef(context, "bus", bus.GetComponent<BusController>());
            SetRef(context, "mode", mode);
            SetRef(context, "onFoot", onFoot);
            SetRef(context, "interactor", interactor);
            SetRef(context, "death", death);
            SetRef(context, "director", director);
            SetRef(context, "debugRiders", debugRiders);
            SetRef(context, "tracker", tracker);
            SetRef(context, "progress", progress);
            SetRef(context, "clock", clock);
            SetRef(context, "attention", attention);
            SetRef(context, "autoPilot", autoPilot);
            SetRef(context, "riders", riders);
            SetRef(context, "views", views);
            SetRef(context, "manifest", manifest);
            SetRef(context, "monsters", monsters);
            SetRef(context, "scarePlayer", scarePlayer);
            SetRef(context, "scares", scares);
            SetRefArray(context, "bindables", Bindables(scene).ToArray());

            bus.SetActive(false);
            SaveScene(scene, ScenePath);
        }

        static GameObject Instantiate(string prefabPath, string name) {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) {
                throw new System.InvalidOperationException("no " + prefabPath + "; PrefabBuilder runs before NightSystemsBuilder");
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            return instance;
        }

        static Passenger LoadRider(string prefabPath) {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Passenger rider = prefab != null ? prefab.GetComponent<Passenger>() : null;
            if (rider == null) {
                throw new System.InvalidOperationException("no rider prefab at " + prefabPath + "; PrefabBuilder runs before NightSystemsBuilder");
            }
            return rider;
        }

        static GreyboxPassengerView LoadGreyboxView() {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBuilder.GreyboxPassengerViewPath);
            GreyboxPassengerView view = prefab != null ? prefab.GetComponent<GreyboxPassengerView>() : null;
            if (view == null) {
                throw new System.InvalidOperationException("no greybox passenger view at " + PrefabBuilder.GreyboxPassengerViewPath + "; PrefabBuilder runs before NightSystemsBuilder");
            }
            return view;
        }

        // Every component the scene root binds, in hierarchy order (D65)
        public static List<Object> Bindables(Scene scene) {
            List<Object> bindables = new List<Object>();
            foreach (GameObject root in scene.GetRootGameObjects()) {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) {
                    if (behaviour is IShiftBindable || behaviour is IGameBindable) {
                        bindables.Add(behaviour);
                    }
                }
            }
            return bindables;
        }
    }
}
