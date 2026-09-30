using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Route;
using BusDriver.Gameplay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static BusDriver.Editor.Builders.BuilderUtil;

namespace BusDriver.Editor.Builders {
    // BuildAll step 8 (§4.15, T-M2-07): Generated/Scenes/Route01_World.unity, built from the
    // RouteDefinition (§3) and the EnvironmentViewSet. In order: the road ribbon, the cross-section
    // profiles and their dressing, the side stubs, the stops, the signs, the depot and the lodge, the
    // zone triggers, the cliff's fall zones and camera anchor, the safety floor and KillPlane, the
    // night lighting and the RouteSceneRoot. Deterministic: fixed dressing seed, data order.
    public static class RouteBuilder {
        public const string ScenePath = SceneIds.GeneratedFolder + "/" + SceneIds.Route01World + ".unity";
        public const string MeshFolder = GeneratedRoot + "/Meshes/Route01";
        public const string RootName = "Route Root";
        public const string SpawnName = "Bus Spawn";
        public const string CliffName = "Cliff";
        public const string FallCamAnchorName = "FallCamAnchor";
        public const string StubWallName = "Stub Wall";

        // The fall zones and the tunnel/rumble triggers are boxes this long, overlapping a little
        const float ZoneBoxStep = 10f;
        const float ZoneBoxOverlap = 0.5f;
        // The walls closing the route's two ends (§3.1 row 26: the lodge's end wall)
        const float EndWallThickness = 1f;
        // The depot yard pad on the left, 0 → depotPadEnd (§3.1 row 1)
        const float DepotPadWidth = 2.9f;
        const float LampInset = 0.7f;
        const float SignOffset = 0.5f;

        struct Built {
            public RouteDefinition Route;
            public RoutePath Path;
            public RouteGeneration Gen;
            public List<BusStop> Stops;
            public List<ZoneVolume> Zones;
            public List<FallZone> FallZones;
            public KillPlane KillPlane;
            public Transform FallCamAnchor;
            public int NextSeed;
        }

        [MenuItem("Tools/Bus Driver/Builders/Route01_World")]
        public static void Build() {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RouteDefinition route = LoadRoute();
            Built b = NewBuilt(route);
            EnsureFolder(MeshFolder);

            Transform world = Group("World", null).transform;
            List<RouteOpening> openings = StubOpenings(route, b.Path);
            RoadMeshBuilder.Build(route, b.Path, Group("Road", world).transform, MeshFolder);
            ProfileBuildResult profiles = ProfileBuilder.Build(route, b.Path, Group("Profiles", world).transform, openings, MeshFolder);
            BuildDressing(ref b, profiles, Group("Dressing", world).transform);
            BuildStubs(ref b, Group("Stubs", world).transform);
            BuildStops(ref b, Group("Stops", world).transform);
            BuildSigns(ref b, Group("Signs", world).transform);
            BuildDepot(ref b, Group("Depot", world).transform);
            BuildLodge(ref b, Group("Lodge", world).transform);
            BuildRumbleMarkings(b, Group("Rumble Strips", world).transform);

            BuildZones(ref b, Group("Zones", null).transform);
            BuildCliff(ref b, Group(CliffName, null).transform);
            BuildMap(ref b, Group("Map", null).transform);
            LightingPresetApplier lighting = LightingBuild.CreateApplier(null);
            BuildRouteRoot(ref b, lighting);
            SaveScene(scene, ScenePath);
        }

        // Only what §3.5 checks, in the open scene with the meshes kept in memory: the profiles and
        // their containment, the stubs and the fall zones. RouteContainmentTests builds this from a
        // modified copy of the data to prove the check catches a missing guardrail.
        public static GameObject BuildContainment(RouteDefinition route) {
            Built b = NewBuilt(route);
            GameObject root = Group("Containment Only", null);
            ProfileBuilder.Build(route, b.Path, Group("Profiles", root.transform).transform, StubOpenings(route, b.Path), null);
            BuildStubs(ref b, Group("Stubs", root.transform).transform);
            BuildCliff(ref b, Group(CliffName, root.transform).transform);
            return root;
        }

