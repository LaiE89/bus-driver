using System.Collections.Generic;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static BusDriver.Editor.Builders.BuilderUtil;

namespace BusDriver.Editor.Builders {
    // BuildAll step 8 (§4.15): Generated/Scenes/Route01_World.unity. This is the LEGACY MODE of
    // T-M1-16: the MVP's closed test loop, roadside, obstacles, three stops with lamps and the
    // pooled waiting riders (LegacyRiderSpawner, including the WeepingAngel), ported as-is from
    // BusDriverSceneBuilder. T-M2-07 replaces it with Route 1 built from RouteDefinition.
    public static class RouteBuilder {
        public const string ScenePath = SceneIds.GeneratedFolder + "/" + SceneIds.Route01World + ".unity";

        const float RoadWidth = 8f;
        const float KerbWidth = 0.3f;
        const float KerbHeight = 0.15f;
        const float ArcStepDegrees = 5f;
        const float SpawnDistance = 12f;
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

        // Builder state for one run
        static List<RoadSample> samples;
        static List<RoadArc> arcs;
        static List<BusStop> busStops;
        static Vector3 turtlePos;
        static float turtleHeading;
        static float turtleDistance;

        [MenuItem("Tools/Bus Driver/Builders/Route01_World")]
        public static void Build() {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildEnvironment();
            BuildLighting();
            BuildRouteRoot();
            SaveScene(scene, ScenePath);
        }

        static Material M(string name) {
            return MaterialLibraryBuilder.Get(name);
        }

