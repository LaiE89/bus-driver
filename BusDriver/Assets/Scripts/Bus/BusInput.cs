using UnityEngine;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Input;

namespace BusDriver.Gameplay.Bus {
    // Driving actions → BusController. Throttle and steer stay analogue end to end (controller-ready
    // rule 5, §4.10); BusController does its own steering smoothing.
    public class BusInput : MonoBehaviour, IGameBindable {
        [SerializeField] BusController bus;

        // Something else (the smoke test, AutoPilot) calls bus.SetInput. Parking on disable still applies.
        public bool ExternalControl { get; set; }

        GameServices game;

        public void Bind(GameServices services) {
            game = services;
        }

        void OnEnable() {
            bus.Park(false);
        }

        // The Driving map is only live in the Driving context, so a paused game or a screen reads
        // as no input
        void Update() {
            if (ExternalControl || game == null) {
                return;
            }
            BusDriverActions actions = game.Input.Actions;
            bus.SetInput(actions.Steer.ReadValue<float>(), actions.Throttle.ReadValue<float>(), actions.Handbrake.IsPressed());

            if (actions.ResetBus.WasPressedThisFrame()) {
                bus.ResetUpright();
            }
        }

        // Nobody in the seat, so the bus parks itself
        void OnDisable() {
            if (bus != null) {
                bus.SetInput(0f, 0f, true);
                bus.Park(true);
            }
        }
    }
}
