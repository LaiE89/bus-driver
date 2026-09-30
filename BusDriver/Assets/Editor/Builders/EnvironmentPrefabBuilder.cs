using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Views;
using BusDriver.Gameplay.World;
using TMPro;
using UnityEditor;
using UnityEngine;
using static BusDriver.Editor.Builders.BuilderUtil;
using Object = UnityEngine.Object;

namespace BusDriver.Editor.Builders {
    // Part of BuildAll step 7 (T-M2-06): a logic prefab and a greybox view for every environment
    // kind (§3.3, §4.14, Appendix A.4), plus the greybox half of Data/Views/Environment.
    //   Logic   Generated/Prefabs/Environment/<Kind>.prefab: EnvironmentPiece, an empty View slot,
    //           and everything gameplay needs (colliders on World, BusStop, lights + LightFlicker)
    //   View    Generated/Prefabs/Environment/Views/View_<Kind>.prefab: renderers only (plus
    //           EmissiveView, TMP labels and a tree's LODGroup), never a collider or rigidbody
    // Place() is how scene builders put one in a scene: the logic prefab with its view (art if
    // assigned, else greybox) instantiated in the slot, and the lamp wired to the view's glow.
    public static class EnvironmentPrefabBuilder {
        public const string Folder = PrefabBuilder.Folder + "/Environment";
        public const string ViewFolder = Folder + "/Views";
        public const string ViewSlotName = "View";
        public const string LightAnchorName = "Anchor_Light";
        public const string StopLabelName = "Stop Label";

        // A.4: the stop boarding zone, stop-local (+X toward the kerb), kept clear of everything
        public const float StopClearMinX = 1.8f;
        public const float StopClearMaxX = 4.4f;
        public const float StopClearHalfLength = 12f;
        public const float BlockerWidth = 8f;

        public static string FileName(string kind) {
            return kind.Replace('.', '_');
        }

        public static string LogicPath(string kind) {
            return Folder + "/" + FileName(kind) + ".prefab";
        }

        public static string ViewPath(string kind) {
            return ViewFolder + "/View_" + FileName(kind) + ".prefab";
        }

        static Material M(string name) {
            return MaterialLibraryBuilder.Get(name);
        }

        [MenuItem("Tools/Bus Driver/Builders/Environment Prefabs")]
        public static void Build() {
            EnsureFolder(Folder);
            EnsureFolder(ViewFolder);
            foreach (BlockerKind kind in Enum.GetValues(typeof(BlockerKind))) {
                foreach (BlockerVariant variant in Enum.GetValues(typeof(BlockerVariant))) {
                    string key = EnvironmentKinds.Blocker(kind, variant);
                    SaveView(key, BlockerView(key, kind, variant));
                    SaveLogic(key, BlockerLogic);
                }
            }
            foreach (StopKind kind in Enum.GetValues(typeof(StopKind))) {
                string key = EnvironmentKinds.Stop(kind);
                SaveView(key, StopView(key, kind));
                SaveLogic(key, root => StopLogic(root, kind));
            }
            SaveView(EnvironmentKinds.StreetLamp, StreetLampView());
            SaveLogic(EnvironmentKinds.StreetLamp, StreetLampLogic);
            SaveView(EnvironmentKinds.TunnelLamp, TunnelLampView());
            SaveLogic(EnvironmentKinds.TunnelLamp, TunnelLampLogic);
            foreach (SignKind kind in Enum.GetValues(typeof(SignKind))) {
                string key = EnvironmentKinds.Sign(kind);
                SaveView(key, SignView(key, kind));
                SaveLogic(key, root => SignLogic(root, kind));
            }
            SaveView(EnvironmentKinds.GuardrailSegment, GuardrailView());
            SaveLogic(EnvironmentKinds.GuardrailSegment, null);
            SaveView(EnvironmentKinds.GuardrailEndCap, EndCapView());
            SaveLogic(EnvironmentKinds.GuardrailEndCap, null);
            for (int variant = 0; variant < EnvironmentKinds.TreeVariants; variant++) {
                string key = EnvironmentKinds.Tree(variant);
                SaveView(key, TreeView(key, MeshBuilder.Trees[variant]));
                SaveLogic(key, null);
            }
            SaveView(EnvironmentKinds.RockChunk, RockView());
            SaveLogic(EnvironmentKinds.RockChunk, null);
            SaveView(EnvironmentKinds.DepotBuilding, DepotView());
            SaveLogic(EnvironmentKinds.DepotBuilding, root => Solid(root, "Building Collider", new Vector3(0f, 3f, 0f), new Vector3(16f, 6f, 10f), false));
            SaveView(EnvironmentKinds.LodgeBuilding, LodgeView());
            SaveLogic(EnvironmentKinds.LodgeBuilding, root => Solid(root, "Building Collider", new Vector3(0f, 3.5f, 0f), new Vector3(22f, 7f, 12f), false));
            RefreshViewSet();
            AssetDatabase.SaveAssets();
        }

        // ================================================================ placing

        public static EnvironmentViewSet ViewSet {
            get {
                GameRootConfig config = AssetDatabase.LoadAssetAtPath<GameRootConfig>(GameRootConfig.AssetPath);
                EnvironmentViewSet set = config != null ? config.environment : null;
                if (set == null) {
                    set = AssetDatabase.LoadAssetAtPath<EnvironmentViewSet>(EnvironmentSeed.AssetPath);
                }
                if (set == null) {
                    throw new InvalidOperationException("no EnvironmentViewSet; DataSeeder runs before the scene builders");
                }
                return set;
            }
        }

