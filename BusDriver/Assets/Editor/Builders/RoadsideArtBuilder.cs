using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Editor.Art;
using BusDriver.Gameplay.Views;
using BusDriver.Gameplay.World;
using UnityEditor;
using UnityEngine;
using static BusDriver.Editor.Builders.BuilderUtil;

namespace BusDriver.Editor.Builders {
    // Route01_World's set dressing from the city kit (Art/Environment/City, CityKitImport): only
    // where a rural mountain road would plausibly have it. The Pinecrest Gas stop is a hamlet (a
    // service garage and a row of shops), the Ridge Clinic is a single-storey building, the depot
    // has its yard clutter and a water tower, St. Agnes gets a churchyard railing, the campground,
    // trailhead and lodge get benches and bins, the closed side roads get cones, and a billboard
    // announces the gas stop. Nothing stands between the road and the containment line except at
    // the stubs' far ends, so no prop touches driving or the stops' boarding zones.
    //
    // Every spot is (route distance, signed lateral offset: + is right of the centreline). By
    // default a piece's front (+Z) faces the road; `yaw` turns it further, clockwise from above.
    // RouteBuilder asks Footprints() first, so the forest rows leave room for the buildings.
    //
    // A little light so the buildings read at night: an open shop or the clinic gets a light
    // over its front and a glow behind its glass (one gas shop is closed, one flickers); the
    // garage and the depot get wall packs over their doors, and the billboard a lamp on its
    // face. Each light has its own LightFlicker (with its glow as an EmissiveView), so it wavers
    // like the stop lamps and its glow dims with it.
    public static class RoadsideArtBuilder {
        struct Spot {
            public string Prefab;
            public float Distance;
            public float Lateral;
            public float Yaw;
            public float Up;
            // Buildings: sunk a little so the grade never shows a gap under them; their footprint
            // also clears the trees
            public bool Building;
            // Then moved this far in the piece's own frame (the billboard sits in front of its pole)
            public Vector3 Local;
            public bool BlankPanel;
            public Lit Lit;

            public Spot(string prefab, float distance, float lateral, float yaw = 0f, float up = 0f, bool building = false) {
                Prefab = prefab;
                Distance = distance;
                Lateral = lateral;
                Yaw = yaw;
                Up = up;
                Building = building;
                Local = Vector3.zero;
                BlankPanel = false;
                Lit = Lit.None;
            }
        }

        enum Lit { None, Warm, WarmFlicker, Cool }

        // A light on its own (not a shop's): placed like a Spot, then `Local` in that frame. `Aim`
        // is its direction in the same frame (a spot light), or zero for a point light.
        struct Lamp {
            public float Distance;
            public float Lateral;
            public float Yaw;
            public float Up;
            public Vector3 Local;
            public Vector3 Aim;
            public float Range;
            public float Intensity;
            public float SpotAngle;
            // A small glowing fixture box at the light, or none
            public Vector3 FixtureSize;
        }

        static readonly Color WarmLight = new Color(1f, 0.8f, 0.55f);
        static readonly Color CoolLight = new Color(0.8f, 0.9f, 1f);
        // A shop's front light: this far out from the front and up, and how strong
        const float ShopLightOut = 0.8f;
        const float ShopLightUp = 2.7f;
        const float ShopLightRange = 9f;
        const float ShopLightIntensity = 6f;

        const float BuildingSink = 0.2f;
        const float FootprintMargin = 1.5f;
        // And the footprint reaches this much further out of the front, so no tree stands
        // between a building and the road
        const float FrontClearance = 4f;
        // A shop's front is this far out; its centre is the front plus half its depth
        const float ShopFront = 11f;

        static Spot Shop(string prefab, float distance, float lateral, Lit lit = Lit.None) {
            return new Spot(prefab, distance, lateral, 0f, 0f, true) { Lit = lit };
        }

        // Front at ±ShopFront: lateral of the centre for a 5.5 m deep shop
        static float Row(float side) {
            return side * (ShopFront + 2.75f);
        }

