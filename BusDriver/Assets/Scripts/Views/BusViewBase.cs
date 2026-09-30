using BusDriver.Core.Data;
using UnityEngine;

namespace BusDriver.Gameplay.Views {
    // The only thing the bus logic knows about its visuals (§4.14, D16). GreyboxBusView and the
    // Phase B art view implement it; the logic root keeps every collider, camera and anchor.
    public abstract class BusViewBase : MonoBehaviour {
        public const int WheelFL = 0;
        public const int WheelFR = 1;
        public const int WheelRL = 2;
        public const int WheelRR = 3;

        // 0 closed … 1 open; the view picks its own easing
        public abstract void SetDoorOpen(float open01);
        // The front road wheels' angle; the view turns the steering wheel to match
        public abstract void SetSteering(float wheelAngleDeg);
        // Relative to the bus root. FL, FR, RL, RR
        public abstract void SetWheelPose(int wheelIndex, Vector3 localPos, Quaternion localRot);
        public abstract void SetInteriorLights(float intensity01);
        public abstract Transform DashAnchor(DashScreen screen);
    }
}
