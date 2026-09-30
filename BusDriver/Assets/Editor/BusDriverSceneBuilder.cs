using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.World;
using BusDriver.UI.Hud;
using BusDriver.UI.Screens;

namespace BusDriver.Editor.Builders {
    // Generates the MVP scene (Assets/Scenes/BusRoute.unity) from primitives.
    // The scene is rebuilt from scratch on every run, so HAND EDITS TO IT ARE LOST.
    // Tune handling through Assets/Settings/BusTuning.asset, which is never overwritten,
    // and change layout numbers here.
    public static class BusDriverSceneBuilder {
        public const string ScenePath = "Assets/Scenes/BusRoute.unity";
        const string MenuScenePath = "Assets/Scenes/Menu.unity";
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        const string MapMaterialFolder = "Assets/Materials/Map";
        const string NpcMaterialFolder = "Assets/Materials/NPCs";
        const string NpcPrefabFolder = "Assets/Prefabs/NPCs";
        const string LevelPrefabFolder = "Assets/Prefabs/Level Essentials";
        const int UILayer = 5;

        // Road
        const float RoadWidth = 8f;
        const float KerbWidth = 0.3f;
        const float KerbHeight = 0.15f;
        const float ArcStepDegrees = 5f;

        static readonly Color FogColor = new Color(0.02f, 0.025f, 0.04f);

        struct RoadSample {
            public Vector3 pos;
            public float heading;
            public float distance;
        }

        struct RoadArc {
            public Vector3 center;
            public float radius;
            public float sign;
            public float midHeading;
        }

        static Dictionary<string, Material> mats;
        static GameObject passengerPrefab;
        static GameObject monsterPrefab;
        static List<BusStop> busStops;
        static List<RoadSample> samples;
        static List<RoadArc> arcs;
        static Vector3 turtlePos;
        static float turtleHeading;
        static float turtleDistance;

        [MenuItem("Tools/Bus Driver/Build MVP Scene")]
        public static void BuildScene() {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }
            EnsureFolder("Assets/Materials");
            EnsureFolder(MapMaterialFolder);
            EnsureFolder(NpcMaterialFolder);
            EnsureFolder("Assets/Settings");
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(NpcPrefabFolder);
            EnsureFolder(LevelPrefabFolder);
            DeleteGreyboxFolders();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateMaterials();
            BuildNpcPrefabs();

            BuildEnvironment();
            GameObject bus = InstantiateBus();
            BuildLighting();
            BuildHUDAndSystems(bus);

            EditorSceneManager.SaveScene(scene, ScenePath);
            FixBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Bus Driver MVP scene built at " + ScenePath);
        }