        static Built NewBuilt(RouteDefinition route) {
            return new Built {
                Route = route,
                Path = new RoutePath(route),
                Gen = route.generation,
                Stops = new List<BusStop>(),
                Zones = new List<ZoneVolume>(),
                FallZones = new List<FallZone>(),
                NextSeed = 1,
            };
        }

        static RouteDefinition LoadRoute() {
            GameRootConfig config = AssetDatabase.LoadAssetAtPath<GameRootConfig>(GameRootConfig.AssetPath);
            RouteDefinition route = config != null ? config.Route(RouteSeed.RouteId) : null;
            if (route == null) {
                throw new System.InvalidOperationException("no route '" + RouteSeed.RouteId + "' in GameRootConfig; DataSeeder runs before RouteBuilder");
            }
            return route;
        }

        static Material M(string name) {
            return MaterialLibraryBuilder.Get(name);
        }

        static float Sign(RouteSide side) {
            return RouteStrip.SideSign(side);
        }

        static SideProfile ProfileAt(Built b, RouteSide side, float distance) {
            return ProfileAt(b.Route, b.Path, side, distance);
        }

        static SideProfile ProfileAt(RouteDefinition route, RoutePath path, RouteSide side, float distance) {
            RouteSegment segment = route.segments[path.SegmentIndexAt(distance)];
            return side == RouteSide.Left ? segment.left : segment.right;
        }

        // A box with a collider on World, in world space
        static GameObject Solid(string name, Transform parent, Vector3 center, Quaternion rotation, Vector3 size, Material material, bool containment) {
            GameObject go = material != null
                ? Box(name, parent, Vector3.zero, size, material, true)
                : Group(name, parent);
            if (material == null) {
                go.AddComponent<BoxCollider>().size = size;
            }
            go.transform.SetPositionAndRotation(center, rotation);
            go.layer = Layers.World;
            go.isStatic = true;
            if (containment) {
                go.tag = Tags.Containment;
            }
            return go;
        }

        // ------------------------------------------------------------------ dressing

        // The profile builder's placements, as environment views (§3.4, T-M2-06)
        static void BuildDressing(ref Built b, ProfileBuildResult profiles, Transform parent) {
            Transform trees = Group("Trees", parent).transform;
            foreach (DressingSpot spot in profiles.Trees) {
                GameObject tree = EnvironmentPrefabBuilder.Place(EnvironmentKinds.Tree(spot.Variant), trees, spot.Position, Quaternion.Euler(0f, spot.YawDeg, 0f));
                tree.transform.localScale = Vector3.one * spot.Scale;
            }
            Transform rocks = Group("Rocks", parent).transform;
            foreach (DressingSpot spot in profiles.Rocks) {
                GameObject rock = EnvironmentPrefabBuilder.Place(EnvironmentKinds.RockChunk, rocks, spot.Position, Quaternion.Euler(0f, spot.YawDeg, 0f));
                rock.transform.localScale = Vector3.one * spot.Scale;
            }
            Transform rails = Group("Guardrails", parent).transform;
            float segment = b.Gen.guardrailSegmentLength;
            foreach (RailSpot spot in profiles.Guardrails) {
                GameObject rail = EnvironmentPrefabBuilder.Place(EnvironmentKinds.GuardrailSegment, rails, spot.Position, spot.Rotation);
                if (spot.Length < segment - 0.01f) {
                    rail.transform.localScale = new Vector3(1f, 1f, spot.Length / segment);
                }
            }
            foreach (RailSpot spot in profiles.EndCaps) {
                EnvironmentPrefabBuilder.Place(EnvironmentKinds.GuardrailEndCap, rails, spot.Position, spot.Rotation);
            }
            Transform lamps = Group("Tunnel Lights", parent).transform;
            foreach (Vector3 position in profiles.TunnelLights) {
                RoutePose pose = b.Path.Evaluate(b.Path.Project(position).Distance);
                EnvironmentPrefabBuilder.Place(EnvironmentKinds.TunnelLamp, lamps, position, pose.Rotation, b.NextSeed++);
            }
        }

        // ------------------------------------------------------------------ stubs

        // A stub's geometry (§3.3): 8 m wide, 20 m long, branching off the road at 70° from the
        // route heading toward its side, starting at the shoulder's outer edge
        struct StubFrame {
            public RoutePose Pose;
            public float Sign;
            public Vector3 Axis;
            // Unit vector across the stub, to its right when looking down the axis
            public Vector3 Across;
            public Vector3 Mouth;
            public float MouthLateral;
            public float HalfWidth;
            public float Length;
        }