        static List<Spot> Spots() {
            const float R = 1f;
            List<Spot> s = new List<Spot>();

            // ---- depot (left, 0–60 m): the shed is at 22–38 m, 9–19 m out, its roof at 6.2 m
            s.Add(new Spot("SM_Prop_Water_Tower_01", 26f, -16.5f, 0f, 6.2f, true));
            s.Add(new Spot("SM_Prop_Aircon_01", 33.5f, -11.5f, 0f, 6.2f));
            s.Add(new Spot("SM_Prop_Aircon_01", 35f, -11.5f, 0f, 6.2f));
            s.Add(new Spot("SM_Prop_SatDish_01", 23.5f, -10.5f, 30f, 6.2f));
            s.Add(new Spot("SM_Prop_PowerBox_01", 19.5f, -9.2f));
            s.Add(new Spot("SM_Prop_CarboardBox_03", 16.5f, -10f, 15f));
            s.Add(new Spot("SM_Prop_CarboardBox_04", 17.3f, -10.4f, -20f));
            s.Add(new Spot("SM_Prop_CarboardBox_01", 16.9f, -10.1f, 40f, 0.8f));
            s.Add(new Spot("SM_Prop_Hydrant_01", 40.5f, -8.3f));
            s.Add(new Spot("SM_Prop_Skip_01", 44f, -12f, 0f, 0f, true));
            s.Add(new Spot("SM_Prop_Skip_02", 47f, -12f, 0f, 0f, true));
            s.Add(new Spot("SM_Prop_TrashBag_01", 42.2f, -10.6f, 20f));
            s.Add(new Spot("SM_Prop_TrashBag_02", 49.2f, -10.8f, -50f));
            s.Add(new Spot("SM_Prop_TrashBag_03", 48.6f, -10.1f, 80f));
            s.Add(new Spot("SM_Prop_Cone_01", 54f, -8.2f));
            s.Add(new Spot("SM_Prop_Cone_01", 56f, -8.2f, 30f));
            s.Add(new Spot("SM_Prop_Barrier_01", 58.5f, -8.4f));

            // ---- the billboard on the approach to Pinecrest Gas (left, turned toward the traffic).
            // The kit's board is only its frame, catwalk and lamps (front +Z), so it gets a blank
            // weathered panel behind them.
            s.Add(new Spot("SM_Prop_Billboard_Pole_01", 745f, -10f, 60f, 0f, true));
            s.Add(new Spot("SM_Prop_Billboard_01", 745f, -10f, 60f, 4.4f, true) { Local = new Vector3(0f, 0f, 0.5f), BlankPanel = true });

            // ---- Pinecrest Gas (stop at 800, right): a service garage and three shops
            s.Add(Shop("SM_Bld_Shop_06", 787.5f, Row(R)));
            s.Add(Shop("SM_Bld_Shop_01", 792.5f, Row(R), Lit.Warm));
            s.Add(Shop("SM_Bld_Shop_03", 800f, Row(R), Lit.WarmFlicker));
            s.Add(Shop("SM_Bld_Shop_04", 807.5f, Row(R)));
            s.Add(new Spot("SM_Prop_ATM_01", 794f, ShopFront - 0.25f, 0f, 1.1f));
            s.Add(new Spot("SM_Prop_PotPlant_01", 790.7f, ShopFront - 0.6f));
            s.Add(new Spot("SM_Prop_PotPlant_01", 795.5f, ShopFront - 0.6f));
            s.Add(new Spot("SM_Prop_Trashbin_01", 798f, 9.6f));
            s.Add(new Spot("SM_Prop_Mailbox_01", 803.5f, 9.4f));
            s.Add(new Spot("SM_Prop_Newspaper_01", 803.5f, 10.1f, 25f));
            s.Add(new Spot("SM_Prop_Sign_Parking_01", 812f, 8.2f));
            s.Add(new Spot("SM_Prop_PowerBox_01", 782.5f, 12f, -90f));
            s.Add(new Spot("SM_Prop_Skip_02", 782.5f, 16f, -90f, 0f, true));
            s.Add(new Spot("SM_Prop_TrashBag_02", 782f, 18.2f));
            s.Add(new Spot("SM_Prop_CarboardBox_02", 812.5f, 15f, 20f));
            s.Add(new Spot("SM_Prop_CarboardBox_03", 813f, 16.2f, -10f));

            // ---- St. Agnes (stop at 1900, right): a churchyard railing, open where the stop is
            foreach (float d in new[] { 1872.5f, 1877.5f, 1882.5f, 1917.5f, 1922.5f, 1927.5f }) {
                s.Add(new Spot("SM_Env_Fence_01", d, 8.3f, 0f, 0f, true));
            }
            foreach (float d in new[] { 1870f, 1885f, 1915f, 1930f }) {
                s.Add(new Spot("SM_Env_Fence_End_01", d, 8.3f));
            }

            // ---- Ridge Clinic (stop at 2400, right): two plaster units as one front
            s.Add(Shop("SM_Bld_Shop_05", 2397.5f, Row(R), Lit.Cool));
            s.Add(Shop("SM_Bld_Shop_02", 2402.5f, Row(R), Lit.Cool));
            s.Add(new Spot("SM_Prop_ParkBench_01", 2393f, 9.4f));
            s.Add(new Spot("SM_Prop_PotPlant_02", 2394.8f, ShopFront - 0.5f));
            s.Add(new Spot("SM_Prop_PotPlant_02", 2405.4f, ShopFront - 0.5f));
            s.Add(new Spot("SM_Prop_Trashbin_02", 2408f, 9.2f));

            // ---- Hollow Creek Campground (1250, right) and Summit Trailhead (2750, right)
            s.Add(new Spot("SM_Prop_Skip_02", 1264f, 10.5f, 0f, 0f, true));
            s.Add(new Spot("SM_Prop_Trashbin_02", 1244f, 8.6f));
            s.Add(new Spot("SM_Prop_ParkBench_01", 1257f, 8.6f));
            s.Add(new Spot("SM_Prop_ParkBench_01", 2758f, 8.6f));
            s.Add(new Spot("SM_Prop_Trashbin_02", 2755f, 8.5f));

            // ---- Summit Lodge (terminus at 2960; the end wall at 3000)
            s.Add(new Spot("SM_Prop_ParkBench_01", 2984f, -9f));
            s.Add(new Spot("SM_Prop_Trashbin_01", 2981.5f, -8.7f));
            s.Add(new Spot("SM_Prop_Skip_01", 2991f, -11f, 0f, 0f, true));
            s.Add(new Spot("SM_Prop_TrashBag_01", 2993.2f, -10f, 60f));
            s.Add(new Spot("SM_Prop_Sign_Parking_01", 2988f, 8.2f));
            s.Add(new Spot("SM_Prop_PotPlant_02", 2980f, 8.6f));
            return s;
        }