        // The logic prefab at a pose, its view (art if assigned, else greybox) in the View slot, the
        // logic light moved onto the view's Anchor_Light, and the LightFlicker driving the view's
        // glow. `seed` != 0 gives the lamp its own flicker.
        public static GameObject Place(string kind, Transform parent, Vector3 position, Quaternion rotation, int seed = 0) {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LogicPath(kind));
            if (prefab == null) {
                throw new InvalidOperationException($"no environment prefab for '{kind}'; PrefabBuilder runs before the scene builders");
            }
            GameObject viewPrefab = ViewSet.Resolve(kind);
            if (viewPrefab == null) {
                throw new InvalidOperationException($"the EnvironmentViewSet has no view for '{kind}'");
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetPositionAndRotation(position, rotation);
            EnvironmentPiece piece = instance.GetComponent<EnvironmentPiece>();
            GameObject view = (GameObject)PrefabUtility.InstantiatePrefab(viewPrefab, piece.ViewSlot);
            view.transform.localPosition = Vector3.zero;
            view.transform.localRotation = Quaternion.identity;

            Transform logicLight = instance.transform.Find(LightAnchorName);
            Transform viewLight = FindDeep(view.transform, LightAnchorName);
            if (logicLight != null && viewLight != null) {
                logicLight.position = viewLight.position;
            }
            LightFlicker flicker = instance.GetComponent<LightFlicker>();
            if (flicker != null) {
                List<Object> glows = new List<Object>();
                foreach (MonoBehaviour behaviour in view.GetComponentsInChildren<MonoBehaviour>(true)) {
                    if (behaviour is IEmissiveView) {
                        glows.Add(behaviour);
                    }
                }
                SetRefArray(flicker, "emissiveViews", glows.ToArray());
                if (seed != 0) {
                    SetInt(flicker, "seed", seed);
                }
            }
            return instance;
        }

        static Transform FindDeep(Transform root, string name) {
            if (root.name == name) {
                return root;
            }
            foreach (Transform child in root) {
                Transform found = FindDeep(child, name);
                if (found != null) {
                    return found;
                }
            }
            return null;
        }

        // The builder owns every entry's greyboxView (§4.8 "generated", D76); artView is the artists'
        static void RefreshViewSet() {
            EnvironmentViewSet set = AssetDatabase.LoadAssetAtPath<EnvironmentViewSet>(EnvironmentSeed.AssetPath);
            if (set == null) {
                throw new InvalidOperationException("no " + EnvironmentSeed.AssetPath + "; DataSeeder runs before PrefabBuilder");
            }
            foreach (string kind in EnvironmentKinds.All()) {
                EnvironmentViewEntry entry = set.Find(kind);
                if (entry == null) {
                    entry = new EnvironmentViewEntry { kind = kind };
                    set.entries.Add(entry);
                }
                entry.greyboxView = AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath(kind));
            }
            EditorUtility.SetDirty(set);
        }

        // ================================================================ saving

        static void SaveView(string kind, GameObject root) {
            root.name = "View_" + FileName(kind);
            AddEmissiveView(root);
            MarkStatic(root);
            SaveOrOverwritePrefab(root, ViewPath(kind));
            Object.DestroyImmediate(root);
        }

        static void SaveLogic(string kind, Action<GameObject> addLogic) {
            GameObject root = new GameObject(FileName(kind));
            EnvironmentPiece piece = root.AddComponent<EnvironmentPiece>();
            Transform slot = Group(ViewSlotName, root.transform).transform;
            SetString(piece, "kind", kind);
            SetRef(piece, "viewSlot", slot);
            if (addLogic != null) {
                addLogic(root);
            }
            SaveOrOverwritePrefab(root, LogicPath(kind));
            Object.DestroyImmediate(root);
        }

        static bool IsEmissive(Material material) {
            return material != null && material.IsKeywordEnabled("_EMISSION");
        }

