using BusDriver.Core.Util;
using UnityEngine;

namespace BusDriver.Gameplay.Flow {
    // The scene root of the MVP driving scene (BusRoute) until ShiftContext replaces it in
    // T-M1-15. The legacy scene builder adds it.
    public sealed class LegacyNightRoot : MonoBehaviour, INightRoot {
        public GameServices Game { get; private set; }
        public NightSetup Setup { get; private set; }

        public void Initialize(GameServices game) {
            Game = game;
        }

        public void Begin(NightSetup setup) {
            Setup = setup;
            Log.Info(LogCat.Flow, $"night {setup.NightIndex} begins (legacy scene{(setup.IsDebugRun ? ", debug run" : "")})");
        }
    }
}
