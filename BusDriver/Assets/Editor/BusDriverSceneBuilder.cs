using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Generates the greybox MVP scene (Assets/Scenes/BusRoute.unity) from primitives.
// The scene is rebuilt from scratch on every run, so HAND EDITS TO IT ARE LOST.
// Tune handling through Assets/Settings/BusTuning.asset, which is never overwritten,
// and change layout numbers here.
public static class BusDriverSceneBuilder {
    public const string ScenePath = "Assets/Scenes/BusRoute.unity";
    const string MenuScenePath = "Assets/Scenes/Menu.unity";
    const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    const string MaterialFolder = "Assets/Materials/Greybox";
    const string TuningPath = "Assets/Settings/BusTuning.asset";
    // Regenerated on every build. Real art should live somewhere else.
    const string PrefabFolder = "Assets/Prefabs/Greybox";
    // Where the test monster waits. Remove these two with StaringMonster.
    const int MonsterStopIndex = 0;
    const int MonsterSlot = 1;
    const int UILayer = 5;

    // Road
    const float RoadWidth = 8f;
    const float KerbWidth = 0.3f;
    const float KerbHeight = 0.15f;
    const float ArcStepDegrees = 5f;

    // Bus body, root origin is at ground level under the middle of the bus
    const float BusWidth = 2.55f;
    const float BusLength = 12f;
    const float BusHeight = 3f;
    const float HullBottom = 0.45f;
    const float Panel = 0.08f;
    const float FrontAxleZ = 3.3f;
    const float RearAxleZ = -2.7f;
    const float TrackHalf = 1.05f;

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
        EnsureFolder(MaterialFolder);
        EnsureFolder("Assets/Settings");
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Prefabs");
        EnsureFolder(PrefabFolder);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateMaterials();
        BuildGreyboxPrefabs();
        BusTuning tuning = GetOrCreateTuning();

        BuildEnvironment();
        GameObject bus = BuildBus(tuning);
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

    static void CreateMaterials() {
        mats = new Dictionary<string, Material>();
        AddMaterial("Ground", new Color(0.07f, 0.09f, 0.07f));
        AddMaterial("Asphalt", new Color(0.13f, 0.13f, 0.14f));
        AddMaterial("Kerb", new Color(0.5f, 0.5f, 0.5f));
        AddMaterial("LinePaint", new Color(0.9f, 0.9f, 0.85f), new Color(0.9f, 0.9f, 0.8f) * 0.6f);
        AddMaterial("Post", new Color(0.15f, 0.15f, 0.15f));
        AddMaterial("Reflector", new Color(1f, 0.6f, 0.1f), new Color(1f, 0.55f, 0.1f) * 1.5f);
        AddMaterial("StopPad", new Color(0.8f, 0.7f, 0.1f), new Color(0.8f, 0.7f, 0.1f) * 0.5f);
        AddMaterial("Wall", new Color(0.3f, 0.28f, 0.27f));
        AddMaterial("Building", new Color(0.18f, 0.18f, 0.2f));
        AddMaterial("Obstacle", new Color(0.45f, 0.3f, 0.2f));
        AddMaterial("BusBody", new Color(0.6f, 0.58f, 0.5f));
        AddMaterial("BusInterior", new Color(0.35f, 0.36f, 0.38f));
        AddMaterial("Seat", new Color(0.15f, 0.2f, 0.4f));
        AddMaterial("Dash", new Color(0.08f, 0.08f, 0.09f));
        AddMaterial("Tyre", new Color(0.04f, 0.04f, 0.04f));
        AddMaterial("Passenger", new Color(0.65f, 0.55f, 0.5f));
        AddMaterial("PassengerOdd", new Color(0.25f, 0.2f, 0.22f));
        AddMaterial("LampWhite", Color.white, new Color(1f, 0.95f, 0.8f) * 3f);
        AddMaterial("LampRed", new Color(0.8f, 0.05f, 0.05f), new Color(1f, 0.05f, 0.05f) * 2f);
        AddMaterial("Door", new Color(0.3f, 0.33f, 0.36f));
        AddMaterial("Face", new Color(0.04f, 0.04f, 0.04f));
    }