        static StubFrame FrameOf(Built b, RouteStub stub) {
            return FrameOf(b.Route, b.Path, stub);
        }

        static StubFrame FrameOf(RouteDefinition route, RoutePath path, RouteStub stub) {
            RoutePose pose = path.Evaluate(stub.distance);
            float sign = Sign(stub.side);
            float angle = stub.angleDeg * Mathf.Deg2Rad;
            Vector3 axis = (pose.Forward * Mathf.Cos(angle) + pose.Right * (sign * Mathf.Sin(angle))).normalized;
            float mouthLateral = RoadMeshBuilder.ShoulderEdge(route, stub.distance, stub.side);
            return new StubFrame {
                Pose = pose,
                Sign = sign,
                Axis = axis,
                Across = Vector3.Cross(Vector3.up, axis).normalized,
                Mouth = pose.Offset(mouthLateral * sign),
                MouthLateral = mouthLateral,
                HalfWidth = stub.width * 0.5f,
                Length = stub.length,
            };
        }

        // Where a stub wall (at ±halfWidth across the axis) is at a given lateral distance from the
        // centreline, as a distance along the axis (stubs sit on straights, §3.2)
        static float AxisAtLateral(StubFrame f, float acrossOffset, float lateral) {
            Vector3 start = f.Mouth + f.Across * acrossOffset;
            float startLateral = Vector3.Dot(start - f.Pose.Position, f.Pose.Right) * f.Sign;
            float lateralPerMetre = Vector3.Dot(f.Axis, f.Pose.Right) * f.Sign;
            return (lateral - startLateral) / lateralPerMetre;
        }

        // The route distance where a stub wall crosses a lateral line
        static float RouteDistanceAt(StubFrame f, float acrossOffset, float lateral) {
            Vector3 point = f.Mouth + f.Across * acrossOffset + f.Axis * AxisAtLateral(f, acrossOffset, lateral);
            return f.Pose.Distance + Vector3.Dot(point - f.Pose.Position, f.Pose.Forward);
        }

        // The main road's containment line opens exactly between the two stub walls' crossings (§3.3)
        public static List<RouteOpening> StubOpenings(RouteDefinition route, RoutePath path) {
            List<RouteOpening> openings = new List<RouteOpening>();
            foreach (RouteStub stub in route.stubs) {
                StubFrame f = FrameOf(route, path, stub);
                float line = ProfileBuilder.ContainmentOffset(route.generation, ProfileAt(route, path, stub.side, stub.distance));
                if (line < 0f) {
                    continue;
                }
                float a = RouteDistanceAt(f, -f.HalfWidth, line);
                float c = RouteDistanceAt(f, f.HalfWidth, line);
                // The main wall runs a little into the stub walls, so there's no seam to squeeze through
                const float overlap = 0.1f;
                openings.Add(new RouteOpening { Side = stub.side, From = Mathf.Min(a, c) + overlap, To = Mathf.Max(a, c) - overlap });
            }
            return openings;
        }

        // Ground, two invisible walls (tagged Containment: they close the opening, §3.5) and the
        // blocker across the end, facing the main road
        static void BuildStubs(ref Built b, Transform parent) {
            float wallHeight = b.Gen.containmentWallHeight;
            const float wallThickness = 0.3f;
            for (int i = 0; i < b.Route.stubs.Length; i++) {
                RouteStub stub = b.Route.stubs[i];
                StubFrame f = FrameOf(b, stub);
                Transform root = Group($"Stub {stub.distance:0} {stub.side} {stub.blocker}", parent).transform;
                Quaternion along = Quaternion.LookRotation(f.Axis, Vector3.up);
                float y = f.Pose.Position.y;

                // From just inside the shoulder to the blocker; its top a hair under the ribbon
                float groundStart = -1.5f;
                float groundLength = f.Length - groundStart;
                Vector3 groundCenter = f.Mouth + f.Axis * (groundStart + groundLength * 0.5f);
                groundCenter.y = y - 0.055f;
                Solid("Ground", root, groundCenter, along, new Vector3(stub.width + 0.6f, 0.1f, groundLength), M("Asphalt"), false);

                foreach (float side in new[] { -1f, 1f }) {
                    float offset = side * (f.HalfWidth + wallThickness * 0.5f);
                    // Each wall starts where it crosses the shoulder's edge, so neither pokes into the road
                    float from = AxisAtLateral(f, offset, f.MouthLateral);
                    float length = f.Length - from;
                    Vector3 center = f.Mouth + f.Across * offset + f.Axis * (from + length * 0.5f);
                    center.y = y + wallHeight * 0.5f - 1f;
                    Solid(StubWallName, root, center, along, new Vector3(wallThickness, wallHeight + 1f, length), null, true);
                }

                Vector3 end = f.Mouth + f.Axis * f.Length;
                end.y = y;
                EnvironmentPrefabBuilder.Place(EnvironmentKinds.Blocker(stub.blocker, stub.variant), root, end,
                    Quaternion.LookRotation(-f.Axis, Vector3.up));
            }
        }

