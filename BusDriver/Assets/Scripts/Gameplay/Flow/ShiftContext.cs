using System.Collections.Generic;
using System.Text;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Attention;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Economy;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.Scares;
using BusDriver.Gameplay.Shift;
using BusDriver.Gameplay.Views;
using UnityEngine;

namespace BusDriver.Gameplay.Flow {
    // The night systems scene's root (§4.3, §4.5). SceneLoader calls Initialize when the scene is
    // wired; RunFlow calls AttachRoute once the route scene is loaded, then Begin. Begin builds
    // ShiftServices and calls Init in the fixed order. Steps whose services don't exist yet are
    // skipped: they arrive with their tickets (sanity, scares, death and monsters in M4–M7).
    // ShiftDirector owns the drive lock and the pause predicate.
    public sealed class ShiftContext : MonoBehaviour, INightRoot {
        [SerializeField] BusController bus;
        [SerializeField] PlayerModeController mode;
        [SerializeField] OnFootController onFoot;
        [SerializeField] PlayerInteractor interactor;
        [Tooltip("The death pipeline and its presenters (§2.14)")]
        [SerializeField] DeathDirector death;
        [SerializeField] ShiftDirector director;
        [SerializeField] RouteTracker tracker;
        [SerializeField] RouteProgress progress;
        [SerializeField] ShiftClockDriver clock;
        [Tooltip("Observer evaluation (§2.8)")]
        [SerializeField] PlayerAttention attention;
        [Tooltip("Every rider of the night (§4.6)")]
        [SerializeField] PassengerRegistry riders;
        [Tooltip("Creates passenger views from looks (§4.14)")]
        [SerializeField] ViewFactory views;
        [Tooltip("Spawns the night's riders (§4.6)")]
        [SerializeField] ManifestSpawner manifest;
        [Tooltip("The night's monsters and the kill-sequence slot (§4.6)")]
        [SerializeField] MonsterSystem monsters;
        [Tooltip("Plays scare steps (§4.6)")]
        [SerializeField] ScarePlayer scarePlayer;
        [Tooltip("The single gate for scares (§2.17)")]
        [SerializeField] ScareDirector scares;
        [Tooltip("The development and test driver (§4.18)")]
        [SerializeField] AutoPilot autoPilot;
        [Tooltip("Development and test riders (T-M2-07), until ManifestSpawner (T-M3-03)")]
        [SerializeField] DebugRiders debugRiders;
        [Tooltip("UI and game-scoped components of this scene, bound in list order (§4.5 step 15)")]
        [SerializeField] MonoBehaviour[] bindables = new MonoBehaviour[0];

        public GameServices Game { get; private set; }
        public RouteSceneRoot Route { get; private set; }
        public NightSetup Setup { get; private set; }
        public ShiftServices Shift { get; private set; }
        public bool HasBegun { get { return Shift != null; } }
        public BusController Bus { get { return bus; } }
        public ShiftDirector Director { get { return director; } }
        public DeathDirector Death { get { return death; } }

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
            RegisterDebugSections(shift);

            // 1. RouteTracker, RouteProgress; a night that ends before the route does closes the
            // road past its end stop (§2.4, D43)
            shift.Tracker.Init(shift);
            shift.Progress.Init(shift);
            if (shift.Progress.EndStop != null) {
                Route.ActivateNightEnd(shift.Progress.EndStop.StopId);
            }
            // 2. ShiftClockDriver
            shift.Clock.Init(shift);
            // 3. The bus, CCTV, the mode switch and the input adapters
            shift.Cabin.Init(shift);
            shift.Cctv.Init(shift);
            shift.Mode.Init(shift);
            shift.BusInput.Init(shift);
            shift.DriverLook.Init(shift);
            shift.OnFoot.Init(shift);
            shift.Interactor.Init(shift);
            shift.EngineSound.Init(shift);
            if (shift.AutoPilot != null) {
                shift.AutoPilot.Init(shift);
            }
            // 4. PlayerAttention
            shift.Attention.Init(shift);
            // 5. PassengerRegistry, ViewFactory
            shift.Riders.Init(shift);
            shift.Views.Init(shift);
            // 6. ShiftLedger (built in BuildServices), EconomyRules
            shift.Economy.Init(shift);
            // 7. Sanity arrives with M5
            // 8. ScarePlayer, ScareDirector
            shift.ScarePlayer.Init(shift);
            shift.Scares.Init(shift);
            // 9. DeathDirector and its presenters
            shift.Death.Init(shift);
            // 10. MonsterSystem
            shift.Monsters.Init(shift);
            // 11–13. Hallucinations, items and journal/hints arrive with M5–M7
            // 14. ManifestSpawner, and the debug rider hook beside it
            shift.Manifest.Init(shift);
            if (shift.DebugRiders != null) {
                shift.DebugRiders.Init(shift);
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
                Fade = new ScreenFade(),
                Debug = new DebugRegistry(),
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
                Shake = mode.DriverCamera != null ? mode.DriverCamera.GetComponent<CameraShake>() : null,
                Death = death,
                DebugRiders = debugRiders,
                Director = director,
                Tracker = tracker,
                Progress = progress,
                Clock = clock,
                Attention = attention,
                AutoPilot = autoPilot,
                Riders = riders,
                Views = views,
                Manifest = manifest,
                Monsters = monsters,
                ScarePlayer = scarePlayer,
                Scares = scares,
                ScareOverlay = new ScareOverlayState(),
                CabinLights = bus.GetComponentInChildren<CabinLights>(true),
                ScareAnchors = bus.GetComponentInChildren<ScareAnchors>(true),
                Night = ResolveNight(setup),
                Balance = ResolveBalance(),
                Ledger = new ShiftLedger(setup.Run.walletCents),
                Economy = new EconomyRules(),
            };
            return shift;
        }

        BalanceConfig ResolveBalance() {
            BalanceConfig balance = Game != null && Game.Config != null ? Game.Config.balance : null;
            if (balance == null) {
                Log.Warn(LogCat.Content, "GameRootConfig has no BalanceConfig; using the seeded defaults");
                balance = ScriptableObject.CreateInstance<BalanceConfig>();
            }
            return balance;
        }

        NightDefinition ResolveNight(NightSetup setup) {
            GameRootConfig config = Game != null ? Game.Config : null;
            NightDefinition night = config != null ? config.Night(setup.NightIndex) : null;
            if (night == null && config != null) {
                Log.Warn(LogCat.Content, $"GameRootConfig has no night {setup.NightIndex}; using night 1");
                night = config.Night(1);
            }
            return night;
        }

        // The first F1 section (T-M1-18). ShiftClockDriver registers "Clock", RouteTracker "Route"
        // and RouteProgress "Stops".
        void RegisterDebugSections(ShiftServices shift) {
            shift.Debug.Register("Run", text => {
                text.Append("seed ").Append(shift.Setup.Run.seed)
                    .Append("  night ").Append(shift.Setup.NightIndex)
                    .Append("  wallet ").Append(Money.Format(shift.Ledger.WalletNowCents))
                    .Append(shift.Setup.IsDebugRun ? "  (debug run)" : "").Append('\n');
                text.Append("shift ").Append(shift.Director.State)
                    .Append("  night time ").Append(shift.Director.NightSeconds.ToString("0.0")).Append(" s\n");
            });
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