    static void AddMaterial(string name, Color baseColor, Color? emission = null) {
        string path = MaterialFolder + "/" + name + ".mat";
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

    static void BuildGreyboxPrefabs() {
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

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + name + ".prefab");
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

    static BusTuning GetOrCreateTuning() {
        BusTuning tuning = AssetDatabase.LoadAssetAtPath<BusTuning>(TuningPath);
        if (tuning == null) {
            tuning = ScriptableObject.CreateInstance<BusTuning>();
            AssetDatabase.CreateAsset(tuning, TuningPath);
        }
        return tuning;
    }

    static PhysicsMaterial GetOrCreateHullPhysicsMaterial() {
        string path = MaterialFolder + "/BusHull.asset";
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (material == null) {
            material = new PhysicsMaterial("BusHull");
            AssetDatabase.CreateAsset(material, path);
        }
        // Slippery hull so the bus scrapes along walls instead of sticking to them
        material.dynamicFriction = 0.25f;
        material.staticFriction = 0.25f;
        material.bounciness = 0f;
        material.frictionCombine = PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(material);
        return material;
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

            // Waiting in front of the shelter, facing the road
            BusStop busStop = stop.gameObject.AddComponent<BusStop>();
            List<Object> waiting = new List<Object>();
            for (int slot = 0; slot < waitingCounts[stopIndex]; slot++) {
                bool monster = stopIndex == MonsterStopIndex && slot == MonsterSlot;
                Vector3 spot = new Vector3(edge + 1.2f + 0.3f * slot, 0f, -1.2f + 1.2f * slot);
                GameObject waiter = SpawnPassenger(monster ? monsterPrefab : passengerPrefab, monster ? "Staring Monster" : "Passenger", stop, spot, Quaternion.Euler(0f, -90f, 0f));
                waiting.Add(waiter.GetComponent<Passenger>());
            }
            SetRefArray(busStop, "waiting", waiting.ToArray());
            busStops.Add(busStop);
        }
    }

    // ------------------------------------------------------------------- bus

    static GameObject BuildBus(BusTuning tuning) {
        GameObject bus = new GameObject("Bus");
        Transform root = bus.transform;

        Rigidbody rb = bus.AddComponent<Rigidbody>();
        rb.mass = tuning.mass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // One hull collider. Interior and visual parts carry no colliders, they would
        // join the compound collider and distort the inertia tensor.
        BoxCollider hull = bus.AddComponent<BoxCollider>();
        hull.center = new Vector3(0f, HullBottom + BusHeight * 0.5f, 0f);
        hull.size = new Vector3(BusWidth, BusHeight, BusLength);
        hull.sharedMaterial = GetOrCreateHullPhysicsMaterial();

        BusController controller = bus.AddComponent<BusController>();
        bus.AddComponent<CrashDetector>();
        BusInput input = bus.AddComponent<BusInput>();
        BusEngineSound engineSound = bus.AddComponent<BusEngineSound>();
        SetRef(engineSound, "bus", controller);

        // Scaled primitives stay under Visuals, wheel colliders must not inherit any scale
        Transform visuals = Group("Visuals", root).transform;
        BuildShell(visuals);
        BuildInterior(visuals);
        BuildCabin(bus, controller);

        Transform com = Group("COM", root).transform;
        com.localPosition = new Vector3(0f, 0.9f, 0.2f);

        BuildWheels(root, tuning, out WheelCollider[] front, out WheelCollider[] rear, out Transform[] frontVisuals, out Transform[] rearVisuals);
        BuildBusLights(root);

        SetRef(controller, "tuning", tuning);
        SetRefArray(controller, "frontWheels", front);
        SetRefArray(controller, "rearWheels", rear);
        SetRefArray(controller, "frontWheelVisuals", frontVisuals);
        SetRefArray(controller, "rearWheelVisuals", rearVisuals);
        SetRef(controller, "centerOfMass", com);
        SetRef(input, "bus", controller);

        // Right hand lane at the start of the long straight
        RoadSample spawn = SampleAt(12f);
        root.SetPositionAndRotation(RoadPoint(spawn, 2f, 0.05f), Quaternion.Euler(0f, spawn.heading, 0f));
        return bus;
    }