        [MenuItem("Tools/Bus Driver/Fix Build Settings")]
        public static void FixBuildSettings() {
            if (!File.Exists(ScenePath)) {
                Debug.LogWarning(ScenePath + " does not exist yet, build the MVP scene first");
                return;
            }
            // MainMenu loads build index 1 and the pause flow loads "Menu" by name
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene> {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes) {
                if (existing.path != MenuScenePath && existing.path != ScenePath && existing.path != SampleScenePath) {
                    scenes.Add(existing);
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------------------------------------------------------- assets

        static void EnsureFolder(string path) {
            if (AssetDatabase.IsValidFolder(path)) {
                return;
            }
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        static void DeleteGreyboxFolders() {
            string[] greyboxFolders = {
                "Assets/Materials/Greybox",
                "Assets/Prefabs/Greybox"
            };
            foreach (string folder in greyboxFolders) {
                if (!AssetDatabase.IsValidFolder(folder)) {
                    continue;
                }
                if (!AssetDatabase.DeleteAsset(folder)) {
                    Debug.LogWarning("BusDriverSceneBuilder: failed to delete " + folder);
                }
            }
            AssetDatabase.Refresh();
        }

        static void CreateMaterials() {
            mats = new Dictionary<string, Material>();
            AddMapMaterial("Ground", new Color(0.07f, 0.09f, 0.07f));
            AddMapMaterial("Asphalt", new Color(0.13f, 0.13f, 0.14f));
            AddMapMaterial("Kerb", new Color(0.5f, 0.5f, 0.5f));
            AddMapMaterial("LinePaint", new Color(0.9f, 0.9f, 0.85f), new Color(0.9f, 0.9f, 0.8f) * 0.6f);
            AddMapMaterial("Post", new Color(0.15f, 0.15f, 0.15f));
            AddMapMaterial("Reflector", new Color(1f, 0.6f, 0.1f), new Color(1f, 0.55f, 0.1f) * 1.5f);
            AddMapMaterial("StopPad", new Color(0.8f, 0.7f, 0.1f), new Color(0.8f, 0.7f, 0.1f) * 0.5f);
            AddMapMaterial("Wall", new Color(0.3f, 0.28f, 0.27f));
            AddMapMaterial("Building", new Color(0.18f, 0.18f, 0.2f));
            AddMapMaterial("Obstacle", new Color(0.45f, 0.3f, 0.2f));
            AddMapMaterial("BusBody", new Color(0.6f, 0.58f, 0.5f));
            AddMapMaterial("BusInterior", new Color(0.35f, 0.36f, 0.38f));
            AddMapMaterial("Seat", new Color(0.15f, 0.2f, 0.4f));
            AddMapMaterial("Dash", new Color(0.08f, 0.08f, 0.09f));
            AddMapMaterial("Tyre", new Color(0.04f, 0.04f, 0.04f));
            AddMapMaterial("LampWhite", Color.white, new Color(1f, 0.95f, 0.8f) * 3f);
            AddMapMaterial("LampRed", new Color(0.8f, 0.05f, 0.05f), new Color(1f, 0.05f, 0.05f) * 2f);
            AddMapMaterial("Door", new Color(0.3f, 0.33f, 0.36f));

            AddNpcMaterial("Passenger", new Color(0.65f, 0.55f, 0.5f));
            AddNpcMaterial("PassengerOdd", new Color(0.25f, 0.2f, 0.22f));
            AddNpcMaterial("Player", new Color(0.35f, 0.55f, 0.85f));
            AddNpcMaterial("Face", new Color(0.04f, 0.04f, 0.04f));
        }

        static void AddMapMaterial(string name, Color baseColor, Color? emission = null) {
            AddMaterial(MapMaterialFolder, name, baseColor, emission);
        }

        static void AddNpcMaterial(string name, Color baseColor, Color? emission = null) {
            AddMaterial(NpcMaterialFolder, name, baseColor, emission);
        }

        static void AddMaterial(string folder, string name, Color baseColor, Color? emission = null) {
            string path = folder + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null && GraphicsSettings.currentRenderPipeline != null) {
                    shader = GraphicsSettings.currentRenderPipeline.defaultShader;
                }
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", 0.15f);
            if (emission.HasValue) {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                // Anything else and the URP material inspector switches emission back off
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            EditorUtility.SetDirty(mat);
            mats[name] = mat;
        }

        static void BuildNpcPrefabs() {
            passengerPrefab = BuildPassengerPrefab<Passenger>("Passenger", "Passenger");
            // Looks like everyone else on purpose, the head is the only tell
            // monsterPrefab = BuildPassengerPrefab<StaringMonster>("StaringMonster", "Passenger");
            monsterPrefab = BuildPassengerPrefab<WeepingAngel>("WeepingAngel", "Passenger");
        }

        // Root at the feet. Passenger.SetPose moves the body and head between standing and seated.
        static GameObject BuildPassengerPrefab<T>(string name, string material) where T : Passenger {
            GameObject root = new GameObject(name);
            T passenger = root.AddComponent<T>();

            // Only there for the player's interaction ray, and only enabled while the bus is walkable
            CapsuleCollider reach = root.AddComponent<CapsuleCollider>();
            reach.isTrigger = true;
            reach.center = new Vector3(0f, 0.65f, 0f);
            reach.radius = 0.32f;
            reach.height = 1.4f;
            reach.enabled = false;

            GameObject body = Prim(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.42f, 0.75f, 0.42f), material);
            GameObject head = Prim(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.62f, 0f), Vector3.one * 0.26f, material);
            // Without a face nobody could tell which way a sphere is looking
            Box("Face", head.transform, new Vector3(0f, 0.08f, 0.46f), new Vector3(0.6f, 0.2f, 0.15f), "Face");

            SetRef(passenger, "head", head.transform);
            SetRef(passenger, "body", body.transform);
            SetRef(passenger, "interactCollider", reach);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, NpcPrefabFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject SpawnPassenger(GameObject prefab, string name, Transform parent, Vector3 localPos, Quaternion localRot) {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPos;
            instance.transform.localRotation = localRot;
            return instance;
        }

        // --------------------------------------------------------------- helpers

        static GameObject Group(string name, Transform parent) {
            GameObject go = new GameObject(name);
            if (parent != null) {
                go.transform.SetParent(parent, false);
            }
            return go;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, string material, bool keepCollider = false) {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            // Always explicit, the primitive default material is not a URP one
            go.GetComponent<MeshRenderer>().sharedMaterial = mats[material];
            if (!keepCollider) {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, string material, bool keepCollider = false) {
            return Prim(PrimitiveType.Cube, name, parent, localPos, scale, material, keepCollider);
        }

        // Fails loudly when a serialized field gets renamed
        static SerializedProperty FindProp(SerializedObject so, string prop) {
            SerializedProperty property = so.FindProperty(prop);
            if (property == null) {
                Debug.LogError($"BusDriverSceneBuilder: no serialized field '{prop}' on {so.targetObject.GetType().Name}");
            }
            return property;
        }

        static void SetRef(Object target, string prop, Object value) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetRefArray(Object target, string prop, Object[] values) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string prop, int value) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string prop, float value) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Object target, string prop, bool value) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetVector(Object target, string prop, Vector3 value) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetStringArray(Object target, string prop, string[] values) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Vector3 Forward(float heading) {
            float rad = heading * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        static Vector3 Right(float heading) {
            float rad = heading * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
        }

        // ----------------------------------------------------------- environment

        static void BuildEnvironment() {
            Transform environment = Group("Environment", null).transform;

            // The one and only drivable collider. Road slabs are visual, so the wheels
            // never roll across a seam between two colliders.
            GameObject ground = Box("Ground", environment, new Vector3(70f, -0.5f, 100f), new Vector3(600f, 1f, 600f), "Ground", true);
            ground.isStatic = true;

            BuildRoad(environment);
            BuildRoadside(environment);
            BuildObstacles(environment);
            BuildBusStops(environment);
        }

        // Clockwise loop: long straight, two R40 corners, a chicane on the far side, two more corners
        static void BuildRoad(Transform environment) {
            Transform road = Group("Road", environment).transform;
            samples = new List<RoadSample>();
            arcs = new List<RoadArc>();
            turtlePos = Vector3.zero;
            turtleHeading = 0f;
            turtleDistance = 0f;

            Straight(road, 200f);
            Arc(road, 40f, 90f);
            Straight(road, 60f);
            Arc(road, 40f, 90f);
            Straight(road, 40f);
            Arc(road, 60f, 30f);
            Arc(road, 60f, -30f);
            Arc(road, 60f, -30f);
            Arc(road, 60f, 30f);
            Straight(road, 40f);
            Arc(road, 40f, 90f);
            Straight(road, 60f);
            Arc(road, 40f, 90f);

            if (turtlePos.magnitude > 0.5f) {
                Debug.LogWarning($"Road loop does not close, end point is {turtlePos.magnitude:F2} m from the start");
            }
        }

        static void Straight(Transform road, float length) {
            AddRoadPiece(road, turtlePos, turtleHeading, length, 0f, 0f);
            turtlePos += Forward(turtleHeading) * length;
        }

        // Positive degrees turn right
        static void Arc(Transform road, float radius, float degrees) {
            float sign = Mathf.Sign(degrees);
            arcs.Add(new RoadArc {
                center = turtlePos + Right(turtleHeading) * radius * sign,
                radius = radius,
                sign = sign,
                midHeading = turtleHeading + degrees * 0.5f
            });
            int steps = Mathf.CeilToInt(Mathf.Abs(degrees) / ArcStepDegrees);
            float step = degrees / steps;
            float chord = 2f * radius * Mathf.Sin(Mathf.Abs(step) * 0.5f * Mathf.Deg2Rad);
            for (int i = 0; i < steps; i++) {
                float chordHeading = turtleHeading + step * 0.5f;
                AddRoadPiece(road, turtlePos, chordHeading, chord, radius, sign);
                turtlePos += Forward(chordHeading) * chord;
                turtleHeading += step;
            }
        }

        // radius 0 means a straight piece
        static void AddRoadPiece(Transform road, Vector3 start, float heading, float length, float radius, float sign) {
            Vector3 forward = Forward(heading);
            Vector3 right = Right(heading);
            Vector3 mid = start + forward * (length * 0.5f);
            Quaternion rotation = Quaternion.Euler(0f, heading, 0f);

            // On a curve the outside edge is longer than the centreline chord, so each
            // strip gets the chord length for its own radius plus a little overlap
            float LengthAt(float lateral) {
                if (radius <= 0f) {
                    return length;
                }
                return length * (radius - sign * lateral) / radius * 1.06f;
            }

            float halfRoad = RoadWidth * 0.5f;
            GameObject slab = Box("Slab", road, mid, new Vector3(RoadWidth, 0.04f, Mathf.Max(LengthAt(-halfRoad), LengthAt(halfRoad))), "Asphalt");
            slab.transform.rotation = rotation;
            slab.isStatic = true;

            foreach (float side in new[] { -1f, 1f }) {
                float lateral = side * (halfRoad + KerbWidth * 0.5f);
                Vector3 kerbPos = mid + right * lateral + Vector3.up * (KerbHeight * 0.5f);
                GameObject kerb = Box("Kerb", road, kerbPos, new Vector3(KerbWidth, KerbHeight, LengthAt(lateral)), "Kerb", true);
                kerb.transform.rotation = rotation;
                kerb.isStatic = true;
            }

            int count = Mathf.Max(1, Mathf.CeilToInt(length));
            for (int i = 0; i < count; i++) {
                float along = length * i / count;
                samples.Add(new RoadSample { pos = start + forward * along, heading = heading, distance = turtleDistance + along });
            }
            turtleDistance += length;
        }

        static RoadSample SampleAt(float distance) {
            distance = Mathf.Repeat(distance, turtleDistance);
            foreach (RoadSample sample in samples) {
                if (sample.distance >= distance) {
                    return sample;
                }
            }
            return samples[samples.Count - 1];
        }

        static Vector3 RoadPoint(RoadSample sample, float lateral, float height) {
            return sample.pos + Right(sample.heading) * lateral + Vector3.up * height;
        }

        // Emissive dashes and reflector posts are the speed cues at night
        static void BuildRoadside(Transform environment) {
            Transform markings = Group("Markings", environment).transform;
            for (float d = 0f; d < turtleDistance; d += 6f) {
                RoadSample sample = SampleAt(d);
                GameObject dash = Box("Dash", markings, RoadPoint(sample, 0f, 0.025f), new Vector3(0.15f, 0.012f, 2f), "LinePaint");
                dash.transform.rotation = Quaternion.Euler(0f, sample.heading, 0f);
            }
            for (float d = 0f; d < turtleDistance; d += 20f) {
                RoadSample sample = SampleAt(d);
                foreach (float side in new[] { -1f, 1f }) {
                    float lateral = side * (RoadWidth * 0.5f + 1f);
                    Box("Post", markings, RoadPoint(sample, lateral, 0.45f), new Vector3(0.1f, 0.9f, 0.1f), "Post").isStatic = true;
                    Box("Reflector", markings, RoadPoint(sample, lateral, 0.95f), new Vector3(0.12f, 0.15f, 0.12f), "Reflector");
                }
            }
        }

        static void BuildObstacles(Transform environment) {
            Transform obstacles = Group("Obstacles", environment).transform;

            // Walls and a lamp on the outside of each tight corner
            foreach (RoadArc arc in arcs) {
                if (arc.radius > 45f) {
                    continue;
                }
                Vector3 outward = Right(arc.midHeading) * -arc.sign;
                GameObject wall = Box("Corner Wall", obstacles, arc.center + outward * (arc.radius + 12f) + Vector3.up * 1.5f, new Vector3(1f, 3f, 40f), "Wall", true);
                wall.transform.rotation = Quaternion.Euler(0f, arc.midHeading, 0f);
                wall.isStatic = true;
                StreetLamp(obstacles, arc.center + outward * (arc.radius + 7f), -outward);
            }

            // Building silhouettes along the outside of the long straights
            float[] buildingHeights = { 7f, 12f, 9f, 14f };
            for (int i = 0; i < buildingHeights.Length; i++) {
                RoadSample sample = SampleAt(20f + i * 45f);
                float height = buildingHeights[i];
                GameObject building = Box("Building", obstacles, RoadPoint(sample, -16f, height * 0.5f), new Vector3(12f, height, 18f), "Building", true);
                building.transform.rotation = Quaternion.Euler(0f, sample.heading, 0f);
                building.isStatic = true;
            }

            ParkedCar(obstacles, 95f, -2.9f);
            ParkedCar(obstacles, 345f, 2.9f);
            ParkedCar(obstacles, 700f, -2.9f);

            // Light enough to shove, heavy enough not to jitter against an 11 t bus
            RoadSample crateSpot = SampleAt(290f);
            Vector3[] crateOffsets = { new Vector3(1.2f, 0.5f, 0f), new Vector3(2.3f, 0.5f, 0.2f), new Vector3(1.7f, 1.5f, 0.1f) };
            foreach (Vector3 offset in crateOffsets) {
                Vector3 pos = RoadPoint(crateSpot, offset.x, offset.y) + Forward(crateSpot.heading) * offset.z;
                GameObject crate = Box("Crate", obstacles, pos, Vector3.one, "Obstacle", true);
                crate.transform.rotation = Quaternion.Euler(0f, crateSpot.heading, 0f);
                crate.AddComponent<Rigidbody>().mass = 50f;
            }
        }

        static void ParkedCar(Transform parent, float distance, float lateral) {
            RoadSample sample = SampleAt(distance);
            GameObject car = Box("Parked Car", parent, RoadPoint(sample, lateral, 0.75f), new Vector3(1.9f, 1.5f, 4.5f), "Obstacle", true);
            car.transform.rotation = Quaternion.Euler(0f, sample.heading, 0f);
            car.isStatic = true;
        }

        static void StreetLamp(Transform parent, Vector3 basePos, Vector3 armDirection) {
            Transform lamp = Group("Street Lamp", parent).transform;
            lamp.position = basePos;
            Box("Pole", lamp, new Vector3(0f, 2.75f, 0f), new Vector3(0.15f, 5.5f, 0.15f), "Post", true).isStatic = true;
            Vector3 head = Vector3.up * 5.5f + armDirection * 1.2f;
            Box("Head", lamp, head, new Vector3(0.4f, 0.12f, 0.4f), "LampWhite");

            Light light = Group("Light", lamp).AddComponent<Light>();
            light.transform.localPosition = head - Vector3.up * 0.1f;
            light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            light.type = LightType.Spot;
            light.spotAngle = 120f;
            light.innerSpotAngle = 60f;
            light.range = 16f;
            light.intensity = 35f;
            light.color = new Color(1f, 0.8f, 0.55f);
            light.shadows = LightShadows.None;
        }

        // Markers only for now, there is no bus stop logic yet
        static void BuildBusStops(Transform environment) {
            Transform stops = Group("Bus Stops", environment).transform;
            busStops = new List<BusStop>();
            // The first one is a short roll from the spawn point so boarding is quick to test
            float[] distances = { 60f, 420f, 640f };
            int[] waitingCounts = { 3, 2, 2 };
            for (int stopIndex = 0; stopIndex < distances.Length; stopIndex++) {
                RoadSample sample = SampleAt(distances[stopIndex]);
                Transform stop = Group("Bus Stop", stops).transform;
                stop.SetPositionAndRotation(RoadPoint(sample, 0f, 0f), Quaternion.Euler(0f, sample.heading, 0f));

                float edge = RoadWidth * 0.5f;
                Box("Pad", stop, new Vector3(edge - 1.4f, 0.027f, 0f), new Vector3(2.6f, 0.012f, 14f), "StopPad").isStatic = true;
                Box("Pole", stop, new Vector3(edge + 1.2f, 1.3f, 5f), new Vector3(0.1f, 2.6f, 0.1f), "Post", true).isStatic = true;
                Box("Sign", stop, new Vector3(edge + 1.2f, 2.4f, 5f), new Vector3(0.6f, 0.4f, 0.06f), "StopPad").isStatic = true;
                Box("Shelter Back", stop, new Vector3(edge + 2.6f, 1.2f, 0f), new Vector3(0.1f, 2.4f, 4f), "Wall", true).isStatic = true;
                Box("Shelter Roof", stop, new Vector3(edge + 1.9f, 2.45f, 0f), new Vector3(1.6f, 0.1f, 4f), "Wall").isStatic = true;
                StreetLamp(stop, stop.TransformPoint(new Vector3(edge + 1.2f, 0f, -4f)), -Right(sample.heading));

                // Waiting NPCs are spawned at runtime by SceneController via ObjectPooling
                BusStop busStop = stop.gameObject.AddComponent<BusStop>();
                SetInt(busStop, "spawnCount", waitingCounts[stopIndex]);
                SetVector(busStop, "firstWaitLocal", new Vector3(edge + 1.2f, 0f, -1.2f));
                SetVector(busStop, "waitLocalStep", new Vector3(0.3f, 0f, 1.2f));
                busStops.Add(busStop);
            }
        }

        // ------------------------------------------------------------------- bus

        // The generated bus (PrefabBuilder, T-M1-14) in the right hand lane at the start of the
        // long straight. BuildAll must have run first; verify.sh content runs it before this.
        static GameObject InstantiateBus() {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBuilder.BusPath);
            if (prefab == null) {
                throw new System.InvalidOperationException("no " + PrefabBuilder.BusPath + "; run Tools/Bus Driver/Build All first");
            }
            GameObject bus = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            bus.name = "Bus";
            RoadSample spawn = SampleAt(12f);
            bus.transform.SetPositionAndRotation(RoadPoint(spawn, 2f, 0.05f), Quaternion.Euler(0f, spawn.heading, 0f));
            // The stops are this scene's; the prefab can't hold them
            SetRefArray(bus.GetComponent<BusCabin>(), "stops", busStops.ToArray());
            return bus;
        }

        // -------------------------------------------------------------- lighting

        static void BuildLighting() {
            RenderSettings.skybox = null;
            // Flat ambient is also what makes the existing brightness option do anything
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.1f, 0.12f);
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.012f;

            Light moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            moon.intensity = 0.08f;
            moon.color = new Color(0.6f, 0.7f, 1f);
            moon.shadows = LightShadows.Soft;
        }

