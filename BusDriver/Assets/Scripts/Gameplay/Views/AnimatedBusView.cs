using BusDriver.Core.Data;
using UnityEngine;

namespace BusDriver.Gameplay.Views {
    // The artist's bus (T-M10-05, Appendix A.2): a rigged model whose wheels, door leaves and
    // steering column are bones. BusArtViewBuilder binds everything by name at build time, so
    // nothing here searches the hierarchy. Holds no colliders (§4.14).
    public sealed class AnimatedBusView : BusViewBase {
        [Tooltip("The bus root the wheel poses are relative to (the view's parent)")]
        [SerializeField] Transform busRoot;

        [Header("Wheels")]
        [Tooltip("FL, FR, RL, RR: the spin bones. A rear bone carries both tyres of its dual pair.")]
        [SerializeField] Transform[] wheelBones = new Transform[4];
        [Tooltip("FL, FR, RL, RR: the wheel centre at rest, root-local. The wheel colliders sit here too.")]
        [SerializeField] Vector3[] wheelRestCentres = new Vector3[4];

        [Header("Steering")]
        [SerializeField] Transform steeringColumn;
        [Tooltip("The column's own axis, in its local space")]
        [SerializeField] Vector3 steeringAxis = Vector3.right;
        [Tooltip("Steering-wheel degrees per degree of road-wheel angle; the sign depends on the column bone's axis")]
        [SerializeField] float steeringRatio = 12f;

        [Header("Door")]
        [Tooltip("The door leaves' hinge bones")]
        [SerializeField] Transform[] doorLeaves = new Transform[0];
        [Tooltip("Per leaf: degrees about the bus's up axis when fully open")]
        [SerializeField] float[] doorOpenAngles = new float[0];

        [Header("Cabin")]
        [Tooltip("Emissive panels dimmed with the cabin lights; none on Bus_Asset_001")]
        [SerializeField] Renderer[] cabinLights = new Renderer[0];
        [Tooltip("Indexed by DashScreen: Clock, FareBox, Gps, Mirror")]
        [SerializeField] Transform[] dashAnchors = new Transform[4];

        // Each bone's rest pose relative to its wheel centre, in root space
        Vector3[] boneOffsets;
        Quaternion[] boneRestRotations;
        Quaternion steeringRest = Quaternion.identity;
        Quaternion[] doorRest;
        MaterialPropertyBlock block;

        void Awake() {
            if (busRoot == null) {
                busRoot = transform.parent != null ? transform.parent : transform;
            }
            boneOffsets = new Vector3[wheelBones.Length];
            boneRestRotations = new Quaternion[wheelBones.Length];
            for (int i = 0; i < wheelBones.Length; i++) {
                if (wheelBones[i] == null) {
                    continue;
                }
                Vector3 centre = i < wheelRestCentres.Length ? wheelRestCentres[i] : Vector3.zero;
                boneOffsets[i] = busRoot.InverseTransformPoint(wheelBones[i].position) - centre;
                boneRestRotations[i] = Quaternion.Inverse(busRoot.rotation) * wheelBones[i].rotation;
            }
            if (steeringColumn != null) {
                steeringRest = steeringColumn.localRotation;
            }
            doorRest = new Quaternion[doorLeaves.Length];
            for (int i = 0; i < doorLeaves.Length; i++) {
                doorRest[i] = doorLeaves[i] != null ? Quaternion.Inverse(busRoot.rotation) * doorLeaves[i].rotation : Quaternion.identity;
            }
        }

        // Each leaf turns about the bus's vertical axis through its hinge
        public override void SetDoorOpen(float open01) {
            if (doorRest == null) {
                return;
            }
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(open01));
            for (int i = 0; i < doorLeaves.Length; i++) {
                if (doorLeaves[i] == null) {
                    continue;
                }
                float angle = i < doorOpenAngles.Length ? doorOpenAngles[i] * t : 0f;
                doorLeaves[i].rotation = busRoot.rotation * Quaternion.AngleAxis(angle, Vector3.up) * doorRest[i];
            }
        }

        public override void SetSteering(float wheelAngleDeg) {
            if (steeringColumn != null) {
                steeringColumn.localRotation = steeringRest * Quaternion.AngleAxis(wheelAngleDeg * steeringRatio, steeringAxis);
            }
        }

        // The pose is the wheel collider's, root-local: the bone rides it rigidly from its rest offset
        public override void SetWheelPose(int wheelIndex, Vector3 localPos, Quaternion localRot) {
            if (boneOffsets == null || wheelIndex < 0 || wheelIndex >= wheelBones.Length || wheelBones[wheelIndex] == null) {
                return;
            }
            Vector3 position = localPos + localRot * boneOffsets[wheelIndex];
            Quaternion rotation = localRot * boneRestRotations[wheelIndex];
            wheelBones[wheelIndex].SetPositionAndRotation(busRoot.TransformPoint(position), busRoot.rotation * rotation);
        }

        // Same rule as the greybox: full brightness is the material's own emission
        public override void SetInteriorLights(float intensity01) {
            intensity01 = Mathf.Clamp01(intensity01);
            if (block == null) {
                block = new MaterialPropertyBlock();
            }
            for (int i = 0; i < cabinLights.Length; i++) {
                Renderer panel = cabinLights[i];
                if (panel == null) {
                    continue;
                }
                if (intensity01 >= 0.999f) {
                    panel.SetPropertyBlock(null);
                    continue;
                }
                Material material = panel.sharedMaterial;
                Color emission = material != null && material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.white;
                block.SetColor("_EmissionColor", emission * intensity01);
                panel.SetPropertyBlock(block);
            }
        }

        public override Transform DashAnchor(DashScreen screen) {
            int index = (int)screen;
            return index >= 0 && index < dashAnchors.Length ? dashAnchors[index] : null;
        }
    }
}
