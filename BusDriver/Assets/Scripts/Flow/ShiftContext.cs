using System.Collections.Generic;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Flow {
    // The night systems scene's root (§4.3, §4.5). SceneLoader calls Initialize when the scene is
    // wired; RunFlow calls AttachRoute once the route scene is loaded, then Begin. Begin builds
    // ShiftServices and calls Init in the fixed order. Steps whose services don't exist yet are
    // skipped: they arrive with their tickets (RouteTracker T-M2-09, ShiftClockDriver T-M2-12,
    // PlayerAttention T-M4-01, …). ShiftDirector owns the drive lock and the pause predicate.
    public sealed class ShiftContext : MonoBehaviour, INightRoot {
        [SerializeField] BusController bus;
        [SerializeField] PlayerModeController mode;
        [SerializeField] OnFootController onFoot;
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] LegacyGameOver gameOver;
        [SerializeField] ShiftDirector director;
        [Tooltip("UI and game-scoped components of this scene, bound in list order (§4.5 step 15)")]
        [SerializeField] MonoBehaviour[] bindables = new MonoBehaviour[0];

        public GameServices Game { get; private set; }
        public RouteSceneRoot Route { get; private set; }
        public NightSetup Setup { get; private set; }
        public ShiftServices Shift { get; private set; }
        public bool HasBegun { get { return Shift != null; } }
        public BusController Bus { get { return bus; } }
        public ShiftDirector Director { get { return director; } }

        // Game-scoped components (the screens, the menus) get the services now, before any Start
        public void Initialize(GameServices game) {
            Game = game;
            BindGame(bindables, game);
        }

        // The bus waits inactive in Night_Systems, which has no ground, until the route is there
        // to stand on; it starts the night at the route's spawn point
        public void AttachRoute(RouteSceneRoot route) {
            Route = route;
            if (route == null) {
                return;
            }
            if (route.BusSpawn != null) {
                bus.transform.SetPositionAndRotation(route.BusSpawn.position, route.BusSpawn.rotation);
            }
            bus.gameObject.SetActive(true);
            BindGame(route.Bindables, Game);
        }

        public void Begin(NightSetup setup) {
            if (HasBegun) {
                Log.Warn(LogCat.Flow, "ShiftContext.Begin called twice; ignored");
                return;
            }
            if (Route == null) {
                Log.Error(LogCat.Flow, "ShiftContext.Begin without a route; the night can't start");
                return;
            }
            Setup = setup;
            ShiftServices shift = BuildServices(setup);
            Shift = shift;
            // Holds the bus and keeps pausing off until its first state (§2.1)
            shift.Director.Init(shift);

            // 1. RouteTracker, RouteProgress (T-M2-09, T-M2-11)
            // 2. ShiftClockDriver (T-M2-12)
            // 3. The bus, CCTV, the mode switch and the input adapters
            shift.Cabin.Init(shift);
            shift.Cctv.Init(shift);
            shift.Mode.Init(shift);
            shift.BusInput.Init(shift);
            shift.DriverLook.Init(shift);
            shift.OnFoot.Init(shift);
            shift.Interactor.Init(shift);
            shift.EngineSound.Init(shift);
            // 4–13. PlayerAttention, PassengerRegistry/ViewFactory, the ledger, sanity, scares,
            // death, monsters, hallucinations, items, journal/hints arrive with M3–M7; the legacy
            // game over stands in for DeathDirector until T-M4-06
            if (shift.GameOver != null) {
                shift.GameOver.Init(shift);
            }
            // 14. ManifestSpawner: the legacy rider spawner until T-M2-07
            if (shift.Riders != null) {
                shift.Riders.Init(shift);
            }
            // 15. Every IShiftBindable (all UI)
            BindShift(bindables, shift);
            BindShift(Route.Bindables, shift);
            Log.Info(LogCat.Flow, $"night {setup.NightIndex} begins{(setup.IsDebugRun ? " (debug run)" : "")}");
            // 16. The intro card, then Driving
            shift.Director.Begin();
        }

        ShiftServices BuildServices(NightSetup setup) {
            ShiftServices shift = new ShiftServices {
                Game = Game,
                Setup = setup,
                Rng = new RngStreams(setup.Run.seed),
                Route = Route,
                Bus = bus,
                Doors = bus.GetComponent<BusDoors>(),
                Cabin = bus.GetComponent<BusCabin>(),
                Cctv = bus.GetComponentInChildren<CCTVSystem>(true),
                BusInput = bus.GetComponent<BusInput>(),
                DriverLook = bus.GetComponentInChildren<DriverLook>(true),
                EngineSound = bus.GetComponent<BusEngineSound>(),
                Mode = mode,
                OnFoot = onFoot,
                Interactor = interactor,
                DriverCamera = mode.DriverCamera,
                OnFootCamera = mode.OnFootCamera,
                GameOver = gameOver,
                Riders = Route.Riders,
                Director = director,
            };
            return shift;
        }

        static void BindGame(IReadOnlyList<MonoBehaviour> list, GameServices game) {
            for (int i = 0; i < list.Count; i++) {
                IGameBindable bindable = list[i] as IGameBindable;
                if (bindable != null) {
                    bindable.Bind(game);
                }
            }
        }

        static void BindShift(IReadOnlyList<MonoBehaviour> list, ShiftServices shift) {
            for (int i = 0; i < list.Count; i++) {
                IShiftBindable bindable = list[i] as IShiftBindable;
                if (bindable != null) {
                    bindable.Bind(shift);
                }
            }
        }
    }
}
