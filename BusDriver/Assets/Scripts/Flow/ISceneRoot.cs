using BusDriver.Core.Save;
using BusDriver.Gameplay.Route;

namespace BusDriver.Gameplay.Flow {
    // Exactly one per scene, on a root GameObject (§4.5). SceneLoader finds it through
    // scene.GetRootGameObjects() and calls Initialize once the scene is loaded, before any Start.
    public interface ISceneRoot {
        void Initialize(GameServices game);
    }

    // The root of a night's systems scene (ShiftContext). RunFlow hands it the route scene's root
    // once that is loaded, then begins the night (§4.4).
    public interface INightRoot : ISceneRoot {
        bool HasBegun { get; }
        void AttachRoute(RouteSceneRoot route);
        void Begin(NightSetup setup);
    }

    // Everything a night needs to know about the run it belongs to
    public sealed class NightSetup {
        public int NightIndex { get; }
        public RunState Run { get; }
        // Editor debug runs never write run.json or meta.json (§4.4)
        public bool IsDebugRun { get; }

        public NightSetup(RunState run, bool isDebugRun) {
            Run = run;
            NightIndex = run.nightIndex;
            IsDebugRun = isDebugRun;
        }
    }
}