        static List<Lamp> Lamps() {
            Vector3 downAndOut = new Vector3(0f, -1f, 0.6f);
            Vector3 wallPack = new Vector3(0.35f, 0.15f, 0.2f);
            return new List<Lamp> {
                // The depot's bay doors (the shed's front is 9 m out on the left, its doors at 28 and 33.5 m)
                new Lamp { Distance = 28f, Lateral = -9f, Up = 4.7f, Local = new Vector3(0f, 0f, 0.2f), Aim = downAndOut,
                           Range = 12f, Intensity = 18f, SpotAngle = 110f, FixtureSize = wallPack },
                new Lamp { Distance = 33.5f, Lateral = -9f, Up = 4.7f, Local = new Vector3(0f, 0f, 0.2f), Aim = downAndOut,
                           Range = 12f, Intensity = 18f, SpotAngle = 110f, FixtureSize = wallPack },
                // Pinecrest Gas's service garage, over its roller door
                new Lamp { Distance = 787.5f, Lateral = ShopFront, Up = 3.1f, Local = new Vector3(-1f, 0f, 0.25f), Aim = downAndOut,
                           Range = 10f, Intensity = 14f, SpotAngle = 110f, FixtureSize = wallPack },
                // The billboard: from its catwalk lamps down onto the panel (the board is at 4.4 m, 3.1 m tall)
                new Lamp { Distance = 745f, Lateral = -10f, Yaw = 60f, Up = 8.2f, Local = new Vector3(0f, 0f, 1.9f),
                           Aim = new Vector3(0f, -1.2f, -1f), Range = 7f, Intensity = 10f, SpotAngle = 80f },
            };
        }

        // The buildings' ground footprints (+ a margin), for RouteBuilder to keep the trees out of
        public static List<Bounds> Footprints(RoutePath path, out List<Quaternion> rotations) {
            List<Bounds> footprints = new List<Bounds>();
            rotations = new List<Quaternion>();
            foreach (Spot spot in Spots()) {
                GameObject prefab = Prefab(spot.Prefab, false);
                if (!spot.Building || prefab == null) {
                    continue;
                }
                Pose(path, spot, out Vector3 position, out Quaternion rotation);
                Bounds local = LocalBounds(prefab);
                Vector3 size = local.size + new Vector3(FootprintMargin * 2f, 0f, FootprintMargin * 2f + FrontClearance);
                Vector3 center = local.center + new Vector3(0f, 0f, FrontClearance * 0.5f);
                footprints.Add(new Bounds(position + rotation * center, size));
                rotations.Add(rotation);
            }
            return footprints;
        }

