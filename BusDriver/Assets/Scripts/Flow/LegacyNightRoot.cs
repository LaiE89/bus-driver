using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using UnityEngine;

namespace BusDriver.Gameplay.Flow {
    // The scene root of the MVP driving scene (BusRoute) until ShiftContext replaces it in
    // T-M1-15. The legacy scene builder adds it.
    public sealed class LegacyNightRoot : MonoBehaviour, INightRoot {
        [Tooltip("The bus prefab starts with DriveLock.Scripted; this releases it until ShiftDirector exists (T-M1-17)")]
        [SerializeField] BusController bus;

        public GameServices Game { get; private set; }
        public NightSetup Setup { get; private set; }

        // Hands the services to the look controllers, the options screen and the rest (D56)
        public void Initialize(GameServices game) {
            Game = game;
            SceneBinding.BindAll(gameObject.scene, game);
        }

        public void Begin(NightSetup setup) {
            Setup = setup;
            if (bus != null) {
                bus.SetDriveLock(DriveLock.Scripted, false);
            }else {
                Log.Error(LogCat.Flow, "LegacyNightRoot has no bus; it stays locked (DriveLock.Scripted)");
            }
            Log.Info(LogCat.Flow, $"night {setup.NightIndex} begins (legacy scene{(setup.IsDebugRun ? ", debug run" : "")})");
        }
    }
}
