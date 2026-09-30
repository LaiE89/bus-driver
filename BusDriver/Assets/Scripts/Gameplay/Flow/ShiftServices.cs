using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.Shift;
using BusDriver.Gameplay.Views;
using UnityEngine;

namespace BusDriver.Gameplay.Flow {
    // Everything one night's objects may reach (§4.5): the game services, the night setup, the
    // route, the RNG streams and the night services. ShiftContext fills it once, before the first
    // Init call, so every Init can already see every reference; the services themselves are only
    // usable once their own Init has run. Services that don't exist yet are simply absent.
    public sealed class ShiftServices {
        public GameServices Game;
        public NightSetup Setup;
        // This night's definition (§2.19); night 1's when the config lacks this night
        public NightDefinition Night;
        public RngStreams Rng;
        // The F1 overlay's sections and cheats (§4.18); services register theirs in Init
        public DebugRegistry Debug;
        // The night's fade to black (the KillPlane respawn, deaths)
        public ScreenFade Fade;
        public RouteSceneRoot Route;
        public ShiftDirector Director;
        public RouteTracker Tracker;
        public RouteProgress Progress;
        public ShiftClockDriver Clock;

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
        // On the driver camera: the rumble strip, scares
        public CameraShake Shake;

        // Every rider of the night (§4.6)
        public PassengerRegistry Riders;
        // Passenger views from looks (§4.14)
        public ViewFactory Views;
        // Spawns the night's riders (§4.6)
        public ManifestSpawner Manifest;

        // Legacy until DeathDirector (T-M4-06)
        public LegacyGameOver GameOver;
        // Development and test riders, until ManifestSpawner (T-M3-03)
        public DebugRiders DebugRiders;
        // The development and test driver (§4.18)
        public AutoPilot AutoPilot;
    }

    // UI and other components that ShiftContext binds without knowing their types (§4.5 step 15)
    public interface IShiftBindable {
        void Bind(ShiftServices shift);
    }
}