        // Whether a world point is inside any footprint (its oriented box, horizontally)
        public static bool InFootprint(Vector3 point, List<Bounds> footprints, List<Quaternion> rotations) {
            for (int i = 0; i < footprints.Count; i++) {
                Vector3 local = Quaternion.Inverse(rotations[i]) * (point - footprints[i].center);
                Vector3 half = footprints[i].extents;
                if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z) {
                    return true;
                }
            }
            return false;
        }

        public static void Build(RouteDefinition route, RoutePath path, Transform parent) {
            Transform lights = Group("Lights", parent).transform;
            int seed = 1;
            foreach (Spot spot in Spots()) {
                GameObject go = Place(path, spot, parent);
                if (go != null && spot.Lit != Lit.None) {
                    LightShop(go, spot, lights, seed++);
                }
            }
            foreach (Lamp lamp in Lamps()) {
                PlaceLamp(path, lamp, lights, seed++);
            }
            BuildStubCones(route, path, Group("Stub Cones", parent).transform);
        }

        // Cones (and a barrier where the road ends at nothing) just short of each closed stub's blocker
        static void BuildStubCones(RouteDefinition route, RoutePath path, Transform parent) {
            foreach (RouteStub stub in route.stubs) {
                if (stub.blocker == BlockerKind.FallenTree) {
                    continue;
                }
                RouteBuilder.StubEnd(route, path, stub, out Vector3 end, out Vector3 axis);
                Vector3 across = Vector3.Cross(Vector3.up, axis).normalized;
                Quaternion facing = Quaternion.LookRotation(-axis, Vector3.up);
                Vector3 row = end - axis * 2.2f;
                float[] offsets = stub.blocker == BlockerKind.CollapsedBridge ? new[] { -2.6f, 2.6f } : new[] { -2.4f, 0f, 2.4f };
                foreach (float offset in offsets) {
                    Instance("SM_Prop_Cone_01", parent, row + across * offset, facing);
                }
                if (stub.blocker == BlockerKind.CollapsedBridge) {
                    Instance("SM_Prop_Barrier_01", parent, row, facing);
                }
            }
        }

        static void Pose(RoutePath path, Spot spot, out Vector3 position, out Quaternion rotation) {
            RoutePose pose = path.Evaluate(spot.Distance);
            float sink = spot.Building && spot.Up == 0f ? BuildingSink : 0f;
            position = pose.Offset(spot.Lateral, spot.Up - sink);
            // Front (+Z) toward the centreline, then the extra yaw
            Vector3 toRoad = pose.Right * -Mathf.Sign(spot.Lateral);
            rotation = Quaternion.LookRotation(toRoad, Vector3.up) * Quaternion.Euler(0f, spot.Yaw, 0f);
            position += rotation * spot.Local;
        }

        // A light over the shop's front and a glow behind its glass, flickering together
        static void LightShop(GameObject shop, Spot spot, Transform parent, int seed) {
            Bounds local = LocalBounds(AssetDatabase.LoadAssetAtPath<GameObject>(CityKitImport.PrefabPath(spot.Prefab)));
            bool cool = spot.Lit == Lit.Cool;
            Transform t = shop.transform;

            GameObject glow = Box("Interior Glow", t, new Vector3(local.center.x, 1.35f, local.min.z + 0.5f),
                new Vector3(local.size.x * 0.85f, 2.1f, 0.04f), MaterialLibraryBuilder.Get(cool ? "WindowCool" : "Window"));
            glow.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector3 at = t.TransformPoint(new Vector3(local.center.x, ShopLightUp, local.max.z + ShopLightOut));
            GameObject root = Group(spot.Prefab + " Light", parent);
            root.transform.SetPositionAndRotation(at, t.rotation);
            Light light = NewLight(root, LightType.Point, cool ? CoolLight : WarmLight, ShopLightRange, ShopLightIntensity);
            Flicker(root, light, glow.GetComponent<Renderer>(), spot.Lit == Lit.WarmFlicker ? FlickerMode.Unstable : FlickerMode.Subtle, seed);
        }

