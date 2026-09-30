using UnityEngine;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using BusDriver.UI.Screens;

namespace BusDriver.Gameplay.Bus {
    public class BusInput : MonoBehaviour, IGameBindable {
        [SerializeField] BusController bus;
        [SerializeField] KeyCode resetKey = KeyCode.R;

        // Something else (the smoke test) calls bus.SetInput. Parking on disable still applies.
        public bool ExternalControl { get; set; }

        GameServices game;

        public void Bind(GameServices services) {
            game = services;
        }

        void OnEnable() {
            bus.Park(false);
        }

        void Update() {
            if ((game != null && game.Pause.IsPaused) || ExternalControl) {
                return;
            }
            // Raw axes, BusController does its own steering smoothing
            float steer = UnityEngine.Input.GetAxisRaw("Horizontal");
            float accel = UnityEngine.Input.GetAxisRaw("Vertical");
            bus.SetInput(steer, accel, UnityEngine.Input.GetKey(GameKeys.handbrake));

            if (UnityEngine.Input.GetKeyDown(resetKey)) {
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
