using System;
using BusDriver.Core.Save;
using BusDriver.Core.Util;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BusDriver.Gameplay.Flow {
    // Explicit values, append-only
    public enum RunFlowState : int { Boot = 0, FirstLaunch = 1, Menu = 2, LoadingNight = 3, InNight = 4 }

    // The run state machine (§4.4), plain C# owned by GameRoot. This is the T-M1-04 skeleton:
    // Boot, Menu, LoadingNight, InNight, New Run and the editor debug run. Night results, Continue,
    // abandoned detection and deaths arrive with their tickets (T-M3-06, T-M4-06, T-M7-02).
    public sealed class RunFlow {
        public const string DebugSeedPref = "BusDriver.DebugSeed";
        public const string DebugNightPref = "BusDriver.DebugNight";

        readonly GameServices game;
        NightSetup pendingNight;

        public RunFlowState State { get; private set; } = RunFlowState.Boot;
        public RunState Run { get; private set; }
        public bool IsDebugRun { get; private set; }

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
            if (State == RunFlowState.LoadingNight) {
                Log.Warn(LogCat.Flow, "New Run ignored: a night is already loading");
                return;
            }
            Run = RunState.NewRun(RngStreams.NewSeed());
            IsDebugRun = false;
            game.Meta.RecordRunStarted();
            game.Saves.Save(SaveSlot.Run, Run);
            Log.Info(LogCat.Flow, $"new run seed={Run.seed}");
            LoadNight();
        }

        // A plain quit for now; leaving a night as a death (D21) arrives in T-M7-08
        public void QuitToMenu() {
            if (game.Scenes.IsLoading) {
                Log.Warn(LogCat.Flow, "Quit to Menu ignored: a scene is loading");
                return;
            }
            game.Scenes.Run(game.Scenes.LoadMenu());
        }

        void LoadNight() {
            pendingNight = new NightSetup(Run, IsDebugRun);
            SetState(RunFlowState.LoadingNight);
            game.Scenes.Run(game.Scenes.LoadNight(pendingNight));
        }

        void HandleSceneReady(Scene scene, ISceneRoot root) {
            INightRoot night = root as INightRoot;
            if (night == null) {
                if (scene.name == SceneIds.Menu || State == RunFlowState.Boot) {
                    SetState(RunFlowState.Menu);
                }
                return;
            }
            if (State != RunFlowState.LoadingNight || pendingNight == null) {
                // Play pressed in a night scene, or legacy code loaded one directly
                if (Run == null || State == RunFlowState.Boot) {
                    StartDebugRun();
                }
                pendingNight = new NightSetup(Run, IsDebugRun);
            }
            NightSetup setup = pendingNight;
            pendingNight = null;
            SetState(RunFlowState.InNight);
            night.Begin(setup);
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