    static void BuildShell(Transform visuals) {
        Transform shell = Group("Shell", visuals).transform;
        float halfW = BusWidth * 0.5f - Panel * 0.5f;
        float halfL = BusLength * 0.5f;
        float floorTop = HullBottom + 0.1f;
        float top = HullBottom + BusHeight;

        Box("Floor", shell, new Vector3(0f, HullBottom + 0.05f, 0f), new Vector3(BusWidth, 0.1f, BusLength), "BusInterior");
        Box("Roof", shell, new Vector3(0f, top - 0.05f, 0f), new Vector3(BusWidth, 0.1f, BusLength), "BusBody");
        Box("Rear Wall", shell, new Vector3(0f, (floorTop + top) * 0.5f, -halfL + Panel * 0.5f), new Vector3(BusWidth, top - floorTop, Panel), "BusBody");

        // Window band runs from 1.45 to 2.65
        const float lowerTop = 1.45f;
        const float upperBottom = 2.65f;
        float lowerH = lowerTop - floorTop;
        float upperH = top - 0.1f - upperBottom;
        float lowerY = floorTop + lowerH * 0.5f;
        float upperY = upperBottom + upperH * 0.5f;

        Box("Left Lower", shell, new Vector3(-halfW, lowerY, 0f), new Vector3(Panel, lowerH, BusLength), "BusBody");
        Box("Left Upper", shell, new Vector3(-halfW, upperY, 0f), new Vector3(Panel, upperH, BusLength), "BusBody");
        Box("Right Upper", shell, new Vector3(halfW, upperY, 0f), new Vector3(Panel, upperH, BusLength), "BusBody");

        // Door opening on the kerb side, left open for the walk-around mechanic later
        const float doorBack = 4.1f;
        const float doorFront = 5.5f;
        float rearLen = doorBack + halfL;
        Box("Right Lower Rear", shell, new Vector3(halfW, lowerY, -halfL + rearLen * 0.5f), new Vector3(Panel, lowerH, rearLen), "BusBody");
        float frontLen = halfL - doorFront;
        Box("Right Lower Front", shell, new Vector3(halfW, lowerY, doorFront + frontLen * 0.5f), new Vector3(Panel, lowerH, frontLen), "BusBody");

        float pillarH = upperBottom - lowerTop;
        float pillarY = lowerTop + pillarH * 0.5f;
        float[] pillars = { -halfL + 0.075f, -4f, -2f, 0f, 2f, doorBack - 0.05f, halfL - 0.075f };
        foreach (float z in pillars) {
            Box("Pillar L", shell, new Vector3(-halfW, pillarY, z), new Vector3(Panel, pillarH, 0.15f), "BusBody");
            Box("Pillar R", shell, new Vector3(halfW, pillarY, z), new Vector3(Panel, pillarH, 0.15f), "BusBody");
        }
        Box("Pillar Door", shell, new Vector3(halfW, pillarY, doorFront + 0.05f), new Vector3(Panel, pillarH, 0.15f), "BusBody");

        // Windscreen opening from 1.5 to 2.95
        float frontZ = halfL - Panel * 0.5f;
        Box("Front Lower", shell, new Vector3(0f, (floorTop + 1.5f) * 0.5f, frontZ), new Vector3(BusWidth, 1.5f - floorTop, Panel), "BusBody");
        Box("Front Upper", shell, new Vector3(0f, (2.95f + top - 0.1f) * 0.5f, frontZ), new Vector3(BusWidth, top - 0.1f - 2.95f, Panel), "BusBody");

        foreach (float side in new[] { -1f, 1f }) {
            Box("Headlight", shell, new Vector3(side * 0.9f, 1f, halfL + 0.01f), new Vector3(0.35f, 0.18f, 0.02f), "LampWhite");
            Box("Tail Light", shell, new Vector3(side * 0.9f, 1f, -halfL - 0.01f), new Vector3(0.3f, 0.15f, 0.02f), "LampRed");
        }
    }

