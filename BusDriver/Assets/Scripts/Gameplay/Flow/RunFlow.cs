using System;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Save;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.Shift;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BusDriver.Gameplay.Flow {
    // Explicit values, append-only
    public enum RunFlowState : int { Boot = 0, FirstLaunch = 1, Menu = 2, LoadingNight = 3, InNight = 4 }

    // The run state machine (§4.4), plain C# owned by GameRoot: Boot, Menu, LoadingNight, InNight,
    // New Run, the editor debug run, and the night results (T-M3-06). Continue, abandoned detection
    // and deaths arrive with their tickets (T-M4-06, T-M7-02).
    public sealed class RunFlow {
        public const string DebugSeedPref = "BusDriver.DebugSeed";
        public const string DebugNightPref = "BusDriver.DebugNight";
        public const string DebugNoMonstersPref = "BusDriver.DebugNoMonsters";

        readonly GameServices game;
        NightSetup pendingNight;
        // The loaded night's systems root, waiting for its route or already begun
        INightRoot currentNight;

        public RunFlowState State { get; private set; } = RunFlowState.Boot;
        public RunState Run { get; private set; }
        public bool IsDebugRun { get; private set; }
        // Development and test: nights load without their monster riders (T-M3-03)
        public bool DebugNoMonsters { get; set; }

        public event Action<RunFlowState> OnStateChanged;

        public RunFlow(GameServices game) {
            this.game = game;
            game.Scenes.OnSceneReady += HandleSceneReady;
        }

        // Settings and meta are already loaded by their services. The state is decided when the
        // first scene is wired, from its root: a night root means an editor debug run. Until the
        // first-launch warning exists (T-M8-02) Boot never goes to FirstLaunch (D55).
        public void Boot(Scene activeScene) {
            Log.Info(LogCat.Flow, "boot in scene " + activeScene.name);
        }

        public void NewRun() {
            NewRun(RngStreams.NewSeed());
        }

        // Tests and the smoke test: a New Run with a fixed seed, so every random system (riders,
        // seats) plays out the same way on every run
        internal void NewRun(int seed) {
            if (State == RunFlowState.LoadingNight) {
                Log.Warn(LogCat.Flow, "New Run ignored: a night is already loading");
                return;
            }
            Run = RunState.NewRun(seed);
            IsDebugRun = false;
            game.Meta.RecordRunStarted();
            game.Saves.Save(SaveSlot.Run, Run);
            Log.Info(LogCat.Flow, $"new run seed={Run.seed}");
            LoadNight();
        }

        // Development and test: a debug run (never saved, §4.4) straight into night n — the §4.18
        // "jump to night N" cheat and the tests that need a later night
        internal void NewDebugRun(int seed, int nightIndex) {
            if (State == RunFlowState.LoadingNight) {
                Log.Warn(LogCat.Flow, "debug run ignored: a night is already loading");
                return;
            }
            Run = RunState.NewRun(seed);
            Run.nightIndex = Mathf.Clamp(nightIndex, 1, 5);
            IsDebugRun = true;
            Log.Info(LogCat.Flow, $"debug run seed={seed} night={Run.nightIndex}");
            LoadNight();
        }

        // A plain quit for now; leaving a night as a death (D21) arrives in T-M7-08
        public void QuitToMenu() {
            LoadMenu();
        }

        // Back to the menu scene, whatever the run's state (Game Over, Run Won, a quit)
        public void LoadMenu() {
            if (game.Scenes.IsLoading) {
                Log.Warn(LogCat.Flow, "Load Menu ignored: a scene is loading");
                return;
            }
            game.Scenes.Run(game.Scenes.LoadMenu());
        }

        void LoadNight() {
            pendingNight = new NightSetup(Run, IsDebugRun, DebugNoMonsters);
            SetState(RunFlowState.LoadingNight);
            game.Scenes.Run(game.Scenes.LoadNight(pendingNight));
        }

        // A night needs both its systems scene (the INightRoot) and its route scene (the
        // RouteSceneRoot); whichever is wired second starts it. Both hooks run from sceneLoaded, so
        // the night begins before any Start in the route scene.
        void HandleSceneReady(Scene scene, ISceneRoot root) {
            INightRoot night = root as INightRoot;
            if (night != null) {
                HandleNightRoot(scene, night);
                return;
            }
            RouteSceneRoot route = SceneLoader.FindInScene<RouteSceneRoot>(scene);
            if (route != null) {
                if (currentNight != null && !currentNight.HasBegun) {
                    BeginNight(route);
                }
                return;
            }
            if (scene.name == SceneIds.Menu || State == RunFlowState.Boot) {
                currentNight = null;
                SetState(RunFlowState.Menu);
            }
        }

        void HandleNightRoot(Scene scene, INightRoot night) {
            currentNight = night;
            bool loadingByFlow = State == RunFlowState.LoadingNight && pendingNight != null;
            if (!loadingByFlow) {
                // Play pressed in a night scene (the editor debug run, §4.4)
                if (Run == null || State == RunFlowState.Boot) {
                    StartDebugRun();
                }
                pendingNight = new NightSetup(Run, IsDebugRun, DebugNoMonsters);
            }
            // A route in the same scene (the legacy single-scene night) starts it at once
            RouteSceneRoot route = SceneLoader.FindInScene<RouteSceneRoot>(scene);
            if (route != null) {
                BeginNight(route);
                return;
            }
            // SceneLoader.LoadNight loads the route next; its own sceneLoaded begins the night.
            // Outside that, the route may already be open beside it (both scenes open in the
            // editor), or it is loaded now.
            if (loadingByFlow) {
                return;
            }
            route = SceneLoader.FindLoaded<RouteSceneRoot>();
            if (route != null) {
                BeginNight(route);
            }else {
                game.Scenes.Run(game.Scenes.LoadRouteAdditive());
            }
        }

        void BeginNight(RouteSceneRoot route) {
            NightSetup setup = pendingNight;
            pendingNight = null;
            if (setup == null) {
                setup = new NightSetup(Run, IsDebugRun, DebugNoMonsters);
            }
            SetState(RunFlowState.InNight);
            // The route scene's lighting is the night's (§4.3); its LightingPresetApplier puts
            // the brightness on when AttachRoute binds it
            Scene routeScene = route.gameObject.scene;
            if (SceneManager.GetActiveScene() != routeScene) {
                SceneManager.SetActiveScene(routeScene);
            }
            // The director dies with its scene, and these subscriptions with it
            ShiftDirector director = currentNight.Director;
            if (director != null) {
                director.OnNightCompleted += CompleteNight;
                director.OnSummaryConfirmed += HandleSummaryConfirmed;
                director.OnRunWon += HandleRunWon;
            }
            currentNight.AttachRoute(route);
            currentNight.Begin(setup);
        }

        // §4.4: the night's result goes into the run: wallet, sanity carry-over (§2.15), stats and
        // history. The next night is the run's night from now on, though it's only saved (and so
        // only reachable by Continue) once the Summary is confirmed.
        public void CompleteNight(NightResult result) {
            if (Run == null || result == null) {
                return;
            }
            BalanceConfig balance = game.Config != null ? game.Config.balance : null;
            float bonus = balance != null ? balance.sanityCarryBonus : 30f;
            float floor = balance != null ? balance.sanityCarryFloor : 60f;
            Run.walletCents = result.WalletAfterCents;
            Run.sanity = EconomyMath.CarriedSanity(result.sanityEnd, bonus, floor);
            result.AddTo(Run.stats);
            Run.nights.Add(result.ToHistory());
            Run.nightIndex = result.nightIndex + 1;
            Run.nightInProgress = false;
            Log.Info(LogCat.Flow, $"night {result.nightIndex} complete: wallet {Money.Format(Run.walletCents)}, next night {Run.nightIndex}");
        }

        // Summary Continue on nights 1–4: save the run, then the next night (§4.4). Debug runs
        // never write.
        void HandleSummaryConfirmed() {
            if (Run == null) {
                return;
            }
            if (!IsDebugRun) {
                game.Saves.Save(SaveSlot.Run, Run);
            }
            LoadNight();
        }

        // Summary Continue on night 5: the run is won and its save is gone (§4.4). The Run Won
        // screen arrives in T-M7-07; until then the run goes straight back to the menu (D91).
        void HandleRunWon() {
            if (!IsDebugRun) {
                game.Saves.Delete(SaveSlot.Run);
                game.Meta.RecordRunWon();
            }
            Log.Info(LogCat.Flow, "run won");
            LoadMenu();
        }

        // Editor only: seed and night from EditorPrefs (default random / 1). Debug runs never
        // write run.json or meta.json (§4.4).
        void StartDebugRun() {
            int seed = RngStreams.NewSeed();
            int night = 1;
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.HasKey(DebugSeedPref)) {
                seed = UnityEditor.EditorPrefs.GetInt(DebugSeedPref);
            }
            night = Mathf.Clamp(UnityEditor.EditorPrefs.GetInt(DebugNightPref, 1), 1, 5);
            DebugNoMonsters = UnityEditor.EditorPrefs.GetBool(DebugNoMonstersPref, false);
#endif
            Run = RunState.NewRun(seed);
            Run.nightIndex = night;
            IsDebugRun = true;
            Log.Info(LogCat.Flow, $"debug run seed={seed} night={night}");
        }

        void SetState(RunFlowState next) {
            if (State == next) {
                return;
            }
            Log.Info(LogCat.Flow, $"run flow {State} -> {next}");
            State = next;
            if (OnStateChanged != null) {
                OnStateChanged(next);
            }
        }
    }
}
