using System.Collections.Generic;
using BusDriver.Editor.Art;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static BusDriver.Editor.Builders.BuilderUtil;

namespace BusDriver.Editor.Builders {
    // The Bus prefab's View when the artist's model is in Assets/Art (part of BuildAll step 7,
    // T-M10-05): the FBX, unpacked under View, with an AnimatedBusView bound to its bones by name.
    //
    // The model comes as 254 skinned meshes, ~200 of them bound only to the static chassis bone.
    // Those are baked into one static mesh per material (Generated/Meshes/BusArt), so the bus
    // costs a dozen draw calls instead of hundreds of skinned ones. Only the parts that move stay
    // skinned: wheels, door leaves, steering column, pedals and gauge needles.
    public static class BusArtViewBuilder {
        public const string MeshFolder = GeneratedRoot + "/Meshes/BusArt";

        // Bones that never move relative to the bus
        static readonly HashSet<string> StaticBones = new HashSet<string> { "root", "chassis", "accessories_null", "dash_null", "pedalbox_null" };
        static readonly string[] WheelBones = { "FL_rot", "FR_rot", "RL_rot", "RR_rot" };
        const string SteeringBone = "steercolumn";
        static readonly string[] DoorBones = { "door1", "door2" };

        public static bool ArtAvailable() {
            return AssetDatabase.LoadAssetAtPath<GameObject>(BusArtImport.ModelPath) != null;
        }

        public static AnimatedBusView Build(Transform root, Transform[] dashAnchors) {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(BusArtImport.ModelPath);
            GameObject viewObject = Group("View", root);
            AnimatedBusView view = viewObject.AddComponent<AnimatedBusView>();

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, viewObject.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            // Generic import adds an Animator; nothing drives it, and views hold no behaviour
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) {
                Object.DestroyImmediate(animator);
            }

            BakeStaticParts(viewObject.transform, model);

            Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) {
                if (!bones.ContainsKey(t.name)) {
                    bones.Add(t.name, t);
                }
            }
            Transform[] wheels = new Transform[WheelBones.Length];
            for (int i = 0; i < WheelBones.Length; i++) {
                wheels[i] = Bone(bones, WheelBones[i]);
            }
            Transform[] doors = new Transform[DoorBones.Length];
            for (int i = 0; i < DoorBones.Length; i++) {
                doors[i] = Bone(bones, DoorBones[i]);
            }