    static void BuildInterior(Transform visuals) {
        Transform interior = Group("Interior", visuals).transform;

        // Driver station, left hand side
        const float driverX = -0.7f;
        Box("Dashboard", interior, new Vector3(0f, 1.3f, 5.55f), new Vector3(BusWidth - 0.2f, 0.4f, 0.7f), "Dash");
        Box("Binnacle", interior, new Vector3(driverX, 1.58f, 5.5f), new Vector3(0.6f, 0.16f, 0.3f), "Dash");
        GameObject wheel = Prim(PrimitiveType.Cylinder, "Steering Wheel", interior, new Vector3(driverX, 1.42f, 5f), new Vector3(0.5f, 0.015f, 0.5f), "Dash");
        wheel.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
        Box("Driver Seat Base", interior, new Vector3(driverX, 0.77f, 4.55f), new Vector3(0.4f, 0.45f, 0.4f), "Dash");
        Box("Driver Seat", interior, new Vector3(driverX, 1.05f, 4.55f), new Vector3(0.55f, 0.12f, 0.55f), "Seat");
        Box("Driver Seat Back", interior, new Vector3(driverX, 1.5f, 4.25f), new Vector3(0.55f, 0.9f, 0.1f), "Seat");

        // Double benches either side of the aisle
        Transform seats = Group("Seats", interior).transform;
        for (float z = -5.2f; z <= 2.9f; z += 1f) {
            foreach (float x in new[] { -0.8f, 0.8f }) {
                Box("Seat", seats, new Vector3(x, 1f, z), new Vector3(0.85f, 0.12f, 0.5f), "Seat");
                Box("Seat Back", seats, new Vector3(x, 1.4f, z - 0.27f), new Vector3(0.85f, 0.75f, 0.08f), "Seat");
                Box("Seat Leg", seats, new Vector3(x, 0.75f, z), new Vector3(0.1f, 0.4f, 0.1f), "Dash");
            }
        }

        // Boxes over the wheels, which poke up through the floor line
        foreach (float z in new[] { FrontAxleZ, RearAxleZ }) {
            foreach (float side in new[] { -1f, 1f }) {
                Box("Wheel Arch", interior, new Vector3(side * TrackHalf, 0.875f, z), new Vector3(0.45f, 0.65f, 1.4f), "BusInterior");
            }
        }
    }

    // Seats, the path through the door, the colliders the player walks on, and the
    // passengers who are already riding when the shift starts
    static BusCabin BuildCabin(GameObject bus, BusController controller) {
        Transform root = bus.transform;
        const float floorTop = HullBottom + 0.1f;
        const float seatTop = 1.06f;
        const float doorZ = 4.8f;

        // Two seats per bench, matching the benches in BuildInterior
        Transform seatRoot = Group("Seats", root).transform;
        List<Object> seats = new List<Object>();
        for (float z = -5.2f; z <= 2.9f; z += 1f) {
            foreach (float x in new[] { -1f, -0.6f, 0.6f, 1f }) {
                Transform seat = Group("Seat", seatRoot).transform;
                seat.localPosition = new Vector3(x, seatTop, z);
                seats.Add(seat.gameObject.AddComponent<BusSeat>());
            }
        }

        Transform nodes = Group("CabinNodes", root).transform;
        Transform aisleAtDoor = Node(nodes, "AisleAtDoor", new Vector3(0f, floorTop, doorZ));
        Transform doorStep = Node(nodes, "DoorStep", new Vector3(1f, floorTop, doorZ));
        // Height is found with a raycast at run time: road or kerb
        Transform doorOutside = Node(nodes, "DoorOutside", new Vector3(1.9f, 0f, doorZ));
        Transform standPoint = Node(nodes, "StandPoint", new Vector3(0.15f, floorTop, 4.5f));

        GameObject interior = BuildInteriorColliders(root);

        GameObject doorPanel = Box("Door Panel", root, new Vector3(1.3f, 1.6f, doorZ), new Vector3(0.06f, 2.1f, 1.4f), "Door");
        BusDoors doors = bus.AddComponent<BusDoors>();
        SetRef(doors, "bus", controller);
        SetRef(doors, "panel", doorPanel.transform);
        SetVector(doors, "closedLocalPos", doorPanel.transform.localPosition);
        // Slides back along the outside of the body
        SetVector(doors, "openLocalPos", new Vector3(1.34f, 1.6f, doorZ - 1.4f));

        Transform passengerRoot = Group("Passengers", root).transform;
        Vector3[] riders = {
            new Vector3(-1f, seatTop, 1.8f),
            new Vector3(0.6f, seatTop, -0.2f),
            new Vector3(-0.6f, seatTop, -2.2f),
            new Vector3(1f, seatTop, -3.2f)
        };
        List<Object> initial = new List<Object>();
        foreach (Vector3 rider in riders) {
            initial.Add(SpawnPassenger(passengerPrefab, "Passenger", passengerRoot, rider, Quaternion.identity).GetComponent<Passenger>());
        }

        BusCabin cabin = bus.AddComponent<BusCabin>();
        SetRef(cabin, "bus", controller);
        SetRef(cabin, "doors", doors);
        SetRefArray(cabin, "seats", seats.ToArray());
        SetRef(cabin, "passengerRoot", passengerRoot);
        SetRef(cabin, "interiorColliders", interior);
        SetRefArray(cabin, "stops", busStops.ToArray());
        SetRefArray(cabin, "initialPassengers", initial.ToArray());
        SetRef(cabin, "aisleAtDoor", aisleAtDoor);
        SetRef(cabin, "doorStep", doorStep);
        SetRef(cabin, "doorOutside", doorOutside);
        SetRef(cabin, "standPoint", standPoint);
        return cabin;
    }

