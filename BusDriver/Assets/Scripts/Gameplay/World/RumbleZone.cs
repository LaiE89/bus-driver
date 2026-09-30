using BusDriver.Core.Data;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using UnityEngine;

namespace BusDriver.Gameplay.World {
    // A rumble strip (§3.3): across the road before Dead Man's Bend, then along its left edge. While
    // any wheel's contact point is over one of its boxes above 10 km/h, bus.rumble_strip plays on the
    // bus and the driver camera shakes lightly. The hull trigger (ZoneVolume) is only the cheap first
    // test: the edge strip is narrower than the bus, so the wheels decide.
    [RequireComponent(typeof(ZoneVolume))]
    public sealed class RumbleZone : MonoBehaviour, IShiftBindable {
        public const float MinSpeedKmh = 10f;
        public const float ShakeAmplitude = 0.1f;

        ZoneVolume volume;
        BoxCollider[] boxes = new BoxCollider[0];
        WheelCollider[] wheels = new WheelCollider[0];
        BusController bus;
        IAudioService audio;
        CameraShake shake;
        SoundHandle sound;

        public bool IsBusInside { get { return volume != null && volume.IsBusInside; } }
        public bool IsRumbling { get; private set; }

        void Awake() {
            volume = GetComponent<ZoneVolume>();
        }

        // ShiftContext, with the route scene's bindables (§4.5 step 15)
        public void Bind(ShiftServices shift) {
            boxes = GetComponentsInChildren<BoxCollider>(true);
            bus = shift.Bus;
            wheels = bus.GetComponentsInChildren<WheelCollider>(true);
            audio = shift.Game.Audio;
            shake = shift.Shake;
        }

        void FixedUpdate() {
            if (bus == null) {
                return;
            }
            SetRumbling(volume.IsBusInside && bus.SpeedKmh > MinSpeedKmh && AnyWheelOver());
        }

        void OnDisable() {
            SetRumbling(false);
        }

        void SetRumbling(bool on) {
            if (on == IsRumbling) {
                return;
            }
            IsRumbling = on;
            if (on) {
                sound = audio.PlayAttached(SoundIds.BusRumbleStrip, bus.transform);
            }else if (audio != null) {
                audio.Stop(sound);
                sound = SoundHandle.None;
            }
            if (shake != null) {
                shake.SetShake(this, on ? ShakeAmplitude : 0f);
            }
        }

        bool AnyWheelOver() {
            for (int w = 0; w < wheels.Length; w++) {
                WheelCollider wheel = wheels[w];
                Vector3 contact;
                if (wheel.GetGroundHit(out WheelHit hit)) {
                    contact = hit.point;
                }else {
                    wheel.GetWorldPose(out Vector3 centre, out Quaternion _);
                    contact = centre - wheel.transform.up * wheel.radius;
                }
                for (int b = 0; b < boxes.Length; b++) {
                    if (Contains(boxes[b], contact)) {
                        return true;
                    }
                }
            }
            return false;
        }

        static bool Contains(BoxCollider box, Vector3 world) {
            Vector3 local = box.transform.InverseTransformPoint(world) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
    }
}