        // One EmissiveView over every glowing renderer, so a LightFlicker can drive the view
        static void AddEmissiveView(GameObject root) {
            List<Object> glowing = new List<Object>();
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
                if (IsEmissive(renderer.sharedMaterial)) {
                    glowing.Add(renderer);
                }
            }
            if (glowing.Count > 0) {
                EmissiveView view = root.AddComponent<EmissiveView>();
                SetRefArray(view, "renderers", glowing.ToArray());
            }
        }

        // Plain parts are static-batched; emissive props (magenta under static batching, §4.17),
        // text and instanced trees stay dynamic
        static void MarkStatic(GameObject root) {
            bool tree = root.GetComponent<LODGroup>() != null;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
                bool dynamic = tree || IsEmissive(renderer.sharedMaterial) || renderer.GetComponent<TMP_Text>() != null;
                renderer.gameObject.isStatic = !dynamic;
            }
        }

        // ================================================================ view parts

        static GameObject Part(Transform parent, string name, Vector3 position, Vector3 scale, string material, Vector3 euler = default(Vector3), PrimitiveType type = PrimitiveType.Cube) {
            GameObject part = Prim(type, name, parent, position, scale, M(material));
            part.transform.localRotation = Quaternion.Euler(euler);
            return part;
        }

        static GameObject MeshPart(Transform parent, string name, Mesh mesh, Material[] materials, Vector3 position, Vector3 scale) {
            GameObject part = Group(name, parent);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return part;
        }

        // A TMP label facing −Z (read by someone looking along +Z)
        static TextMeshPro Label(Transform parent, string name, string text, Vector3 position, Vector2 size, float fontSize, Color color, float yaw = 0f) {
            GameObject go = Group(name, parent);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            TextMeshPro label = go.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = size;
            return label;
        }

        static void Anchor(Transform parent, Vector3 position) {
            Node(parent, LightAnchorName, position);
        }

        static readonly Color Ink = new Color(0.05f, 0.05f, 0.05f);
        static readonly Color SignRed = new Color(0.7f, 0.08f, 0.06f);

        // ================================================================ logic parts

        // A collider on World; containment ones are tagged for the §3.5 test
        static BoxCollider Solid(GameObject root, string name, Vector3 center, Vector3 size, bool containment) {
            GameObject go = Group(name, root.transform);
            go.layer = Layers.World;
            if (containment) {
                go.tag = Tags.Containment;
            }
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            go.isStatic = true;
            return box;
        }

        static Light LampLight(GameObject root, Vector3 position, LightType type, float range, float intensity, FlickerMode mode) {
            GameObject anchor = Group(LightAnchorName, root.transform);
            anchor.transform.localPosition = position;
            Light light = anchor.AddComponent<Light>();
            light.type = type;
            light.range = range;
            light.intensity = intensity;
            light.color = new Color(1f, 0.8f, 0.55f);
            light.shadows = LightShadows.None;
            if (type == LightType.Spot) {
                anchor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                light.spotAngle = 120f;
                light.innerSpotAngle = 60f;
            }
            LightFlicker flicker = root.AddComponent<LightFlicker>();
            SetRefArray(flicker, "lights", new Object[] { light });
            SetInt(flicker, "mode", (int)mode);
            SetInt(flicker, "seed", Animator.StringToHash(root.name));
            return light;
        }

        // ================================================================ blockers

        // Across an 8 m stub, pivot at the stub centre, +Z facing the main road (A.4)
        static GameObject BlockerView(string kind, BlockerKind blocker, BlockerVariant variant) {
            GameObject root = new GameObject(kind);
            Transform t = root.transform;
            bool b = variant == BlockerVariant.B;
            switch (blocker) {
                case BlockerKind.FallenTree:
                    Log(t, "Trunk", new Vector3(0.2f, 0.45f, 0f), 10.5f, b ? 12f : 6f, 0.45f);
                    if (b) {
                        Log(t, "Second Trunk", new Vector3(-0.6f, 1.1f, -0.4f), 9f, -18f, 0.35f);
                    }
                    Part(t, "Root Ball", new Vector3(b ? 4.9f : -5f, 0.9f, 0.3f), new Vector3(1.4f, 1.8f, 1.6f), "Bark", default(Vector3), PrimitiveType.Sphere);
                    for (int i = 0; i < 4; i++) {
                        float x = -3f + i * 2f;
                        Part(t, "Needles " + i, new Vector3(x, 1f, (i % 2 == 0 ? 0.6f : -0.5f)), new Vector3(1.6f, 0.9f, 1.3f), "Foliage", default(Vector3), PrimitiveType.Sphere);
                    }
                    break;
                case BlockerKind.FenceRoadClosed:
                    if (!b) {
                        for (int i = 0; i < 5; i++) {
                            Part(t, "Post " + i, new Vector3(-4f + i * 2f, 0.6f, 0f), new Vector3(0.12f, 1.2f, 0.12f), "Wood");
                        }
                        for (int i = 0; i < 2; i++) {
                            Part(t, "Rail " + i, new Vector3(0f, 0.45f + i * 0.5f, 0.08f), new Vector3(8.2f, 0.15f, 0.05f), i == 0 ? "BarrierRed" : "SignWhite");
                        }
                        ClosedBoard(t, new Vector3(0f, 1.55f, 0.1f));
                    }else {
                        foreach (float x in new[] { -2.2f, 2.2f }) {
                            Part(t, "Barricade Top", new Vector3(x, 1f, 0f), new Vector3(3.2f, 0.3f, 0.06f), "BarrierRed");
                            Part(t, "Barricade Lower", new Vector3(x, 0.55f, 0f), new Vector3(3.2f, 0.2f, 0.06f), "SignWhite");
                            foreach (float leg in new[] { -1.3f, 1.3f }) {
                                Part(t, "Leg", new Vector3(x + leg, 0.55f, 0f), new Vector3(0.08f, 1.1f, 0.5f), "Wood", new Vector3(0f, 0f, leg > 0f ? -8f : 8f));
                            }
                        }
                        ClosedBoard(t, new Vector3(0f, 1.6f, 0.05f));
                    }
                    break;
                case BlockerKind.ConcreteBarriers:
                    for (int i = 0; i < 4; i++) {
                        float x = -3f + i * 2f;
                        float z = b ? (i % 2 == 0 ? 0.4f : -0.4f) : 0f;
                        float yaw = b && i == 3 ? 20f : 0f;
                        Transform barrier = Group("Barrier " + i, t).transform;
                        barrier.localPosition = new Vector3(x, 0f, z);
                        barrier.localRotation = Quaternion.Euler(0f, yaw, 0f);
                        Part(barrier, "Base", new Vector3(0f, 0.4f, 0f), new Vector3(1.95f, 0.8f, 0.6f), "Concrete");
                        Part(barrier, "Top", new Vector3(0f, 0.9f, 0f), new Vector3(1.95f, 0.2f, 0.3f), "Concrete");
                        Part(barrier, "Reflector", new Vector3(0f, 0.7f, 0.31f), new Vector3(0.3f, 0.1f, 0.02f), "Reflector");
                    }
                    break;
                case BlockerKind.CollapsedBridge:
                    Part(t, "Deck", new Vector3(0f, b ? -1.4f : -0.9f, -3.2f), new Vector3(7.6f, 0.4f, 6f), "Concrete", new Vector3(b ? -24f : -14f, b ? 6f : 0f, 0f));
                    if (b) {
                        Part(t, "Fragment", new Vector3(2.2f, -0.6f, -1f), new Vector3(3f, 0.35f, 2.5f), "Concrete", new Vector3(-8f, 20f, 12f));
                    }
                    foreach (float x in new[] { -3.8f, 3.8f }) {
                        Part(t, "Railing Post", new Vector3(x, 0.5f, 0f), new Vector3(0.12f, 1f, 0.12f), "Metal");
                        Part(t, "Broken Rail", new Vector3(x, 0.9f, -1f), new Vector3(0.08f, 0.08f, 2f), "Metal", new Vector3(18f, 0f, 0f));
                    }
                    Part(t, "Bar", new Vector3(0f, 0.9f, 0.2f), new Vector3(7.6f, 0.15f, 0.08f), "BarrierRed");
                    WarningBoard(t, new Vector3(0f, 1.6f, 0.3f), "BRIDGE OUT");
                    break;
                case BlockerKind.Gate:
                    foreach (float x in new[] { -4f, 4f }) {
                        Part(t, "Gate Post", new Vector3(x, 0.8f, 0f), new Vector3(0.25f, 1.6f, 0.25f), "Metal");
                    }
                    if (!b) {
                        for (int i = 0; i < 3; i++) {
                            Part(t, "Gate Bar " + i, new Vector3(0f, 0.4f + i * 0.4f, 0f), new Vector3(7.7f, 0.07f, 0.07f), "Metal");
                        }
                        Part(t, "Gate Brace", new Vector3(0f, 0.8f, 0f), new Vector3(8.4f, 0.07f, 0.07f), "Metal", new Vector3(0f, 0f, 10f));
                        WarningBoard(t, new Vector3(0f, 1.25f, 0.08f), "PRIVATE");
                    }else {
                        Part(t, "Chain Left", new Vector3(-2f, 0.85f, 0f), new Vector3(4.1f, 0.04f, 0.04f), "Metal", new Vector3(0f, 0f, -5f));
                        Part(t, "Chain Right", new Vector3(2f, 0.85f, 0f), new Vector3(4.1f, 0.04f, 0.04f), "Metal", new Vector3(0f, 0f, 5f));
                        WarningBoard(t, new Vector3(0f, 0.55f, 0.05f), "NO ENTRY");
                    }
                    break;
            }
            return root;
        }

        // A cylinder lying along X
        static void Log(Transform parent, string name, Vector3 position, float length, float yaw, float radius) {
            Part(parent, name, position, new Vector3(radius * 2f, length * 0.5f, radius * 2f), "Bark", new Vector3(0f, yaw, 90f), PrimitiveType.Cylinder);
        }

        static void ClosedBoard(Transform parent, Vector3 position) {
            Part(parent, "Closed Board", position, new Vector3(2.4f, 0.6f, 0.04f), "SignWhite");
            Label(parent, "Closed Label", "ROAD CLOSED", position + new Vector3(0f, 0f, 0.03f), new Vector2(2.3f, 0.5f), 3f, SignRed, 180f);
        }

        static void WarningBoard(Transform parent, Vector3 position, string text) {
            Part(parent, "Warning Board", position, new Vector3(1.6f, 0.45f, 0.04f), "SignYellow");
            Label(parent, "Warning Label", text, position + new Vector3(0f, 0f, 0.03f), new Vector2(1.5f, 0.4f), 2.4f, Ink, 180f);
        }

        static void BlockerLogic(GameObject root) {
            // Across the whole stub; part of the containment line at the stub's end (§3.3, §3.5)
            Solid(root, "Blocker Collider", new Vector3(0f, 1.25f, 0f), new Vector3(BlockerWidth + 0.4f, 2.5f, 1.2f), true);
        }

        // ================================================================ stops

        struct StopLook {
            public bool Shelter;
            public float ShelterLength;
            public float PadLength;
            public string ShelterMaterial;
            public string Name;
        }

        static StopLook LookOf(StopKind kind) {
            StopLook look = new StopLook { Shelter = true, ShelterLength = 4f, PadLength = 12f, ShelterMaterial = "Wall" };
            switch (kind) {
                case StopKind.Depot: look.Name = "TOWN DEPOT"; look.PadLength = 20f; look.ShelterLength = 6f; break;
                case StopKind.FarmGate: look.Name = "MILL ROAD FARM"; look.Shelter = false; break;
                case StopKind.GasStation: look.Name = "PINECREST GAS"; break;
                case StopKind.Campground: look.Name = "HOLLOW CREEK"; look.ShelterMaterial = "Wood"; break;
                case StopKind.Church: look.Name = "ST. AGNES"; break;
                case StopKind.Clinic: look.Name = "RIDGE CLINIC"; break;
                case StopKind.Trailhead: look.Name = "SUMMIT TRAILHEAD"; look.ShelterMaterial = "Wood"; break;
                case StopKind.Terminus: look.Name = "SUMMIT LODGE"; look.ShelterLength = 8f; break;
            }
            return look;
        }

        // Stop-local: pivot on the road centreline, +X toward the kerb, x 1.8–4.4 clear for ±12 m (A.4)
        public static readonly Vector3 StopSignPole = new Vector3(5f, 0f, 5f);
        public static readonly Vector3 StopLampPole = new Vector3(5.6f, 0f, -4f);
        public static readonly Vector3 StopLampHead = new Vector3(4.6f, 5.45f, -4f);
        const float ShelterBackX = 6.9f;

        static GameObject StopView(string key, StopKind kind) {
            StopLook look = LookOf(kind);
            GameObject root = new GameObject(key);
            Transform t = root.transform;
            // The boarding zone marking, flat on the ground
            Part(t, "Pad", new Vector3((StopClearMinX + StopClearMaxX) * 0.5f, 0.02f, 0f), new Vector3(StopClearMaxX - StopClearMinX, 0.012f, look.PadLength), "StopPad");

            Part(t, "Sign Pole", StopSignPole + new Vector3(0f, 1.3f, 0f), new Vector3(0.08f, 2.6f, 0.08f), "Post");
            Part(t, "Sign", StopSignPole + new Vector3(0f, 2.35f, 0f), new Vector3(0.7f, 0.5f, 0.05f), "SignYellow");
            Label(t, "Sign Label", "BUS", StopSignPole + new Vector3(0f, 2.35f, -0.04f), new Vector2(0.65f, 0.45f), 2.6f, Ink);
            Label(t, StopLabelName, look.Name, StopSignPole + new Vector3(0f, 1.85f, -0.04f), new Vector2(2f, 0.3f), 1.4f, Color.white);

            Part(t, "Lamp Pole", StopLampPole + new Vector3(0f, 2.75f, 0f), new Vector3(0.15f, 5.5f, 0.15f), "Post");
            Part(t, "Lamp Arm", new Vector3((StopLampPole.x + StopLampHead.x) * 0.5f, 5.52f, StopLampPole.z), new Vector3(StopLampPole.x - StopLampHead.x, 0.08f, 0.08f), "Post");
            Part(t, "Lamp Head", StopLampHead, new Vector3(0.4f, 0.12f, 0.4f), "LampWhite");
            Anchor(t, StopLampHead - new Vector3(0f, 0.1f, 0f));

            if (look.Shelter) {
                float half = look.ShelterLength * 0.5f;
                Part(t, "Shelter Back", new Vector3(ShelterBackX, 1.2f, 0f), new Vector3(0.1f, 2.4f, look.ShelterLength), look.ShelterMaterial);
                Part(t, "Shelter Roof", new Vector3(6.2f, 2.45f, 0f), new Vector3(1.6f, 0.1f, look.ShelterLength + 0.2f), look.ShelterMaterial);
                Part(t, "Shelter Side A", new Vector3(6.2f, 1.2f, -half), new Vector3(1.3f, 2.2f, 0.06f), look.ShelterMaterial);
                Part(t, "Shelter Side B", new Vector3(6.2f, 1.2f, half), new Vector3(1.3f, 2.2f, 0.06f), look.ShelterMaterial);
                Part(t, "Bench", new Vector3(6.5f, 0.45f, 0f), new Vector3(0.45f, 0.08f, look.ShelterLength - 1.2f), "Wood");
            }
            StopDressing(t, kind, look);
            return root;
        }

        // A little of each kind's character, all behind the kerb-clear strip
        static void StopDressing(Transform t, StopKind kind, StopLook look) {
            switch (kind) {
                case StopKind.FarmGate:
                    for (float z = -11f; z <= 11f; z += 2.2f) {
                        if (Mathf.Abs(z) < 1.2f) {
                            continue;
                        }
                        Part(t, "Fence Post", new Vector3(7f, 0.6f, z), new Vector3(0.12f, 1.2f, 0.12f), "Wood");
                    }
                    Part(t, "Fence Rail South", new Vector3(7f, 0.8f, -6.1f), new Vector3(0.06f, 0.12f, 9.8f), "Wood");
                    Part(t, "Fence Rail North", new Vector3(7f, 0.8f, 6.1f), new Vector3(0.06f, 0.12f, 9.8f), "Wood");
                    Part(t, "Farm Gate", new Vector3(8.1f, 0.7f, 1.1f), new Vector3(2.2f, 0.9f, 0.06f), "Wood", new Vector3(0f, -70f, 0f));
                    Part(t, "Mailbox Post", new Vector3(5.3f, 0.55f, 2.5f), new Vector3(0.08f, 1.1f, 0.08f), "Post");
                    Part(t, "Mailbox", new Vector3(5.3f, 1.2f, 2.5f), new Vector3(0.25f, 0.25f, 0.45f), "Metal");
                    break;
                case StopKind.GasStation:
                    Part(t, "Price Sign Pole", new Vector3(7.2f, 2f, -9f), new Vector3(0.15f, 4f, 0.15f), "Post");
                    Part(t, "Price Sign", new Vector3(7.2f, 4.4f, -9f), new Vector3(0.1f, 1.1f, 1.8f), "Window");
                    Label(t, "Gas Label", "GAS", new Vector3(7.13f, 4.4f, -9f), new Vector2(1.6f, 0.9f), 6f, Ink, 90f);
                    Part(t, "Pump", new Vector3(7.1f, 0.8f, 8.5f), new Vector3(0.6f, 1.6f, 0.9f), "Metal");
                    break;
                case StopKind.Campground:
                    BoardOnPosts(t, new Vector3(7f, 0f, -9f), "CAMPGROUND", "Wood");
                    break;
                case StopKind.Church:
                    Part(t, "Cross Post", new Vector3(7.1f, 1.3f, -9f), new Vector3(0.14f, 2.6f, 0.14f), "Wood");
                    Part(t, "Cross Bar", new Vector3(7.1f, 2f, -9f), new Vector3(0.14f, 0.14f, 0.9f), "Wood");
                    break;
                case StopKind.Clinic:
                    Part(t, "Clinic Sign Pole", new Vector3(7.1f, 1.1f, -9f), new Vector3(0.1f, 2.2f, 0.1f), "Post");
                    Part(t, "Clinic Sign", new Vector3(7.1f, 2.4f, -9f), new Vector3(0.08f, 0.8f, 0.8f), "SignWhite");
                    Part(t, "Cross Vertical", new Vector3(7.05f, 2.4f, -9f), new Vector3(0.02f, 0.55f, 0.16f), "BarrierRed");
                    Part(t, "Cross Horizontal", new Vector3(7.05f, 2.4f, -9f), new Vector3(0.02f, 0.16f, 0.55f), "BarrierRed");
                    break;
                case StopKind.Trailhead:
                    BoardOnPosts(t, new Vector3(6.9f, 0f, -9f), "TRAIL MAP", "Wood");
                    break;
                case StopKind.Terminus:
                case StopKind.Depot:
                    BoardOnPosts(t, new Vector3(7f, 0f, -look.ShelterLength * 0.5f - 3f), look.Name, "Wood");
                    break;
            }
        }

        // A wide board on two posts, facing the road (−X)
        static void BoardOnPosts(Transform t, Vector3 basePos, string text, string material) {
            Part(t, "Board Post A", basePos + new Vector3(0f, 0.9f, -1f), new Vector3(0.12f, 1.8f, 0.12f), material);
            Part(t, "Board Post B", basePos + new Vector3(0f, 0.9f, 1f), new Vector3(0.12f, 1.8f, 0.12f), material);
            Part(t, "Board", basePos + new Vector3(0f, 1.5f, 0f), new Vector3(0.08f, 0.7f, 2.2f), "SignWhite");
            Label(t, "Board Label", text, basePos + new Vector3(-0.05f, 1.5f, 0f), new Vector2(2.1f, 0.6f), 2.2f, Ink, 90f);
        }

        static void StopLogic(GameObject root, StopKind kind) {
            StopLook look = LookOf(kind);
            root.AddComponent<BusStop>();
            LampLight(root, StopLampHead - new Vector3(0f, 0.1f, 0f), LightType.Spot, 16f, 35f, FlickerMode.Subtle);
            Solid(root, "Sign Pole Collider", StopSignPole + new Vector3(0f, 1.3f, 0f), new Vector3(0.1f, 2.6f, 0.1f), false);
            Solid(root, "Lamp Pole Collider", StopLampPole + new Vector3(0f, 2.75f, 0f), new Vector3(0.15f, 5.5f, 0.15f), false);
            if (look.Shelter) {
                Solid(root, "Shelter Collider", new Vector3(ShelterBackX, 1.2f, 0f), new Vector3(0.1f, 2.4f, look.ShelterLength), false);
            }
        }

        // ================================================================ lamps

        // Pivot at the pole's base; the arm reaches along +Z (turn +Z toward the road)
        static readonly Vector3 StreetLampHead = new Vector3(0f, 5.45f, 1.2f);

        static GameObject StreetLampView() {
            GameObject root = new GameObject(EnvironmentKinds.StreetLamp);
            Transform t = root.transform;
            Part(t, "Pole", new Vector3(0f, 2.75f, 0f), new Vector3(0.15f, 5.5f, 0.15f), "Post");
            Part(t, "Arm", new Vector3(0f, 5.52f, 0.6f), new Vector3(0.08f, 0.08f, 1.2f), "Post");
            Part(t, "Head", StreetLampHead, new Vector3(0.4f, 0.12f, 0.4f), "LampWhite");
            Anchor(t, StreetLampHead - new Vector3(0f, 0.1f, 0f));
            return root;
        }

        static void StreetLampLogic(GameObject root) {
            LampLight(root, StreetLampHead - new Vector3(0f, 0.1f, 0f), LightType.Spot, 16f, 35f, FlickerMode.Subtle);
            Solid(root, "Pole Collider", new Vector3(0f, 2.75f, 0f), new Vector3(0.15f, 5.5f, 0.15f), false);
        }

        // Pivot at the fixture on the ceiling; +Z along the tunnel. 8 m range (§4.17)
        static GameObject TunnelLampView() {
            GameObject root = new GameObject(EnvironmentKinds.TunnelLamp);
            Transform t = root.transform;
            Part(t, "Bracket", new Vector3(0f, 0.06f, 0f), new Vector3(0.3f, 0.06f, 1.3f), "Metal");
            Part(t, "Fixture", new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.06f, 1.2f), "LampWhite");
            Anchor(t, new Vector3(0f, -0.25f, 0f));
            return root;
        }

        static void TunnelLampLogic(GameObject root) {
            LampLight(root, new Vector3(0f, -0.25f, 0f), LightType.Point, 8f, 4f, FlickerMode.Unstable);
        }

        // ================================================================ signs

        // Pivot at the post's base; the face looks along −Z, at the traffic it warns (+Z = route direction)
        static GameObject SignView(string key, SignKind kind) {
            GameObject root = new GameObject(key);
            Transform t = root.transform;
            if (kind == SignKind.Chevron) {
                // Low, no collider (§3.1): a dark board with a yellow chevron pointing into the bend
                Part(t, "Post", new Vector3(0f, 0.55f, 0f), new Vector3(0.08f, 1.1f, 0.08f), "Post");
                Part(t, "Board", new Vector3(0f, 1.3f, 0f), new Vector3(0.65f, 0.8f, 0.03f), "Post");
                MeshPart(t, "Chevron", MeshBuilder.Get("Chevron"), new[] { M("SignYellow") }, new Vector3(0f, 1.3f, -0.02f), Vector3.one);
                return root;
            }
            Part(t, "Post", new Vector3(0f, 1.1f, 0f), new Vector3(0.08f, 2.2f, 0.08f), "Post");
            Vector3 face = new Vector3(0f, 2.15f, -0.06f);
            switch (kind) {
                case SignKind.BridgeAhead:
                    Diamond(t, "BRIDGE\nAHEAD");
                    break;
                case SignKind.TunnelAhead:
                    Diamond(t, "TUNNEL\nAHEAD");
                    break;
                case SignKind.SharpCurveRight:
                    Diamond(t, null);
                    MeshPart(t, "Arrow", MeshBuilder.Get("Chevron"), new[] { M("Post") }, face + new Vector3(0f, 0f, -0.02f), new Vector3(0.8f, 0.8f, 1f));
                    Part(t, "Plate", new Vector3(0f, 1.45f, -0.03f), new Vector3(0.9f, 0.28f, 0.03f), "SignYellow");
                    Label(t, "Plate Label", "SHARP CURVE", new Vector3(0f, 1.45f, -0.06f), new Vector2(0.85f, 0.25f), 1.3f, Ink);
                    break;
                case SignKind.NoGuardrailAhead:
                    Part(t, "Board", new Vector3(0f, 2.15f, -0.03f), new Vector3(1.1f, 0.75f, 0.03f), "SignWhite");
                    Label(t, "Label", "NO\nGUARDRAIL", face, new Vector2(1f, 0.7f), 1.9f, SignRed);
                    break;
                case SignKind.StopSign:
                    Part(t, "Board", new Vector3(0f, 2.15f, -0.03f), new Vector3(0.6f, 0.75f, 0.03f), "SignWhite");
                    Label(t, "Label", "BUS\nSTOP", face, new Vector2(0.55f, 0.7f), 2f, Ink);
                    break;
                case SignKind.RoadClosed:
                    // The night's end barrier (§2.4, D43): a wide board on two posts, low enough to
                    // sit in the headlights behind the concrete barriers
                    UnityEngine.Object.DestroyImmediate(t.Find("Post").gameObject);
                    foreach (float x in new[] { -1.3f, 1.3f }) {
                        Part(t, "Post", new Vector3(x, 0.9f, 0f), new Vector3(0.1f, 1.8f, 0.1f), "Post");
                    }
                    Part(t, "Board", new Vector3(0f, 1.55f, -0.03f), new Vector3(3f, 0.7f, 0.04f), "SignWhite");
                    Part(t, "Stripe", new Vector3(0f, 1.12f, -0.03f), new Vector3(3f, 0.16f, 0.04f), "BarrierRed");
                    Label(t, "Label", "ROAD CLOSED", new Vector3(0f, 1.55f, -0.06f), new Vector2(2.9f, 0.6f), 3.4f, SignRed);
                    break;
            }
            return root;
        }

        static void Diamond(Transform t, string text) {
            Part(t, "Board", new Vector3(0f, 2.15f, -0.03f), new Vector3(0.75f, 0.75f, 0.03f), "SignYellow", new Vector3(0f, 0f, 45f));
            if (text != null) {
                Label(t, "Label", text, new Vector3(0f, 2.15f, -0.06f), new Vector2(0.7f, 0.5f), 1.5f, Ink);
            }
        }

        static void SignLogic(GameObject root, SignKind kind) {
            if (kind == SignKind.RoadClosed) {
                Solid(root, "Board Collider", new Vector3(0f, 0.9f, 0f), new Vector3(2.8f, 1.8f, 0.12f), false);
                return;
            }
            if (kind != SignKind.Chevron) {
                Solid(root, "Post Collider", new Vector3(0f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 0.1f), false);
            }
        }

        // ================================================================ guardrail

        // A 4 m segment: pivot at its start, +Z along the rail, a 0.8 m rail top (§3.4, A.4). The
        // collision is the profile's continuous rail and wall (ProfileBuilder); these are looks only.
        static GameObject GuardrailView() {
            GameObject root = new GameObject(EnvironmentKinds.GuardrailSegment);
            Transform t = root.transform;
            Part(t, "Rail", new Vector3(0f, 0.65f, 2f), new Vector3(0.06f, 0.3f, 4f), "GuardRail");
            Part(t, "Post", new Vector3(0f, 0.35f, 0.05f), new Vector3(0.12f, 0.7f, 0.12f), "Post");
            Part(t, "Mid Post", new Vector3(0f, 0.35f, 2f), new Vector3(0.12f, 0.7f, 0.12f), "Post");
            Part(t, "Reflector", new Vector3(0f, 0.72f, 2f), new Vector3(0.08f, 0.08f, 0.08f), "Reflector");
            return root;
        }

        // The rail's end, flaring down to the ground along +Z (ProfileBuilder turns start caps around)
        static GameObject EndCapView() {
            GameObject root = new GameObject(EnvironmentKinds.GuardrailEndCap);
            Transform t = root.transform;
            Part(t, "Flare", new Vector3(0f, 0.5f, 0.45f), new Vector3(0.06f, 0.3f, 1f), "GuardRail", new Vector3(22f, 0f, 0f));
            Part(t, "End Post", new Vector3(0f, 0.35f, 0f), new Vector3(0.12f, 0.7f, 0.12f), "Post");
            return root;
        }

        // ================================================================ nature

        static GameObject TreeView(string key, MeshBuilder.TreeShape shape) {
            GameObject root = new GameObject(key);
            Material[] materials = { M("Bark"), M("Foliage") };
            GameObject lod0 = MeshPart(root.transform, "LOD0", MeshBuilder.Get(shape.Name + "_LOD0"), materials, Vector3.zero, Vector3.one);
            GameObject lod1 = MeshPart(root.transform, "LOD1", MeshBuilder.Get(shape.Name + "_LOD1"), materials, Vector3.zero, Vector3.one);
            // Only the lowest-detail level casts shadows it can afford; trees are far from the headlights
            lod1.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            LODGroup group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] {
                new LOD(0.15f, new Renderer[] { lod0.GetComponent<MeshRenderer>() }),
                // Culled below 3 % of the screen height (§4.17)
                new LOD(0.03f, new Renderer[] { lod1.GetComponent<MeshRenderer>() }),
            });
            group.RecalculateBounds();
            return root;
        }

        static GameObject RockView() {
            GameObject root = new GameObject(EnvironmentKinds.RockChunk);
            MeshPart(root.transform, "Rock", MeshBuilder.Get("RockChunk"), new[] { M("Rock") }, Vector3.zero, Vector3.one * 1.5f);
            return root;
        }

        // ================================================================ buildings

        // Pivot at the ground centre; the front faces +Z (turn it toward the road)
        static GameObject DepotView() {
            GameObject root = new GameObject(EnvironmentKinds.DepotBuilding);
            Transform t = root.transform;
            Part(t, "Shed", new Vector3(0f, 3f, 0f), new Vector3(16f, 6f, 10f), "Building");
            Part(t, "Roof Edge", new Vector3(0f, 6.1f, 0f), new Vector3(16.4f, 0.2f, 10.4f), "Wall");
            Part(t, "Bay Door A", new Vector3(-3.5f, 2.1f, 5.02f), new Vector3(4.2f, 4.2f, 0.05f), "Door");
            Part(t, "Bay Door B", new Vector3(2f, 2.1f, 5.02f), new Vector3(4.2f, 4.2f, 0.05f), "Door");
            Part(t, "Office Window", new Vector3(6.2f, 2.2f, 5.03f), new Vector3(1.8f, 1f, 0.05f), "Window");
            Label(t, "Depot Label", "HOLLOW PINES DEPOT", new Vector3(0f, 5.1f, 5.06f), new Vector2(12f, 1f), 8f, Color.white, 180f);
            return root;
        }

        static GameObject LodgeView() {
            GameObject root = new GameObject(EnvironmentKinds.LodgeBuilding);
            Transform t = root.transform;
            Part(t, "Lodge", new Vector3(0f, 3.5f, 0f), new Vector3(22f, 7f, 12f), "Wood");
            // A pitched roof: a cube turned 45° about X, half of it sunk into the walls
            Part(t, "Roof", new Vector3(0f, 7f, 0f), new Vector3(22.6f, 6.4f, 6.4f), "Wall", new Vector3(45f, 0f, 0f));
            Part(t, "Door", new Vector3(0f, 1.2f, 6.02f), new Vector3(1.8f, 2.4f, 0.05f), "Door");
            for (int i = 0; i < 6; i++) {
                float x = -8.5f + i * 3.4f;
                if (Mathf.Abs(x) < 1.5f) {
                    continue;
                }
                Part(t, "Window " + i, new Vector3(x, 2f, 6.03f), new Vector3(1.4f, 1.1f, 0.05f), "Window");
                Part(t, "Upper Window " + i, new Vector3(x, 5f, 6.03f), new Vector3(1.4f, 1.1f, 0.05f), "Window");
            }
            Label(t, "Lodge Label", "SUMMIT LODGE", new Vector3(0f, 3.4f, 6.06f), new Vector2(10f, 1f), 7f, Color.white, 180f);
            return root;
        }
    }
}