        // ------------------------------------------------------------------ stops, signs

        // On the centreline at the stop's distance, +X toward the kerb (§3.2, A.4)
        static void BuildStops(ref Built b, Transform parent) {
            foreach (RouteStop stop in b.Route.stops) {
                RoutePose pose = b.Path.Evaluate(stop.distance);
                GameObject placed = EnvironmentPrefabBuilder.Place(EnvironmentKinds.Stop(stop.kind), parent, pose.Position, pose.Rotation, b.NextSeed++);
                placed.name = "Stop " + stop.stopId;
                BusStop busStop = placed.GetComponent<BusStop>();
                SetString(busStop, "stopId", stop.stopId);
                b.Stops.Add(busStop);
            }
        }

        // Signs face −Z, so +Z along the route faces the traffic; chevrons stand on the cliff's gravel
        static void BuildSigns(ref Built b, Transform parent) {
            foreach (RouteSign sign in b.Route.signs) {
                RoutePose pose = b.Path.Evaluate(sign.distance);
                float edge = RoadMeshBuilder.ShoulderEdge(b.Route, sign.distance, sign.side);
                float lateral = sign.kind == SignKind.Chevron ? edge - 0.15f : edge + SignOffset;
                EnvironmentPrefabBuilder.Place(EnvironmentKinds.Sign(sign.kind), parent, pose.Offset(lateral * Sign(sign.side)), pose.Rotation);
            }
        }

        // ------------------------------------------------------------------ depot and lodge

        // §3.1 row 1: the yard pad on the left from 0 to 60 m, the depot building behind the forest
        // wall, two lamps (§3.3), and a wall across the road behind the spawn
        static void BuildDepot(ref Built b, Transform parent) {
            float padEnd = b.Route.depotPadEnd;
            RoutePose mid = b.Path.Evaluate(padEnd * 0.5f);
            float edge = RoadMeshBuilder.ShoulderEdge(b.Route, mid.Distance, RouteSide.Left);
            Vector3 pad = mid.Offset(-(edge + DepotPadWidth * 0.5f), -0.05f);
            Solid("Yard Pad", parent, pad, mid.Rotation, new Vector3(DepotPadWidth, 0.1f, padEnd), M("Concrete"), false);

            float wall = ProfileBuilder.ContainmentOffset(b.Gen, ProfileAt(b, RouteSide.Left, mid.Distance));
            EnvironmentPrefabBuilder.Place(EnvironmentKinds.DepotBuilding, parent, mid.Offset(-(wall + 6.5f)),
                Quaternion.LookRotation(mid.Right, Vector3.up));
            foreach (float d in new[] { padEnd * 0.25f, padEnd * 0.75f }) {
                RoutePose pose = b.Path.Evaluate(d);
                PlaceLamp(ref b, parent, pose, RouteSide.Left);
            }
            RoutePose start = b.Path.Evaluate(0f);
            EndWall(b, parent, "Depot Wall", start, -1f);
        }

