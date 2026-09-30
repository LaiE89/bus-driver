using BusDriver.Core.Data;
using UnityEngine;

namespace BusDriver.Gameplay.Views {
    // The generated primitive bus (PrefabBuilder, T-M1-14): the MVP shell, interior, wheels and
    // door panel, driven through BusViewBase. It holds no colliders (§4.14).
    public sealed class GreyboxBusView : BusViewBase {
        [Tooltip("FL, FR, RL, RR")]
        [SerializeField] Transform[] wheels = new Transform[4];
        [SerializeField] Transform steeringWheel;
        [Tooltip("Steering-wheel degrees per degree of road-wheel angle")]
        [SerializeField] float steeringRatio = 12f;
        [SerializeField] Transform doorPanel;
        [SerializeField] Vector3 doorClosedLocalPos;
        [SerializeField] Vector3 doorOpenLocalPos;
        [SerializeField] Renderer[] cabinLights = new Renderer[0];
        [SerializeField] Color cabinLightEmission = Color.white;
        [Tooltip("Indexed by DashScreen: Clock, FareBox, Gps, Mirror")]
        [SerializeField] Transform[] dashAnchors = new Transform[4];

        Quaternion steeringRest = Quaternion.identity;
        MaterialPropertyBlock block;

        void Awake() {
            if (steeringWheel != null) {
                steeringRest = steeringWheel.localRotation;
            }
        }

        public override void SetDoorOpen(float open01) {
            if (doorPanel != null) {
                doorPanel.localPosition = Vector3.Lerp(doorClosedLocalPos, doorOpenLocalPos, Mathf.SmoothStep(0f, 1f, open01));
            }
        }

        // The wheel is a cylinder, so its own Y is the column
        public override void SetSteering(float wheelAngleDeg) {
            if (steeringWheel != null) {
                steeringWheel.localRotation = steeringRest * Quaternion.Euler(0f, wheelAngleDeg * steeringRatio, 0f);
            }
        }

        // The view sits at the bus root with no offset, so root-local is view-local
        public override void SetWheelPose(int wheelIndex, Vector3 localPos, Quaternion localRot) {
            if (wheelIndex < 0 || wheelIndex >= wheels.Length || wheels[wheelIndex] == null) {
                return;
            }
            wheels[wheelIndex].SetLocalPositionAndRotation(localPos, localRot);
        }

        public override void SetInteriorLights(float intensity01) {
            if (block == null) {
                block = new MaterialPropertyBlock();
            }
            block.SetColor("_EmissionColor", cabinLightEmission * Mathf.Clamp01(intensity01));
            for (int i = 0; i < cabinLights.Length; i++) {
                if (cabinLights[i] != null) {
                    cabinLights[i].SetPropertyBlock(block);
                }
            }
        }

        public override Transform DashAnchor(DashScreen screen) {
            int index = (int)screen;
            return index >= 0 && index < dashAnchors.Length ? dashAnchors[index] : null;
        }
    }
}