        // --------------------------------------------------------- rig, HUD, glue

        static void BuildHUDAndSystems(GameObject bus) {
            Transform root = bus.transform;
            Transform anchor = root.Find(PrefabBuilder.DriverHeadName);
            DriverLook look = anchor.Find(PrefabBuilder.DriverPivotName).GetComponent<DriverLook>();
            Camera driverCamera = look.transform.Find(PrefabBuilder.DriverCameraName).GetComponent<Camera>();
            Transform ears = anchor.Find(PrefabBuilder.EarsName);
            GameObject driverAvatarRoot = anchor.Find(PrefabBuilder.DriverAvatarName).gameObject;
            CCTVSystem cctv = bus.GetComponentInChildren<CCTVSystem>(true);
            BusCabin cabin = bus.GetComponent<BusCabin>();

            // At the scene root rather than under the bus, see OnFootController
            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBuilder.OnFootRigPath));
            rig.name = "OnFootRig";
            OnFootController onFoot = rig.GetComponent<OnFootController>();
            Camera onFootCamera = rig.GetComponentInChildren<Camera>(true);
            SetRefArray(onFoot, "ignoredColliders", new Object[] { bus.GetComponent<BoxCollider>() });
            rig.SetActive(false);

            DrivingHUD hud = BuildHUD();
            SetRef(hud, "doors", bus.GetComponent<BusDoors>());
            SetRef(hud, "cabin", cabin);
            SetRef(hud, "bus", bus.GetComponent<BusController>());
            SetRef(hud, "cctv", cctv);

            // The scene root GameRoot wires this scene through (T-M1-04, until ShiftContext); it
            // releases the bus's DriveLock.Scripted until ShiftDirector exists (T-M1-17)
            LegacyNightRoot nightRoot = new GameObject("Night Root").AddComponent<LegacyNightRoot>();
            SetRef(nightRoot, "bus", bus.GetComponent<BusController>());

            GameObject systems = new GameObject("Game Systems");
            SceneController mode = systems.AddComponent<SceneController>();
            PlayerInteractor interactor = systems.AddComponent<PlayerInteractor>();
            SetRef(interactor, "onFootCamera", onFootCamera);
            SetRef(hud, "interactor", interactor);
            ObjectPooling npcPool = systems.AddComponent<ObjectPooling>();
            ConfigureNpcPools(npcPool, passengerPrefab, monsterPrefab);
            ConfigureSpawnableNpcs(mode);
            SetRef(mode, "npcPool", npcPool);
            SetBool(mode, "populateStopsOnStart", true);
            SetRef(mode, "busInput", bus.GetComponent<BusInput>());
            SetRef(mode, "driverLook", look);
            SetRef(mode, "cctv", cctv);
            SetRef(mode, "bus", bus.GetComponent<BusController>());
            SetRef(mode, "cabin", cabin);
            SetRef(mode, "doors", bus.GetComponent<BusDoors>());
            SetRef(mode, "onFoot", onFoot);
            SetRef(mode, "driverCamera", driverCamera);
            SetRef(mode, "onFootCamera", onFootCamera);
            SetRef(mode, "ears", ears);
            SetRef(mode, "earsSeatParent", anchor);
            SetRef(mode, "driverAvatar", driverAvatarRoot);
            GameOverMenu gameOverMenu = Object.FindAnyObjectByType<GameOverMenu>(FindObjectsInactive.Include);
            if (gameOverMenu != null) {
                SetRef(mode, "gameOverMenu", gameOverMenu);
            }
            SetRef(hud, "mode", mode);

            // The night's ambience loops (amb.wind), through AudioService (T-M1-11)
            systems.AddComponent<SceneAmbience>();
        }

        static void ConfigureNpcPools(ObjectPooling pooling, GameObject passenger, GameObject monster) {
            SerializedObject so = new SerializedObject(pooling);
            SerializedProperty pools = FindProp(so, "pools");
            if (pools == null) {
                return;
            }
            pools.arraySize = 2;
            SetPoolElement(pools.GetArrayElementAtIndex(0), "Passenger", passenger, 16);
            SetPoolElement(pools.GetArrayElementAtIndex(1), "WeepingAngel", monster, 4);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetPoolElement(SerializedProperty element, string id, GameObject prefab, int prewarm) {
            element.FindPropertyRelative("id").stringValue = id;
            element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            element.FindPropertyRelative("prewarm").intValue = prewarm;
        }

        static void ConfigureSpawnableNpcs(SceneController controller) {
            SerializedObject so = new SerializedObject(controller);
            SerializedProperty list = FindProp(so, "spawnableNpcs");
            if (list == null) {
                return;
            }
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).FindPropertyRelative("poolId").stringValue = "Passenger";
            list.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 10f;
            list.GetArrayElementAtIndex(1).FindPropertyRelative("poolId").stringValue = "WeepingAngel";
            list.GetArrayElementAtIndex(1).FindPropertyRelative("weight").floatValue = 1f;
            SerializedProperty guaranteed = FindProp(so, "guaranteedFirstNpcId");
            if (guaranteed != null) {
                guaranteed.stringValue = "WeepingAngel";
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Named "Canvas" with a "HUD" child; the overlay baker adds PauseScreen and Game Over to it
        static DrivingHUD BuildHUD() {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.layer = UILayer;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            DrivingHUD hud = canvasObject.AddComponent<DrivingHUD>();

            RectTransform hudRoot = UIPanel("HUD", canvasObject.transform);
            RectTransform driverPanel = UIPanel("DriverPanel", hudRoot);
            RectTransform cctvPanel = UIPanel("CCTVPanel", hudRoot);
            RectTransform pausePanel = UIPanel("PausePanel", hudRoot);
            RectTransform gameOverPanel = UIPanel("GameOverPanel", hudRoot);

            Color hudColor = new Color(0.85f, 0.9f, 0.85f, 0.9f);
            TMP_Text speed = Label("SpeedText", driverPanel, "0 km/h", 84f, TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(600f, 110f), hudColor);
            TMP_Text gear = Label("GearText", driverPanel, "N", 60f, TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(-60f, 150f), new Vector2(200f, 80f), hudColor);
            TMP_Text controls = Label("ControlsText", driverPanel, "", 24f, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(1300f, 40f), new Color(1f, 1f, 1f, 0.45f));

            GameObject scanObject = new GameObject("Scanlines", typeof(RectTransform));
            scanObject.layer = UILayer;
            scanObject.transform.SetParent(cctvPanel, false);
            Stretch(scanObject.GetComponent<RectTransform>());
            RawImage scanlines = scanObject.AddComponent<RawImage>();
            scanlines.raycastTarget = false;

            Color cctvColor = new Color(0.9f, 0.95f, 0.9f, 0.95f);
            TMP_Text camLabel = Label("CamLabel", cctvPanel, "CAM 1", 54f, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(60f, -50f), new Vector2(900f, 70f), cctvColor);
            TMP_Text rec = Label("RecText", cctvPanel, "REC", 54f, TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(-60f, -50f), new Vector2(300f, 70f), new Color(1f, 0.15f, 0.1f, 0.95f));
            TMP_Text timestamp = Label("Timestamp", cctvPanel, "02:13:00 AM", 44f, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(700f, 60f), cctvColor);

            GameObject dimObject = new GameObject("Dim", typeof(RectTransform));
            dimObject.layer = UILayer;
            dimObject.transform.SetParent(pausePanel, false);
            Stretch(dimObject.GetComponent<RectTransform>());
            Image dim = dimObject.AddComponent<Image>();
            dim.sprite = null;
            dim.type = Image.Type.Sliced;
            dim.color = new Color(0f, 0f, 0f, 0.392f);
            dim.raycastTarget = true;

            // Same vertical stack as Menu: Title / Start / Options / Quit
            Label("PauseTitle", pausePanel, "PAUSED", 120f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, 304f), new Vector2(1220f, 200f), Color.white);
            Button resumeButton = MenuButton("Resume Button", pausePanel, "RESUME", new Vector2(0f, 120f), 74f);
            Button optionsButton = MenuButton("Options Button", pausePanel, "OPTIONS", new Vector2(0f, 0f), 74f);
            Button quitToMenuButton = MenuButton("Quit To Menu Button", pausePanel, "QUIT TO MENU", new Vector2(0f, -120f), 72f);
            Button quitGameButton = MenuButton("Quit Game Button", pausePanel, "QUIT GAME", new Vector2(0f, -240f), 72f);

            GameObject optionsRoot = InstantiateMenuPrefab(
                "Assets/Prefabs/Level Essentials/In Canvas/Options Menu.prefab",
                canvasObject.transform,
                "Options Menu");
            GameObject controlsRoot = InstantiateMenuPrefab(
                "Assets/Prefabs/Level Essentials/In Canvas/Controls Menu.prefab",
                canvasObject.transform,
                "Controls Menu");

            PauseScreen pauseScreen = canvasObject.AddComponent<PauseScreen>();
            SetRef(pauseScreen, "pauseRoot", pausePanel.gameObject);
            if (optionsRoot != null) {
                SetRef(pauseScreen, "optionsRoot", optionsRoot);
            }
            if (controlsRoot != null) {
                SetRef(pauseScreen, "controlsRoot", controlsRoot);
            }
            SetRef(pauseScreen, "resumeButton", resumeButton);
            SetRef(pauseScreen, "optionsButton", optionsButton);
            SetRef(pauseScreen, "quitToMenuButton", quitToMenuButton);
            SetRef(pauseScreen, "quitGameButton", quitGameButton);

            GameObject gameOverDimObject = new GameObject("Dim", typeof(RectTransform));
            gameOverDimObject.layer = UILayer;
            gameOverDimObject.transform.SetParent(gameOverPanel, false);
            Stretch(gameOverDimObject.GetComponent<RectTransform>());
            Image gameOverDim = gameOverDimObject.AddComponent<Image>();
            gameOverDim.sprite = null;
            gameOverDim.type = Image.Type.Sliced;
            gameOverDim.color = new Color(0f, 0f, 0f, 0.392f);
            gameOverDim.raycastTarget = true;

            Label("GameOverTitle", gameOverPanel, "GAME OVER", 120f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1220f, 200f), Color.white);
            Button retryButton = MenuButton("Retry Button", gameOverPanel, "RETRY", new Vector2(0f, 40f), 74f);
            Button gameOverMainMenuButton = MenuButton("Main Menu Button", gameOverPanel, "MAIN MENU", new Vector2(0f, -110f), 72f);

            GameOverMenu gameOverMenu = canvasObject.AddComponent<GameOverMenu>();
            SetRef(gameOverMenu, "gameOverRoot", gameOverPanel.gameObject);
            SetRef(gameOverMenu, "retryButton", retryButton);
            SetRef(gameOverMenu, "mainMenuButton", gameOverMainMenuButton);

            EventSystem existingEventSystem = Object.FindAnyObjectByType<EventSystem>();
            UIInputModuleSetup.Configure(existingEventSystem != null ? existingEventSystem.gameObject : new GameObject("EventSystem"));
            if (canvasObject.GetComponent<GraphicRaycaster>() == null) {
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            RectTransform onFootPanel = UIPanel("OnFootPanel", hudRoot);
            GameObject crosshair = new GameObject("Crosshair", typeof(RectTransform));
            crosshair.layer = UILayer;
            crosshair.transform.SetParent(onFootPanel, false);
            crosshair.GetComponent<RectTransform>().sizeDelta = new Vector2(6f, 6f);
            Image dot = crosshair.AddComponent<Image>();
            dot.color = new Color(1f, 1f, 1f, 0.7f);
            dot.raycastTarget = false;

            // Shared by the seat and on-foot views
            TMP_Text prompt = Label("PromptText", hudRoot, "", 36f, TextAlignmentOptions.Bottom, new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(1200f, 110f), new Color(1f, 1f, 1f, 0.9f));
            TMP_Text status = Label("StatusText", hudRoot, "", 32f, TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(600f, 50f), new Color(1f, 0.75f, 0.2f, 0.95f));

            onFootPanel.gameObject.SetActive(false);
            cctvPanel.gameObject.SetActive(false);
            pausePanel.gameObject.SetActive(false);
            gameOverPanel.gameObject.SetActive(false);

            SetRef(hud, "driverPanel", driverPanel.gameObject);
            SetRef(hud, "cctvPanel", cctvPanel.gameObject);
            SetRef(hud, "pausePanel", pausePanel.gameObject);
            SetRef(hud, "speedText", speed);
            SetRef(hud, "gearText", gear);
            SetRef(hud, "controlsText", controls);
            SetRef(hud, "camLabelText", camLabel);
            SetRef(hud, "timestampText", timestamp);
            SetRef(hud, "recText", rec);
            SetRef(hud, "scanlines", scanlines);
            SetRef(hud, "onFootPanel", onFootPanel.gameObject);
            SetRef(hud, "promptText", prompt);
            SetRef(hud, "statusText", status);
            return hud;
        }

        static GameObject InstantiateMenuPrefab(string assetPath, Transform parent, string name) {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null) {
                Debug.LogWarning("BusDriverSceneBuilder: missing menu prefab at " + assetPath);
                return null;
            }
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            instance.name = name;
            instance.SetActive(false);
            return instance;
        }

        static Button MenuButton(string name, Transform parent, string label, Vector2 anchoredPos, float height) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(100f, height);

            Image image = go.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = new Color(0.990566f, 0.990566f, 0.990566f, 1f);
            Button button = go.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 0.23529412f);
            colors.pressedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.39215687f);
            colors.selectedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
            colors.disabledColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            TMP_Text labelText = Label(name + " Text", go.transform, label, 64f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, height), Color.white);
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.ForceMeshUpdate();
            float width = Mathf.Ceil(labelText.GetPreferredValues(label).x);
            rect.sizeDelta = new Vector2(width, height);
            labelText.rectTransform.sizeDelta = new Vector2(width, height);
            return button;
        }

        static RectTransform UIPanel(string name, Transform parent) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            Stretch(rect);
            return rect;
        }

        static void Stretch(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Anchor doubles as pivot, so anchoredPos is an inset from that corner
        static TMP_Text Label(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment, Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color color) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
    }
}