        // §3.1 row 26: the lodge building and its end wall block the road at the route's end; two
        // lamps (§3.3) light the terminus
        static void BuildLodge(ref Built b, Transform parent) {
            RoutePose end = b.Path.Evaluate(b.Path.TotalLength);
            EndWall(b, parent, "End Wall", end, 1f);
            EnvironmentPrefabBuilder.Place(EnvironmentKinds.LodgeBuilding, parent, end.Offset(0f) + end.Forward * (EndWallThickness + 7f),
                Quaternion.LookRotation(-end.Forward, Vector3.up));
            string terminus = b.Route.terminusStopId;
            float at = b.Route.TryGetStop(terminus, out RouteStop stop) ? stop.distance + 15f : b.Path.TotalLength - 25f;
            RoutePose pose = b.Path.Evaluate(at);
            PlaceLamp(ref b, parent, pose, RouteSide.Left);
            PlaceLamp(ref b, parent, pose, RouteSide.Right);
        }

        // A street lamp just inside the containment line, its arm reaching over the road
        static void PlaceLamp(ref Built b, Transform parent, RoutePose pose, RouteSide side) {
            float line = ProfileBuilder.ContainmentOffset(b.Gen, ProfileAt(b, side, pose.Distance));
            float sign = Sign(side);
            EnvironmentPrefabBuilder.Place(EnvironmentKinds.StreetLamp, parent, pose.Offset((line - LampInset) * sign),
                Quaternion.LookRotation(pose.Right * -sign, Vector3.up), b.NextSeed++);
        }

        // Across both containment lines, beyond the route's end (direction +1) or before its start (−1)
        static void EndWall(Built b, Transform parent, string name, RoutePose pose, float direction) {
            float width = 2f * b.Gen.forestWallOffset + 2f;
            float height = b.Gen.containmentWallHeight;
            Vector3 center = pose.Position + pose.Forward * (direction * EndWallThickness * 0.5f) + Vector3.up * (height * 0.5f - 0.5f);
            Solid(name, parent, center, pose.Rotation, new Vector3(width, height + 1f, EndWallThickness), M("Wall"), true);
        }

        // Painted strips where the rumble zones are, so the bend is announced to the eye too (§3.3)
        static void BuildRumbleMarkings(Built b, Transform parent) {
            Material paint = M("Kerb");
            foreach (RouteZone zone in b.Route.zones) {
                if (zone.kind != RouteZoneKind.RumbleStrip) {
                    continue;
                }
                if (zone.span == ZoneSpan.Across) {
                    for (float d = zone.start + 0.5f; d < zone.end; d += 1.5f) {
                        RoutePose pose = b.Path.Evaluate(d);
                        GameObject stripe = Box("Rumble Stripe", parent, Vector3.zero, new Vector3(b.Route.RoadWidthAt(d), 0.012f, 0.5f), paint);
                        stripe.transform.SetPositionAndRotation(pose.Offset(0f, 0.015f), pose.Rotation);
                        stripe.layer = Layers.World;
                    }
                    continue;
                }
                RouteSide side = zone.span == ZoneSpan.LeftEdge ? RouteSide.Left : RouteSide.Right;
                List<float> rows = RouteStrip.Rows(zone.start, zone.end, b.Gen.sampleStep);
                Vector3 origin = b.Path.Evaluate(zone.start).Position;
                Mesh mesh = RouteStrip.Build(b.Path, rows, side, d => {
                    float half = b.Route.RoadWidthAt(d) * 0.5f;
                    return new[] { new StripEdge(half - zone.edgeWidth, 0.015f), new StripEdge(half, 0.015f) };
                }, origin, StripFacing.Up, b.Gen.uvLength, null, "RumbleEdge");
                mesh = RoadMeshBuilder.Save(mesh, MeshFolder, $"Rumble_{side}_{zone.start:0}");
                GameObject strip = new GameObject("Rumble Edge");
                strip.transform.SetParent(parent, false);
                strip.transform.position = origin;
                strip.layer = Layers.World;
                strip.AddComponent<MeshFilter>().sharedMesh = mesh;
                strip.AddComponent<MeshRenderer>().sharedMaterial = paint;
            }
        }

        // ------------------------------------------------------------------ zones