        static Vector3 Forward(float heading) {
            float rad = heading * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        static Vector3 Right(float heading) {
            float rad = heading * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
        }

        // ------------------------------------------------------------ environment

        static void BuildEnvironment() {
            Transform environment = Group("Environment", null).transform;
            // The one and only drivable collider. Road slabs are visual, so the wheels never roll
            // across a seam between two colliders.
            GameObject ground = Box("Ground", environment, new Vector3(70f, -0.5f, 100f), new Vector3(600f, 1f, 600f), M("Ground"), true);
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
                Debug.LogWarning($"RouteBuilder: the legacy loop does not close, end point is {turtlePos.magnitude:F2} m from the start");
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

            // On a curve the outside edge is longer than the centreline chord, so each strip gets
            // the chord length for its own radius plus a little overlap
            float LengthAt(float lateral) {
                if (radius <= 0f) {
                    return length;
                }
                return length * (radius - sign * lateral) / radius * 1.06f;
            }

            float halfRoad = RoadWidth * 0.5f;
            GameObject slab = Box("Slab", road, mid, new Vector3(RoadWidth, 0.04f, Mathf.Max(LengthAt(-halfRoad), LengthAt(halfRoad))), M("Asphalt"));
            slab.transform.rotation = rotation;
            slab.isStatic = true;

            foreach (float side in new[] { -1f, 1f }) {
                float lateral = side * (halfRoad + KerbWidth * 0.5f);
                Vector3 kerbPos = mid + right * lateral + Vector3.up * (KerbHeight * 0.5f);
                GameObject kerb = Box("Kerb", road, kerbPos, new Vector3(KerbWidth, KerbHeight, LengthAt(lateral)), M("Kerb"), true);
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

        // Emissive dashes and reflector posts are the speed cues at night. Small emissive props
        // stay non-static: static batching renders them magenta (§4.17).
        static void BuildRoadside(Transform environment) {
            Transform markings = Group("Markings", environment).transform;
            for (float d = 0f; d < turtleDistance; d += 6f) {
                RoadSample sample = SampleAt(d);
                GameObject dash = Box("Dash", markings, RoadPoint(sample, 0f, 0.025f), new Vector3(0.15f, 0.012f, 2f), M("LinePaint"));
                dash.transform.rotation = Quaternion.Euler(0f, sample.heading, 0f);
            }
            for (float d = 0f; d < turtleDistance; d += 20f) {
                RoadSample sample = SampleAt(d);
                foreach (float side in new[] { -1f, 1f }) {
                    float lateral = side * (RoadWidth * 0.5f + 1f);
                    Box("Post", markings, RoadPoint(sample, lateral, 0.45f), new Vector3(0.1f, 0.9f, 0.1f), M("Post")).isStatic = true;
                    Box("Reflector", markings, RoadPoint(sample, lateral, 0.95f), new Vector3(0.12f, 0.15f, 0.12f), M("Reflector"));
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
                GameObject wall = Box("Corner Wall", obstacles, arc.center + outward * (arc.radius + 12f) + Vector3.up * 1.5f, new Vector3(1f, 3f, 40f), M("Wall"), true);
                wall.transform.rotation = Quaternion.Euler(0f, arc.midHeading, 0f);
                wall.isStatic = true;
                StreetLamp(obstacles, arc.center + outward * (arc.radius + 7f), -outward);
            }

            // Building silhouettes along the outside of the long straights
            float[] buildingHeights = { 7f, 12f, 9f, 14f };
            for (int i = 0; i < buildingHeights.Length; i++) {
                RoadSample sample = SampleAt(20f + i * 45f);
                float height = buildingHeights[i];
                GameObject building = Box("Building", obstacles, RoadPoint(sample, -16f, height * 0.5f), new Vector3(12f, height, 18f), M("Building"), true);
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
                GameObject crate = Box("Crate", obstacles, pos, Vector3.one, M("Obstacle"), true);
                crate.transform.rotation = Quaternion.Euler(0f, crateSpot.heading, 0f);
                crate.AddComponent<Rigidbody>().mass = 50f;
            }
        }

        static void ParkedCar(Transform parent, float distance, float lateral) {
            RoadSample sample = SampleAt(distance);
            GameObject car = Box("Parked Car", parent, RoadPoint(sample, lateral, 0.75f), new Vector3(1.9f, 1.5f, 4.5f), M("Obstacle"), true);
            car.transform.rotation = Quaternion.Euler(0f, sample.heading, 0f);
            car.isStatic = true;
        }

        static void StreetLamp(Transform parent, Vector3 basePos, Vector3 armDirection) {
            Transform lamp = Group("Street Lamp", parent).transform;
            lamp.position = basePos;
            Box("Pole", lamp, new Vector3(0f, 2.75f, 0f), new Vector3(0.15f, 5.5f, 0.15f), M("Post"), true).isStatic = true;
            Vector3 head = Vector3.up * 5.5f + armDirection * 1.2f;
            Box("Head", lamp, head, new Vector3(0.4f, 0.12f, 0.4f), M("LampWhite"));

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
                Box("Pad", stop, new Vector3(edge - 1.4f, 0.027f, 0f), new Vector3(2.6f, 0.012f, 14f), M("StopPad")).isStatic = true;
                Box("Pole", stop, new Vector3(edge + 1.2f, 1.3f, 5f), new Vector3(0.1f, 2.6f, 0.1f), M("Post"), true).isStatic = true;
                Box("Sign", stop, new Vector3(edge + 1.2f, 2.4f, 5f), new Vector3(0.6f, 0.4f, 0.06f), M("StopPad")).isStatic = true;
                Box("Shelter Back", stop, new Vector3(edge + 2.6f, 1.2f, 0f), new Vector3(0.1f, 2.4f, 4f), M("Wall"), true).isStatic = true;
                Box("Shelter Roof", stop, new Vector3(edge + 1.9f, 2.45f, 0f), new Vector3(1.6f, 0.1f, 4f), M("Wall")).isStatic = true;
                StreetLamp(stop, stop.TransformPoint(new Vector3(edge + 1.2f, 0f, -4f)), -Right(sample.heading));

                // Waiting riders are spawned at night start by LegacyRiderSpawner
                BusStop busStop = stop.gameObject.AddComponent<BusStop>();
                SetInt(busStop, "spawnCount", waitingCounts[stopIndex]);
                SetVector(busStop, "firstWaitLocal", new Vector3(edge + 1.2f, 0f, -1.2f));
                SetVector(busStop, "waitLocalStep", new Vector3(0.3f, 0f, 1.2f));
                busStops.Add(busStop);
            }
        }

        // ---------------------------------------------------------------- lighting

        // This scene is the active one during a night (§4.3), so its RenderSettings are the night's
        static void BuildLighting() {
            ApplyNightRenderSettings();
            Light moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            moon.intensity = 0.08f;
            moon.color = new Color(0.6f, 0.7f, 1f);
            moon.shadows = LightShadows.Soft;
        }

        // Flat ambient is also what makes the brightness setting do anything. Shared with the
        // other generated scenes until NightLightingPreset (T-M2-05).
        public static void ApplyNightRenderSettings() {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.1f, 0.1f, 0.12f);
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.012f;
        }

        // --------------------------------------------------------------- route root

        // Stops, the bus spawn (the right-hand lane at the start of the long straight), the pooled
        // riders and the night's ambience (amb.wind)
        static void BuildRouteRoot() {
            GameObject rootObject = new GameObject("Route Root");
            RouteSceneRoot route = rootObject.AddComponent<RouteSceneRoot>();
            LegacyRiderSpawner riders = rootObject.AddComponent<LegacyRiderSpawner>();
            ObjectPooling npcPool = rootObject.AddComponent<ObjectPooling>();
            ConfigureNpcPools(npcPool);
            ConfigureSpawnableNpcs(riders);
            SetRef(riders, "npcPool", npcPool);
            SetBool(riders, "populateStopsOnInit", true);
            SceneAmbience ambience = rootObject.AddComponent<SceneAmbience>();

            RoadSample spawn = SampleAt(SpawnDistance);
            Transform spawnPoint = Group("Bus Spawn", rootObject.transform).transform;
            spawnPoint.SetPositionAndRotation(RoadPoint(spawn, 2f, 0.05f), Quaternion.Euler(0f, spawn.heading, 0f));

            SetRefArray(route, "stops", busStops.ToArray());
            SetRef(route, "busSpawn", spawnPoint);
            SetRef(route, "riders", riders);
            SetRefArray(route, "bindables", new Object[] { ambience });
        }

        static void ConfigureNpcPools(ObjectPooling pooling) {
            SerializedObject so = new SerializedObject(pooling);
            SerializedProperty pools = so.FindProperty("pools");
            pools.arraySize = 2;
            SetPool(pools.GetArrayElementAtIndex(0), "Passenger", PrefabBuilder.LegacyPassengerPath, 16);
            SetPool(pools.GetArrayElementAtIndex(1), "WeepingAngel", PrefabBuilder.LegacyWeepingAngelPath, 4);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetPool(SerializedProperty element, string id, string prefabPath, int prewarm) {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) {
                throw new System.InvalidOperationException("no " + prefabPath + "; PrefabBuilder runs before RouteBuilder");
            }
            element.FindPropertyRelative("id").stringValue = id;
            element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            element.FindPropertyRelative("prewarm").intValue = prewarm;
        }

        static void ConfigureSpawnableNpcs(LegacyRiderSpawner spawner) {
            SerializedObject so = new SerializedObject(spawner);
            SerializedProperty list = so.FindProperty("spawnableNpcs");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).FindPropertyRelative("poolId").stringValue = "Passenger";
            list.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 10f;
            list.GetArrayElementAtIndex(1).FindPropertyRelative("poolId").stringValue = "WeepingAngel";
            list.GetArrayElementAtIndex(1).FindPropertyRelative("weight").floatValue = 1f;
            so.FindProperty("guaranteedFirstNpcId").stringValue = "WeepingAngel";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