    static Transform Node(Transform parent, string name, Vector3 localPos) {
        Transform node = Group(name, parent).transform;
        node.localPosition = localPos;
        return node;
    }

    // Saved inactive and only switched on while the bus is frozen for walking, so these
    // never join the hull's compound collider or change the driving inertia tensor.
    // Unscaled empties with sized BoxColliders, floor top at 0.55.
    static GameObject BuildInteriorColliders(Transform root) {
        Transform interior = Group("InteriorColliders", root).transform;
        InteriorBox(interior, "Floor", new Vector3(0f, 0.35f, 0f), new Vector3(BusWidth, 0.4f, BusLength));
        InteriorBox(interior, "Wall L", new Vector3(-1.29f, 1.95f, 0f), new Vector3(0.2f, 2.8f, BusLength));
        InteriorBox(interior, "Wall R Rear", new Vector3(1.29f, 1.95f, -0.95f), new Vector3(0.2f, 2.8f, 10.1f));
        InteriorBox(interior, "Wall R Front", new Vector3(1.29f, 1.95f, 5.75f), new Vector3(0.2f, 2.8f, 0.5f));
        // Separate so a later step can let the player off the bus by disabling it
        InteriorBox(interior, "Doorway Blocker", new Vector3(1.29f, 1.95f, 4.8f), new Vector3(0.2f, 2.8f, 1.4f));
        InteriorBox(interior, "Rear", new Vector3(0f, 1.95f, -5.95f), new Vector3(BusWidth, 2.8f, 0.1f));
        InteriorBox(interior, "Front Dash", new Vector3(0f, 1.95f, 5.6f), new Vector3(BusWidth, 2.8f, 0.8f));
        // One long box per side, an 0.8 m aisle with no gaps between rows to snag on
        InteriorBox(interior, "Bench L", new Vector3(-0.8175f, 1.175f, -1.425f), new Vector3(0.835f, 1.25f, 8.95f));
        InteriorBox(interior, "Bench R", new Vector3(0.8175f, 1.175f, -1.425f), new Vector3(0.835f, 1.25f, 8.95f));
        InteriorBox(interior, "Arch FL", new Vector3(-TrackHalf, 0.875f, FrontAxleZ), new Vector3(0.45f, 0.65f, 1.4f));
        InteriorBox(interior, "Arch FR", new Vector3(TrackHalf, 0.875f, FrontAxleZ), new Vector3(0.45f, 0.65f, 1.4f));
        InteriorBox(interior, "Driver Seat", new Vector3(-0.7f, 1.25f, 4.675f), new Vector3(0.6f, 1.4f, 1.05f)).AddComponent<DriverSeat>();
        interior.gameObject.SetActive(false);
        return interior.gameObject;
    }