        // The tunnel, bridge and rumble strip volumes (§3.3); the cliff's are its fall zones
        static void BuildZones(ref Built b, Transform parent) {
            foreach (RouteZone zone in b.Route.zones) {
                if (zone.kind == RouteZoneKind.Cliff) {
                    continue;
                }
                string name = zone.kind + (zone.span == ZoneSpan.Across ? "" : " " + zone.span) + $" {zone.start:0}–{zone.end:0}";
                ZoneVolume volume = NewVolume(name, parent, zone.kind, zone.start, zone.end);
                switch (zone.kind) {
                    case RouteZoneKind.Tunnel: volume.gameObject.AddComponent<TunnelZone>(); break;
                    case RouteZoneKind.RumbleStrip: volume.gameObject.AddComponent<RumbleZone>(); break;
                }
                float inner;
                float outer;
                float top;
                switch (zone.span) {
                    case ZoneSpan.LeftEdge:
                    case ZoneSpan.RightEdge:
                        // Along the road's edge, inside it
                        float half = b.Route.RoadWidthAt(zone.start) * 0.5f;
                        float sign = zone.span == ZoneSpan.LeftEdge ? -1f : 1f;
                        inner = sign * (half - zone.edgeWidth);
                        outer = sign * half;
                        top = 1.5f;
                        break;
                    default:
                        float reach = zone.kind == RouteZoneKind.Tunnel ? b.Gen.tunnelWallOffset : b.Gen.forestWallOffset;
                        inner = -reach;
                        outer = reach;
                        top = zone.kind == RouteZoneKind.Tunnel ? b.Gen.tunnelCeilingHeight : 3f;
                        break;
                }
                ZoneBoxes(b, volume, zone.start, zone.end, inner, outer, -0.5f, top);
                b.Zones.Add(volume);
            }
        }

        static ZoneVolume NewVolume(string name, Transform parent, RouteZoneKind kind, float start, float end) {
            GameObject go = Group(name, parent);
            go.layer = Layers.Zone;
            ZoneVolume volume = go.AddComponent<ZoneVolume>();
            SetInt(volume, "kind", (int)kind);
            SetFloat(volume, "start", start);
            SetFloat(volume, "end", end);
            return volume;
        }

        // Trigger boxes over [from, to], between two signed laterals, from `bottom` to `top` above the
        // road. Each box is at most ZoneBoxStep long.
        static void ZoneBoxes(Built b, ZoneVolume volume, float from, float to, float lateralA, float lateralB, float bottom, float top) {
            int count = Mathf.Max(1, Mathf.CeilToInt((to - from) / ZoneBoxStep - 1e-3f));
            float step = (to - from) / count;
            for (int i = 0; i < count; i++) {
                float y = b.Path.ElevationAt(from + step * (i + 0.5f));
                ZoneBox(b, volume, i, from + step * i, from + step * (i + 1), lateralA, lateralB, y + bottom, y + top);
            }
        }

        // One trigger box, at absolute heights. On a curve it is lengthened for its outer edge, so
        // neighbouring boxes overlap and leave no wedge-shaped gaps.
        static void ZoneBox(Built b, ZoneVolume volume, int index, float from, float to, float lateralA, float lateralB,
                            float yBottom, float yTop) {
            RoutePose pose = b.Path.Evaluate((from + to) * 0.5f);
            RouteSegment segment = b.Route.segments[pose.SegmentIndex];
            float outer = Mathf.Max(Mathf.Abs(lateralA), Mathf.Abs(lateralB));
            float stretch = segment.kind == SegmentKind.Arc ? (segment.radius + outer) / segment.radius : 1f;
            Vector3 center = pose.Offset((lateralA + lateralB) * 0.5f);
            center.y = (yBottom + yTop) * 0.5f;

            GameObject box = Group($"Box {index:00}", volume.transform);
            box.transform.SetPositionAndRotation(center, pose.Rotation);
            box.layer = Layers.Zone;
            BoxCollider collider = box.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(Mathf.Abs(lateralB - lateralA), yTop - yBottom, (to - from) * stretch + ZoneBoxOverlap);
            SetRef(box.AddComponent<ZoneTrigger>(), "volume", volume);
        }

