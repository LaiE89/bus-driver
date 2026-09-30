using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.Shift;
using BusDriver.Gameplay.World;
using UnityEngine;

namespace BusDriver.Gameplay.Flow {
    // Everything one night's objects may reach (§4.5): the game services, the night setup, the
    // route, the RNG streams and the night services. ShiftContext fills it once, before the first
    // Init call, so every Init can already see every reference; the services themselves are only
    // usable once their own Init has run. Services that don't exist yet are simply absent.
    public sealed class ShiftServices {
        public GameServices Game;
        public NightSetup Setup;
        public RngStreams Rng;
        // The F1 overlay's sections and cheats (§4.18); services register theirs in Init
        public DebugRegistry Debug;
        public RouteSceneRoot Route;
        public ShiftDirector Director;

        public BusController Bus;
        public BusDoors Doors;
        public BusCabin Cabin;
        public CCTVSystem Cctv;
        public BusInput BusInput;
        public DriverLook DriverLook;
        public BusEngineSound EngineSound;

        public PlayerModeController Mode;
        public OnFootController OnFoot;
        public PlayerInteractor Interactor;
        public Camera DriverCamera;
        public Camera OnFootCamera;

        // Legacy until their replacements: DeathDirector (T-M4-06), ManifestSpawner (T-M2-07)
        public LegacyGameOver GameOver;
        public LegacyRiderSpawner Riders;
    }

    // UI and other components that ShiftContext binds without knowing their types (§4.5 step 15)
    public interface IShiftBindable {
        void Bind(ShiftServices shift);
    }
}