            BusTuning tuning = AssetDatabase.LoadAssetAtPath<BusTuning>(PrefabBuilder.TuningPath);
            SetRef(view, "busRoot", root);
            SetRefArray(view, "wheelBones", wheels);
            SetVectorArray(view, "wheelRestCentres", PrefabBuilder.WheelRestCentres(tuning));
            SetRef(view, "steeringColumn", Bone(bones, SteeringBone));
            // The column bone runs along its local X, up the column toward the driver
            SetVector(view, "steeringAxis", Vector3.right);
            // Positive turns this model's wheel clockwise, as the driver sees it, for a right turn
            SetFloat(view, "steeringRatio", 12f);
            SetRefArray(view, "doorLeaves", doors);
            // The rear leaf (hinged at z 4.2) and the front leaf (5.4) both fold in toward the aisle
            SetFloatArray(view, "doorOpenAngles", new[] { -BusLayout.DoorLeafOpenAngle, BusLayout.DoorLeafOpenAngle });
            SetRefArray(view, "cabinLights", new Object[0]);
            SetRefArray(view, "dashAnchors", dashAnchors);
            return view;
        }

        static Transform Bone(Dictionary<string, Transform> bones, string name) {
            Transform bone;
            if (!bones.TryGetValue(name, out bone)) {
                Debug.LogError("[BUILD] bus art: no bone " + name + " in " + BusArtImport.ModelPath);
            }
            return bone;
        }

        static bool IsStatic(SkinnedMeshRenderer renderer) {
            Transform[] bones = renderer.bones;
            if (bones == null || bones.Length == 0) {
                return true;
            }
            foreach (Transform bone in bones) {
                if (bone != null && !StaticBones.Contains(bone.name)) {
                    return false;
                }
            }
            return true;
        }

        // One mesh per material, in the view's space, from every chassis-bound part at rest
        static void BakeStaticParts(Transform view, GameObject model) {
            Dictionary<Material, List<CombineInstance>> byMaterial = new Dictionary<Material, List<CombineInstance>>();
            List<Material> order = new List<Material>();
            List<Mesh> baked = new List<Mesh>();
            List<SkinnedMeshRenderer> statics = new List<SkinnedMeshRenderer>();
            Matrix4x4 toView = view.worldToLocalMatrix;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if (renderer.sharedMesh == null || !IsStatic(renderer)) {
                    continue;
                }
                Mesh mesh = new Mesh();
                renderer.BakeMesh(mesh, true);
                baked.Add(mesh);
                Matrix4x4 matrix = toView * renderer.transform.localToWorldMatrix;
                Material[] materials = renderer.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) {
                    Material material = sub < materials.Length ? materials[sub] : null;
                    if (material == null) {
                        continue;
                    }
                    List<CombineInstance> list;
                    if (!byMaterial.TryGetValue(material, out list)) {
                        list = new List<CombineInstance>();
                        byMaterial.Add(material, list);
                        order.Add(material);
                    }
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = matrix });
                }
                statics.Add(renderer);
            }

            Transform group = Group("Static", view).transform;
            foreach (Material material in order) {
                List<CombineInstance> parts = byMaterial[material];
                int vertices = 0;
                foreach (CombineInstance instance in parts) {
                    vertices += instance.mesh.vertexCount;
                }
                Mesh combined = new Mesh { name = "Bus_" + material.name };
                combined.indexFormat = vertices > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                combined.CombineMeshes(parts.ToArray(), true, true);
                combined.RecalculateBounds();
                combined = SaveMesh(combined, $"{MeshFolder}/Bus_{material.name}.asset");

                GameObject part = Group(material.name, group);
                part.AddComponent<MeshFilter>().sharedMesh = combined;
                part.AddComponent<MeshRenderer>().sharedMaterial = material;
            }

            foreach (Mesh mesh in baked) {
                Object.DestroyImmediate(mesh);
            }
            // The baked parts' own objects are leaves: drop them, and any group left empty
            foreach (SkinnedMeshRenderer renderer in statics) {
                Object.DestroyImmediate(renderer.gameObject);
            }
            // Never the skeleton: leaf bones like FL_rot are empty transforms too. The rest are mesh
            // groups and the Maya lights/cameras the importer skipped.
            for (int i = model.transform.childCount - 1; i >= 0; i--) {
                Transform child = model.transform.GetChild(i);
                if (child.name == "Bus_Joint_GRP") {
                    continue;
                }
                RemoveEmptyGroups(child);
                if (child.childCount == 0 && child.GetComponents<Component>().Length == 1) {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
            Debug.Log($"[BUILD] bus art: {statics.Count} static parts baked into {order.Count} meshes, " +
                $"{model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length} skinned parts left");
        }

        // Depth first; keeps the root and anything with a component or a child
        static void RemoveEmptyGroups(Transform node) {
            for (int i = node.childCount - 1; i >= 0; i--) {
                Transform child = node.GetChild(i);
                RemoveEmptyGroups(child);
                if (child.childCount == 0 && child.GetComponents<Component>().Length == 1) {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        static void SetVectorArray(Object target, string prop, Vector3[] values) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty array = so.FindProperty(prop);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) {
                array.GetArrayElementAtIndex(i).vector3Value = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloatArray(Object target, string prop, float[] values) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty array = so.FindProperty(prop);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) {
                array.GetArrayElementAtIndex(i).floatValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