        static void PlaceLamp(RoutePath path, Lamp lamp, Transform parent, int seed) {
            Spot frame = new Spot(null, lamp.Distance, lamp.Lateral, lamp.Yaw, lamp.Up) { Local = lamp.Local };
            Pose(path, frame, out Vector3 position, out Quaternion rotation);
            GameObject root = Group("Lamp " + lamp.Distance.ToString("0"), parent);
            root.transform.SetPositionAndRotation(position, rotation);
            bool spot = lamp.Aim != Vector3.zero;
            Light light = NewLight(root, spot ? LightType.Spot : LightType.Point, WarmLight, lamp.Range, lamp.Intensity);
            if (spot) {
                light.transform.rotation = rotation * Quaternion.LookRotation(lamp.Aim.normalized, Vector3.up);
                light.spotAngle = lamp.SpotAngle;
                light.innerSpotAngle = lamp.SpotAngle * 0.5f;
            }
            Renderer fixture = null;
            if (lamp.FixtureSize != Vector3.zero) {
                fixture = Box("Fixture", root.transform, Vector3.zero, lamp.FixtureSize, MaterialLibraryBuilder.Get("LampWhite")).GetComponent<Renderer>();
                fixture.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Flicker(root, light, fixture, FlickerMode.Subtle, seed);
        }

        // The light on its own child, so a spot can be aimed without turning the fixture
        static Light NewLight(GameObject root, LightType type, Color color, float range, float intensity) {
            GameObject go = Group("Light", root.transform);
            Light light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        static void Flicker(GameObject root, Light light, Renderer glow, FlickerMode mode, int seed) {
            LightFlicker flicker = root.AddComponent<LightFlicker>();
            SetRefArray(flicker, "lights", new Object[] { light });
            SetInt(flicker, "mode", (int)mode);
            SetInt(flicker, "seed", 7000 + seed);
            if (glow != null) {
                EmissiveView view = glow.gameObject.AddComponent<EmissiveView>();
                SetRefArray(view, "renderers", new Object[] { glow });
                SetRefArray(flicker, "emissiveViews", new Object[] { view });
            }
        }

        static GameObject Place(RoutePath path, Spot spot, Transform parent) {
            Pose(path, spot, out Vector3 position, out Quaternion rotation);
            GameObject go = Instance(spot.Prefab, parent, position, rotation);
            if (go != null && spot.BlankPanel) {
                Bounds frame = LocalBounds(AssetDatabase.LoadAssetAtPath<GameObject>(CityKitImport.PrefabPath(spot.Prefab)));
                Box("Blank Panel", go.transform, new Vector3(0f, (frame.min.y + frame.max.y) * 0.5f + 0.25f, frame.min.z + 0.12f),
                    new Vector3(frame.size.x - 0.2f, frame.size.y - 0.9f, 0.05f), MaterialLibraryBuilder.Get("Wall"));
            }
            return go;
        }

        // The kit prefab with a box collider on World around its meshes
        static GameObject Instance(string name, Transform parent, Vector3 position, Quaternion rotation) {
            GameObject prefab = Prefab(name, true);
            if (prefab == null) {
                return null;
            }
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(position, rotation);
            go.layer = Layers.World;
            Bounds local = LocalBounds(prefab);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = local.center;
            box.size = local.size;
            return go;
        }

        static GameObject Prefab(string name, bool warn) {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CityKitImport.PrefabPath(name));
            if (prefab == null && warn) {
                Debug.LogWarning($"RoadsideArtBuilder: no prefab {name}; run Tools ▸ Bus Driver ▸ Art ▸ Import City Kit");
            }
            return prefab;
        }

        // The prefab's mesh bounds in its root's space (the meshes sit on unrotated, unscaled children)
        static Bounds LocalBounds(GameObject prefab) {
            bool any = false;
            Bounds bounds = new Bounds();
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true)) {
                if (filter.sharedMesh == null) {
                    continue;
                }
                Bounds b = filter.sharedMesh.bounds;
                b.center += filter.transform.localPosition;
                if (any) {
                    bounds.Encapsulate(b);
                } else {
                    bounds = b;
                    any = true;
                }
            }
            return bounds;
        }
    }
}