    static GameObject InteriorBox(Transform parent, string name, Vector3 center, Vector3 size) {
        GameObject go = Group(name, parent);
        go.transform.localPosition = center;
        go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    static void BuildWheels(Transform root, BusTuning tuning, out WheelCollider[] front, out WheelCollider[] rear, out Transform[] frontVisuals, out Transform[] rearVisuals) {
        Transform wheels = Group("Wheels", root).transform;
        Transform wheelVisuals = Group("WheelVisuals", root).transform;

        // The collider sits at the top of the suspension travel. Placed so the tyre
        // just touches y = 0 with the suspension resting at its target position.
        float colliderY = tuning.wheelRadius + tuning.suspensionDistance * (1f - tuning.suspensionTarget);

        front = new WheelCollider[2];
        rear = new WheelCollider[2];
        frontVisuals = new Transform[2];
        rearVisuals = new Transform[2];
        for (int i = 0; i < 2; i++) {
            float x = i == 0 ? -TrackHalf : TrackHalf;
            string sideName = i == 0 ? "L" : "R";
            front[i] = Wheel(wheels, wheelVisuals, "F" + sideName, new Vector3(x, colliderY, FrontAxleZ), tuning, out frontVisuals[i]);
            rear[i] = Wheel(wheels, wheelVisuals, "R" + sideName, new Vector3(x, colliderY, RearAxleZ), tuning, out rearVisuals[i]);
        }
    }

    static WheelCollider Wheel(Transform wheels, Transform wheelVisuals, string suffix, Vector3 localPos, BusTuning tuning, out Transform visual) {
        GameObject go = Group("WC_" + suffix, wheels);
        go.transform.localPosition = localPos;
        WheelCollider wheel = go.AddComponent<WheelCollider>();
        // BusController.ApplyTuning sets everything again at runtime, these keep the editor gizmos honest
        wheel.radius = tuning.wheelRadius;
        wheel.mass = tuning.wheelMass;
        wheel.suspensionDistance = tuning.suspensionDistance;
        JointSpring spring = wheel.suspensionSpring;
        spring.spring = tuning.suspensionSpring;
        spring.damper = tuning.suspensionDamper;
        spring.targetPosition = tuning.suspensionTarget;
        wheel.suspensionSpring = spring;

        visual = Group("Wheel_" + suffix, wheelVisuals).transform;
        visual.localPosition = new Vector3(localPos.x, tuning.wheelRadius, localPos.z);
        float diameter = tuning.wheelRadius * 2f;
        // Cylinder axis is Y, lay it along the axle
        GameObject tyre = Prim(PrimitiveType.Cylinder, "Tyre", visual, Vector3.zero, new Vector3(diameter, 0.175f, diameter), "Tyre");
        tyre.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        // Off-centre block so wheel spin is visible
        Box("Hub Mark", visual, new Vector3(0f, tuning.wheelRadius * 0.5f, 0f), new Vector3(0.37f, 0.2f, 0.12f), "Kerb");
        return wheel;
    }

    static void BuildBusLights(Transform root) {
        Transform lights = Group("Lights", root).transform;
        float frontZ = BusLength * 0.5f + 0.1f;

        for (int i = 0; i < 2; i++) {
            Light headlight = Group(i == 0 ? "Headlight L" : "Headlight R", lights).AddComponent<Light>();
            headlight.transform.localPosition = new Vector3(i == 0 ? -0.9f : 0.9f, 1.2f, frontZ);
            headlight.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
            headlight.type = LightType.Spot;
            headlight.range = 60f;
            headlight.spotAngle = 55f;
            headlight.innerSpotAngle = 30f;
            headlight.intensity = 1500f;
            headlight.color = new Color(1f, 0.93f, 0.8f);
            // One shadow caster is enough to give the road some depth
            headlight.shadows = i == 0 ? LightShadows.Soft : LightShadows.None;
        }

        // Dim and sickly, but bright enough that the desaturated CCTV feed still reads
        foreach (float z in new[] { -4f, 0f, 3.5f }) {
            Light cabin = Group("Cabin Light", lights).AddComponent<Light>();
            cabin.transform.localPosition = new Vector3(0f, 3.2f, z);
            cabin.type = LightType.Point;
            cabin.range = 5f;
            cabin.intensity = 2.2f;
            cabin.color = new Color(0.75f, 0.9f, 0.7f);
            cabin.shadows = LightShadows.None;
        }

        Light dash = Group("Dash Light", lights).AddComponent<Light>();
        dash.transform.localPosition = new Vector3(-0.7f, 1.7f, 5.35f);
        dash.type = LightType.Point;
        dash.range = 1.2f;
        dash.intensity = 0.12f;
        dash.color = new Color(1f, 0.6f, 0.25f);
        dash.shadows = LightShadows.None;
    }

    static Camera CreateCamera(string name, Transform parent, Vector3 localPos, Vector3 localEuler, float fov) {
        Camera cam = Group(name, parent).AddComponent<Camera>();
        cam.transform.localPosition = localPos;
        cam.transform.localRotation = Quaternion.Euler(localEuler);
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 300f;
        // No skybox at night, clear to the fog colour so the far plane never shows
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = FogColor;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        return cam;
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

        // Seated eye point. The pivot turns, the camera rides on it.
        Transform anchor = Group("DriverSeatAnchor", root).transform;
        anchor.localPosition = new Vector3(-0.7f, 1.9f, 4.6f);
        GameObject pivot = Group("DriverCamPivot", anchor);
        DriverLook look = pivot.AddComponent<DriverLook>();
        Camera driverCamera = CreateCamera("DriverCamera", pivot.transform, Vector3.zero, Vector3.zero, 70f);
        driverCamera.tag = "MainCamera";

        // The only AudioListener. Never on a camera, so switching views can't leave zero or two.
        // PlayerModeController moves it to the on-foot head and back.
        Transform ears = Group("Driver Ears", anchor).transform;
        ears.gameObject.AddComponent<AudioListener>();

        GameObject cctvObject = Group("CCTV", root);
        Camera[] cctvCameras = {
            CreateCamera("CAM 1 Front", cctvObject.transform, new Vector3(0.3f, 3.2f, 5.3f), new Vector3(22f, 180f, 0f), 95f),
            CreateCamera("CAM 2 Mid", cctvObject.transform, new Vector3(0f, 3.2f, 0.5f), new Vector3(28f, 180f, 0f), 95f),
            CreateCamera("CAM 3 Rear", cctvObject.transform, new Vector3(0f, 3.2f, -5.7f), new Vector3(22f, 0f, 0f), 95f)
        };
        foreach (Camera cam in cctvCameras) {
            cam.enabled = false;
        }
        CCTVSystem cctv = cctvObject.AddComponent<CCTVSystem>();
        SetRef(cctv, "homeCamera", driverCamera);
        SetRefArray(cctv, "cctvCameras", cctvCameras);
        SetStringArray(cctv, "cameraLabels", new[] { "CAM 1  FRONT", "CAM 2  MID", "CAM 3  REAR" });
        SetRef(look, "cctv", cctv);

        BusCabin cabin = bus.GetComponent<BusCabin>();
        SetRef(cabin, "cctv", cctv);

        // At the scene root rather than under the bus, see OnFootController
        GameObject rig = new GameObject("OnFootRig");
        CharacterController body = rig.AddComponent<CharacterController>();
        body.height = 1.7f;
        body.radius = 0.28f;
        body.center = new Vector3(0f, 0.85f, 0f);
        body.stepOffset = 0.2f;
        body.skinWidth = 0.03f;
        body.slopeLimit = 45f;
        body.minMoveDistance = 0f;
        OnFootController onFoot = rig.AddComponent<OnFootController>();
        Transform footHead = Group("Head", rig.transform).transform;
        footHead.localPosition = new Vector3(0f, 1.6f, 0f);
        Camera onFootCamera = CreateCamera("OnFootCamera", footHead, Vector3.zero, Vector3.zero, 70f);
        onFootCamera.tag = "MainCamera";
        onFootCamera.enabled = false;
        PlayerInteractor interactor = rig.AddComponent<PlayerInteractor>();
        SetRef(onFoot, "head", footHead);
        SetRefArray(onFoot, "ignoredColliders", new Object[] { bus.GetComponent<BoxCollider>() });
        SetRef(interactor, "viewCamera", onFootCamera);
        rig.SetActive(false);

        DrivingHUD hud = BuildHUD();
        SetRef(hud, "interactor", interactor);
        SetRef(hud, "doors", bus.GetComponent<BusDoors>());
        SetRef(hud, "cabin", cabin);
        SetRef(hud, "bus", bus.GetComponent<BusController>());
        SetRef(hud, "cctv", cctv);

        PlayerModeController mode = new GameObject("Game Systems").AddComponent<PlayerModeController>();
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
        SetRef(hud, "mode", mode);

        GameObject soundObject = PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Level Essentials/Sound Controller.prefab")) as GameObject;
        if (soundObject != null) {
            soundObject.name = "Sound Controller";
            SetRef(mode, "soundController", soundObject.GetComponent<SoundController>());
        }
    }

    // Named "Canvas" with a "HUD" child so ingameMenus can adopt it later
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
        dim.color = new Color(0f, 0f, 0f, 0.65f);
        dim.raycastTarget = false;
        Label("PauseText", pausePanel, "PAUSED\n<size=36>ESC  resume      M  main menu</size>", 80f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 300f), Color.white);

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