        // §3.3 Cliff: fall zone boxes along the left edge, from 1 m outside the road edge and 40 m
        // out, from road height −1.5 m down to the valley floor +1 m. The fall camera's anchor sits on
        // the edge near the bend's end, looking back along it (§2.14 Fall).
        static void BuildCliff(ref Built b, Transform parent) {
            if (!b.Route.TryGetZone(RouteZoneKind.Cliff, out RouteZone cliff)) {
                throw new System.InvalidOperationException("route '" + b.Route.id + "' has no Cliff zone");
            }
            ZoneVolume volume = NewVolume("FallZone", parent, RouteZoneKind.Cliff, cliff.start, cliff.end);
            FallZone fall = volume.gameObject.AddComponent<FallZone>();
            float edge = b.Route.cliffRoadWidth * 0.5f;
            float inner = -(edge + b.Gen.fallZoneInset);
            float outer = inner - b.Gen.fallZoneWidth;
            int count = Mathf.Max(1, Mathf.CeilToInt((cliff.end - cliff.start) / ZoneBoxStep - 1e-3f));
            float step = (cliff.end - cliff.start) / count;
            for (int i = 0; i < count; i++) {
                float from = cliff.start + step * i;
                float top = b.Path.ElevationAt(from + step * 0.5f) - b.Gen.fallZoneTopBelowRoad;
                ZoneBox(b, volume, i, from, from + step, inner, outer, b.Gen.valleyFloorY + b.Gen.fallZoneAboveFloor, top);
            }
            b.Zones.Add(volume);
            b.FallZones.Add(fall);

            float at = cliff.end - (cliff.end - cliff.start) * 0.25f;
            RoutePose pose = b.Path.Evaluate(at);
            RoutePose target = b.Path.Evaluate(cliff.start + (cliff.end - cliff.start) * 0.35f);
            Transform anchor = Group(FallCamAnchorName, parent).transform;
            anchor.position = pose.Offset(-(RoadMeshBuilder.ShoulderEdge(b.Route, at, RouteSide.Left) + 1.5f), 3f);
            Vector3 look = target.Offset(-(edge + 4f), -6f) - anchor.position;
            anchor.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            b.FallCamAnchor = anchor;
        }

        // ------------------------------------------------------------------ map, root

        // The safety floor at −80 and the KillPlane trigger at −70 under the whole map (§3.4)
        static void BuildMap(ref Built b, Transform parent) {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            for (float d = 0f; d <= b.Path.TotalLength; d += 10f) {
                bounds.Encapsulate(b.Path.Evaluate(d).Position);
            }
            const float margin = 300f;
            Vector3 size = new Vector3(bounds.size.x + margin * 2f, 1f, bounds.size.z + margin * 2f);
            Vector3 center = new Vector3(bounds.center.x, b.Gen.safetyFloorY - 0.5f, bounds.center.z);
            Solid("Safety Floor", parent, center, Quaternion.identity, size, null, false);

            GameObject plane = Group("KillPlane", parent);
            plane.transform.position = new Vector3(bounds.center.x, b.Gen.killPlaneY, bounds.center.z);
            plane.layer = Layers.Zone;
            BoxCollider trigger = plane.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(size.x, 2f, size.z);
            b.KillPlane = plane.AddComponent<KillPlane>();
        }

        // The spawn marker, the ambience and the lists the night needs (§4.3)
        static void BuildRouteRoot(ref Built b, LightingPresetApplier lighting) {
            GameObject rootObject = new GameObject(RootName);
            RouteSceneRoot root = rootObject.AddComponent<RouteSceneRoot>();
            SceneAmbience ambience = rootObject.AddComponent<SceneAmbience>();

            // Right lane at depotSpawnDistance, facing the route direction
            RoutePose spawn = b.Path.Evaluate(b.Route.depotSpawnDistance);
            Transform spawnPoint = Group(SpawnName, rootObject.transform).transform;
            spawnPoint.SetPositionAndRotation(spawn.Offset(b.Route.roadWidth * 0.25f, 0.05f), spawn.Rotation);

            SetRef(root, "route", b.Route);
            SetRefArray(root, "stops", b.Stops.ToArray());
            SetRef(root, "busSpawn", spawnPoint);
            SetRefArray(root, "zones", b.Zones.ToArray());
            SetRefArray(root, "fallZones", b.FallZones.ToArray());
            SetRef(root, "killPlane", b.KillPlane);
            SetRef(root, "fallCamAnchor", b.FallCamAnchor);
            SetRefArray(root, "bindables", new Object[] { lighting, ambience });
        }
    }
}
