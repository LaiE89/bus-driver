using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using UnityEngine;

namespace BusDriver.Editor.Builders {
    // A stretch of one side of the road with one cross-section profile (consecutive segments merged)
    public struct ProfileRun {
        public RouteSide Side;
        public SideProfile Profile;
        public float From;
        public float To;
    }

    // Where a side's containment line is left open for a stub's own walls (§3.3, §3.5)
    public struct RouteOpening {
        public RouteSide Side;
        public float From;
        public float To;
    }

    // A dressing object for RouteBuilder to instantiate as an environment view (T-M2-06/07)
    public struct DressingSpot {
        public RouteSide Side;
        public float Distance;
        public Vector3 Position;
        public float YawDeg;
        public float Scale;
        public int Variant;
    }

    // A straight piece of guardrail (or an end cap) starting at Position, running along Rotation
    public struct RailSpot {
        public RouteSide Side;
        public float Distance;
        public float Length;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    public sealed class ProfileBuildResult {
        public readonly List<ProfileRun> Runs = new List<ProfileRun>();
        public readonly List<DressingSpot> Trees = new List<DressingSpot>();
        public readonly List<DressingSpot> Rocks = new List<DressingSpot>();
        public readonly List<RailSpot> Guardrails = new List<RailSpot>();
        public readonly List<RailSpot> EndCaps = new List<RailSpot>();
        public readonly List<Vector3> TunnelLights = new List<Vector3>();
        public readonly List<Collider> Containment = new List<Collider>();
        public readonly List<Mesh> Meshes = new List<Mesh>();
    }

    // The cross-section profiles of §3.4 (T-M2-03): Forest ground, Rockface wall, Drop slope +
    // guardrail + invisible wall, CliffDrop face + valley floor, Water deck + railing + creek,
    // TunnelWall shell. It builds the continuous geometry and every containment collider (tagged
    // Containment, layer World, facing the road so the §3.5 rays hit them), and returns the
    // placements of the discrete dressing (trees, rocks, guardrail pieces, end caps, tunnel lights)
    // for RouteBuilder to instantiate from the EnvironmentViewSet. Deterministic: one dressing
    // stream seeded from RouteGeneration.dressingSeed, consumed left side first, in route order.
    public static class ProfileBuilder {
        const float WallBottom = -1f;

        struct Context {
            public RouteDefinition Route;
            public RoutePath Path;
            public RouteGeneration Gen;
            public RouteSide Side;
            public ProfileRun Run;
            public ProfileRun? Previous;
            public ProfileRun? Next;
            public Transform Root;
            public string Prefix;
            public string MeshFolder;
            public List<RouteOpening> Openings;
            public System.Random Random;
            public ProfileBuildResult Result;
        }

        // ------------------------------------------------------------------ runs

        public static List<ProfileRun> Runs(RouteDefinition route, RoutePath path, RouteSide side) {
            List<ProfileRun> runs = new List<ProfileRun>();
            for (int i = 0; i < route.segments.Length; i++) {
                SideProfile profile = side == RouteSide.Left ? route.segments[i].left : route.segments[i].right;
                float from = path.SegmentStart(i);
                float to = from + path.SegmentLength(i);
                if (runs.Count > 0 && runs[runs.Count - 1].Profile == profile) {
                    ProfileRun last = runs[runs.Count - 1];
                    last.To = to;
                    runs[runs.Count - 1] = last;
                }else {
                    runs.Add(new ProfileRun { Side = side, Profile = profile, From = from, To = to });
                }
            }
            return runs;
        }

        // The containment line's distance from the centreline (§3.4), or −1 where there is none (the cliff)
        public static float ContainmentOffset(RouteGeneration gen, SideProfile profile) {
            switch (profile) {
                case SideProfile.Forest: return gen.forestWallOffset;
                case SideProfile.Rockface: return gen.rockfaceOffset;
                case SideProfile.Drop: return gen.guardrailOffset;
                case SideProfile.Water: return gen.bridgeRailingOffset;
                case SideProfile.TunnelWall: return gen.tunnelWallOffset;
                default: return -1f;
            }
        }

        // ------------------------------------------------------------------ build

        public static ProfileBuildResult Build(RouteDefinition route, RoutePath path, Transform parent,
                                               IList<RouteOpening> openings, string meshFolder) {
            ProfileBuildResult result = new ProfileBuildResult();
            System.Random random = new System.Random(route.generation.dressingSeed);
            foreach (RouteSide side in new[] { RouteSide.Left, RouteSide.Right }) {
                Transform sideRoot = BuilderUtil.Group(side.ToString(), parent).transform;
                List<RouteOpening> sideOpenings = new List<RouteOpening>();
                if (openings != null) {
                    foreach (RouteOpening opening in openings) {
                        if (opening.Side == side) {
                            sideOpenings.Add(opening);
                        }
                    }
                }
                List<ProfileRun> runs = Runs(route, path, side);
                result.Runs.AddRange(runs);
                for (int i = 0; i < runs.Count; i++) {
                    ProfileRun run = runs[i];
                    Context ctx = new Context {
                        Route = route,
                        Path = path,
                        Gen = route.generation,
                        Side = side,
                        Run = run,
                        Previous = i > 0 ? runs[i - 1] : (ProfileRun?)null,
                        Next = i < runs.Count - 1 ? runs[i + 1] : (ProfileRun?)null,
                        Root = BuilderUtil.Group($"{i:00} {run.Profile} {run.From:0}–{run.To:0}", sideRoot).transform,
                        Prefix = $"{(side == RouteSide.Left ? "L" : "R")}{i:00}_{run.Profile}",
                        MeshFolder = meshFolder,
                        Openings = sideOpenings,
                        Random = random,
                        Result = result,
                    };
                    switch (run.Profile) {
                        case SideProfile.Forest: Forest(ctx); break;
                        case SideProfile.Rockface: Rockface(ctx); break;
                        case SideProfile.Drop: Drop(ctx); break;
                        case SideProfile.CliffDrop: CliffDrop(ctx); break;
                        case SideProfile.Water: Water(ctx); break;
                        case SideProfile.TunnelWall: TunnelWall(ctx); break;
                    }
                }
                Joints(route, path, side, runs, sideOpenings, sideRoot, meshFolder, result);
            }
            return result;
        }

        // Where a side's containment line jumps between offsets, a wall across the gap closes it
        static void Joints(RouteDefinition route, RoutePath path, RouteSide side, List<ProfileRun> runs,
                           List<RouteOpening> openings, Transform sideRoot, string meshFolder, ProfileBuildResult result) {
            RouteGeneration gen = route.generation;
            for (int i = 1; i < runs.Count; i++) {
                float a = ContainmentOffset(gen, runs[i - 1].Profile);
                float b = ContainmentOffset(gen, runs[i].Profile);
                float at = runs[i].From;
                if (a < 0f || b < 0f || Mathf.Abs(a - b) < 0.01f || InOpening(openings, at, 0f)) {
                    continue;
                }
                Vector3 origin = path.Evaluate(at).Position;
                string name = $"{(side == RouteSide.Left ? "L" : "R")}{i:00}_Joint";
                Mesh mesh = Save(RouteStrip.Joint(path, at, side, Mathf.Min(a, b), Mathf.Max(a, b), WallBottom, gen.containmentWallHeight, origin),
                    meshFolder, name, result);
                Create(sideRoot, $"Joint {at:0}", origin, mesh, null, true, true, result);
            }
        }

        // ------------------------------------------------------------------ profiles

        static float Drop0(Context ctx) {
            return -ctx.Gen.forestGroundDrop;
        }

        static void Forest(Context ctx) {
            RouteGeneration g = ctx.Gen;
            float ground = Drop0(ctx);
            Strip(ctx, "Ground", ctx.Run.From, ctx.Run.To,
                d => new[] { new StripEdge(Shoulder(ctx, d), ground), new StripEdge(g.forestGroundWidth, ground) },
                StripFacing.Up, "Ground", true, false);
            Walled(ctx, "Wall", ctx.Run.From, ctx.Run.To,
                d => new[] { new StripEdge(g.forestWallOffset, WallBottom), new StripEdge(g.forestWallOffset, g.containmentWallHeight) },
                null);

            // Two rows of trees between 8 and 16 m, 4 m apart ± 1.5 m (§3.4)
            int rows = Mathf.Max(1, g.treeRows);
            float band = (g.treeRowEnd - g.treeRowStart) / rows;
            for (int row = 0; row < rows; row++) {
                float centre = g.treeRowStart + band * (row + 0.5f);
                float lateralJitter = Mathf.Min(g.treeJitter, band * 0.5f);
                for (float d = ctx.Run.From + g.treeSpacing * 0.5f; d < ctx.Run.To; d += g.treeSpacing) {
                    float along = d + Jitter(ctx.Random, g.treeJitter);
                    float lateral = centre + Jitter(ctx.Random, lateralJitter);
                    float yaw = (float)ctx.Random.NextDouble() * 360f;
                    float scale = 0.85f + (float)ctx.Random.NextDouble() * 0.3f;
                    int variant = ctx.Random.Next(2);
                    if (!TreeAllowed(ctx, along)) {
                        continue;
                    }
                    RoutePose pose = ctx.Path.Evaluate(along);
                    ctx.Result.Trees.Add(new DressingSpot {
                        Side = ctx.Side, Distance = along, YawDeg = yaw, Scale = scale, Variant = variant,
                        Position = pose.Position + pose.Right * (lateral * RouteStrip.SideSign(ctx.Side)) + Vector3.up * ground,
                    });
                }
            }
        }

        static void Rockface(Context ctx) {
            RouteGeneration g = ctx.Gen;
            float ground = Drop0(ctx);
            Strip(ctx, "Ground", ctx.Run.From, ctx.Run.To,
                d => new[] { new StripEdge(Shoulder(ctx, d), ground), new StripEdge(g.rockfaceOffset, ground) },
                StripFacing.Up, "Ground", true, false);
            // 10 m high, leaning 10° away from the road; its own collider is the containment line
            float top = g.rockfaceOffset + g.rockfaceHeight * Mathf.Tan(g.rockfaceLean * Mathf.Deg2Rad);
            Walled(ctx, "Rock", ctx.Run.From, ctx.Run.To,
                d => new[] { new StripEdge(g.rockfaceOffset, -0.5f), new StripEdge(top, g.rockfaceHeight) },
                "Rock");
        }

        static void Drop(Context ctx) {
            RouteGeneration g = ctx.Gen;
            float ground = Drop0(ctx);
            float slopeRun = g.dropSlopeLength * Mathf.Cos(g.dropSlopeAngle * Mathf.Deg2Rad);
            float slopeFall = g.dropSlopeLength * Mathf.Sin(g.dropSlopeAngle * Mathf.Deg2Rad);
            float slopeOuter = g.guardrailOffset + slopeRun;
            float flatY = ground - slopeFall;
            Strip(ctx, "Ground", ctx.Run.From, ctx.Run.To,
                d => new[] {
                    new StripEdge(Shoulder(ctx, d), ground), new StripEdge(g.guardrailOffset, ground),
                    new StripEdge(slopeOuter, flatY), new StripEdge(slopeOuter + g.dropFlatLength, flatY),
                },
                StripFacing.Up, "Ground", true, false);

            // Next to the cliff the guardrail reaches 2 m into it, over the FallZone (§3.5.4)
            float from = ctx.Run.From;
            float to = ctx.Run.To;
            if (ctx.Previous.HasValue && ctx.Previous.Value.Profile == SideProfile.CliffDrop) {
                from = Mathf.Max(0f, from - g.endCapOverlap);
            }
            if (ctx.Next.HasValue && ctx.Next.Value.Profile == SideProfile.CliffDrop) {
                to = Mathf.Min(ctx.Path.TotalLength, to + g.endCapOverlap);
            }
            Walled(ctx, "Rail", from, to,
                d => new[] { new StripEdge(g.guardrailOffset, ground), new StripEdge(g.guardrailOffset, g.guardrailHeight) },
                null);
            Walled(ctx, "Wall", from, to,
                d => new[] { new StripEdge(g.guardrailOffset, WallBottom), new StripEdge(g.guardrailOffset, g.containmentWallHeight) },
                null);

            float sign = RouteStrip.SideSign(ctx.Side);
            for (float d = from; d < to - 0.01f; d += g.guardrailSegmentLength) {
                float length = Mathf.Min(g.guardrailSegmentLength, to - d);
                ctx.Result.Guardrails.Add(Rail(ctx, d, length, g.guardrailOffset * sign, false));
            }
            ctx.Result.EndCaps.Add(Rail(ctx, from, 0f, g.guardrailOffset * sign, true));
            ctx.Result.EndCaps.Add(Rail(ctx, to, 0f, g.guardrailOffset * sign, false));

            // Sparse trees on the flat below
            for (float d = ctx.Run.From + 7.5f; d < ctx.Run.To; d += 15f) {
                float lateral = slopeOuter + 3f + (float)ctx.Random.NextDouble() * (g.dropFlatLength - 6f);
                float yaw = (float)ctx.Random.NextDouble() * 360f;
                float scale = 0.85f + (float)ctx.Random.NextDouble() * 0.3f;
                int variant = ctx.Random.Next(2);
                RoutePose pose = ctx.Path.Evaluate(d);
                ctx.Result.Trees.Add(new DressingSpot {
                    Side = ctx.Side, Distance = d, YawDeg = yaw, Scale = scale, Variant = variant,
                    Position = pose.Position + pose.Right * (lateral * sign) + Vector3.up * flatY,
                });
            }
        }

        // No containment at all: the road edge, 0.3 m of gravel (the ribbon's shoulder), then a
        // vertical face down to the valley floor (§3.4). The FallZones come from the zones (T-M2-07).
        static void CliffDrop(Context ctx) {
            RouteGeneration g = ctx.Gen;
            Strip(ctx, "Face", ctx.Run.From, ctx.Run.To,
                d => new[] { new StripEdge(Shoulder(ctx, d), 0f), new StripEdge(Shoulder(ctx, d), g.valleyFloorY, true) },
                StripFacing.AwayFromRoad, "Rock", true, false);
            float from = Mathf.Max(0f, ctx.Run.From - 20f);
            float to = Mathf.Min(ctx.Path.TotalLength, ctx.Run.To + 20f);
            Strip(ctx, "ValleyFloor", from, to,
                d => new[] { new StripEdge(Shoulder(ctx, d), g.valleyFloorY, true), new StripEdge(Shoulder(ctx, d) + g.valleyFloorDepth, g.valleyFloorY, true) },
                StripFacing.Up, "Ground", true, false);

            float sign = RouteStrip.SideSign(ctx.Side);
            for (float d = ctx.Run.From + 5f; d < ctx.Run.To; d += 10f) {
                for (int k = 0; k < 2; k++) {
                    float lateral = Shoulder(ctx, d) + 8f + (float)ctx.Random.NextDouble() * (g.valleyFloorDepth - 16f);
                    float yaw = (float)ctx.Random.NextDouble() * 360f;
                    float scale = 0.6f + (float)ctx.Random.NextDouble() * 1.2f;
                    int variant = ctx.Random.Next(2);
                    RoutePose pose = ctx.Path.Evaluate(d);
                    Vector3 position = pose.Position + pose.Right * (lateral * sign);
                    position.y = g.valleyFloorY;
                    ctx.Result.Rocks.Add(new DressingSpot { Side = ctx.Side, Distance = d, Position = position, YawDeg = yaw, Scale = scale, Variant = variant });
                }
            }
        }

        static void Water(Context ctx) {
            RouteGeneration g = ctx.Gen;
            const float deckThickness = 0.8f;
            float deckEdge = g.bridgeRailingOffset + 0.3f;
            // The deck's top outside the ribbon, its outer face and its underside, as one outline
            Strip(ctx, "Deck", ctx.Run.From, ctx.Run.To,
                d => new[] {
                    new StripEdge(Shoulder(ctx, d), 0f), new StripEdge(deckEdge, 0f),
                    new StripEdge(deckEdge, -deckThickness), new StripEdge(0f, -deckThickness),
                },
                StripFacing.Up, "Wall", true, false);
            Strip(ctx, "Railing", ctx.Run.From, ctx.Run.To,
                d => new[] { new StripEdge(g.bridgeRailingOffset, 0.55f), new StripEdge(g.bridgeRailingOffset, 1f) },
                StripFacing.TowardRoad, "GuardRail", false, false);
            Walled(ctx, "RailingCollider", ctx.Run.From, ctx.Run.To,
                d => new[] { new StripEdge(g.bridgeRailingOffset, 0f), new StripEdge(g.bridgeRailingOffset, 1f) },
                null);
            Walled(ctx, "Wall", ctx.Run.From, ctx.Run.To,
                d => new[] { new StripEdge(g.bridgeRailingOffset, WallBottom), new StripEdge(g.bridgeRailingOffset, g.containmentWallHeight) },
                null);

            // The creek, 8 m below the deck, flat and wider than the bridge
            float creekY = ctx.Path.ElevationAt(ctx.Run.From) - g.creekDepth;
            Strip(ctx, "Creek", Mathf.Max(0f, ctx.Run.From - 30f), Mathf.Min(ctx.Path.TotalLength, ctx.Run.To + 30f),
                d => new[] { new StripEdge(0f, creekY, true), new StripEdge(60f, creekY, true) },
                StripFacing.Up, "Water", false, false);
        }

        static void TunnelWall(Context ctx) {
            RouteGeneration g = ctx.Gen;
            float ground = Drop0(ctx);
            Strip(ctx, "Ground", ctx.Run.From, ctx.Run.To,
                d => new[] { new StripEdge(Shoulder(ctx, d), ground), new StripEdge(g.tunnelWallOffset, ground) },
                StripFacing.Up, "Ground", true, false);
            Walled(ctx, "Wall", ctx.Run.From, ctx.Run.To,
                d => new[] { new StripEdge(g.tunnelWallOffset, ground), new StripEdge(g.tunnelWallOffset, g.tunnelCeilingHeight) },
                "Wall");
            Strip(ctx, "Ceiling", ctx.Run.From, ctx.Run.To,
                d => new[] { new StripEdge(0f, g.tunnelCeilingHeight), new StripEdge(g.tunnelWallOffset, g.tunnelCeilingHeight) },
                StripFacing.Down, "Wall", false, false);
            // One row of ceiling lights down the middle, placed once (from the right side)
            if (ctx.Side == RouteSide.Right) {
                for (float d = ctx.Run.From + g.tunnelLightSpacing * 0.5f; d < ctx.Run.To; d += g.tunnelLightSpacing) {
                    ctx.Result.TunnelLights.Add(ctx.Path.Evaluate(d).Position + Vector3.up * (g.tunnelCeilingHeight - 0.15f));
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        static float Shoulder(Context ctx, float distance) {
            return RoadMeshBuilder.ShoulderEdge(ctx.Route, distance, ctx.Side);
        }

        static float Jitter(System.Random random, float amount) {
            return ((float)random.NextDouble() * 2f - 1f) * amount;
        }

        static bool InOpening(List<RouteOpening> openings, float distance, float margin) {
            for (int i = 0; i < openings.Count; i++) {
                if (distance >= openings[i].From - margin && distance <= openings[i].To + margin) {
                    return true;
                }
            }
            return false;
        }

        // Trees keep clear of stub mouths, the depot pad and the lodge
        static bool TreeAllowed(Context ctx, float distance) {
            if (distance < ctx.Run.From || distance > ctx.Run.To || InOpening(ctx.Openings, distance, 12f)) {
                return false;
            }
            if (ctx.Side == RouteSide.Left && distance < ctx.Route.depotPadEnd + 10f) {
                return false;
            }
            return distance < ctx.Path.TotalLength - 40f;
        }

        static RailSpot Rail(Context ctx, float distance, float length, float lateral, bool reversed) {
            RoutePose start = ctx.Path.Evaluate(distance);
            Vector3 position = start.Position + start.Right * lateral;
            Vector3 direction = start.Forward;
            if (length > 0f) {
                RoutePose end = ctx.Path.Evaluate(distance + length);
                direction = end.Position + end.Right * lateral - position;
                direction.y = 0f;
            }
            if (reversed) {
                direction = -direction;
            }
            return new RailSpot {
                Side = ctx.Side, Distance = distance, Length = length, Position = position,
                Rotation = Quaternion.LookRotation(direction.normalized, Vector3.up),
            };
        }

        // A rendered (and optionally colliding) strip over [from, to]
        static void Strip(Context ctx, string part, float from, float to, Func<float, StripEdge[]> edges,
                          StripFacing facing, string material, bool collider, bool containment) {
            List<float> rows = RouteStrip.Rows(from, to, ctx.Gen.sampleStep);
            if (rows.Count < 2) {
                return;
            }
            Vector3 origin = ctx.Path.Evaluate(from).Position;
            Mesh mesh = Save(RouteStrip.Build(ctx.Path, rows, ctx.Side, edges, origin, facing, ctx.Gen.uvLength), ctx.MeshFolder,
                ctx.Prefix + "_" + part, ctx.Result);
            Create(ctx.Root, part, origin, mesh, material != null ? MaterialLibraryBuilder.Get(material) : null, collider, containment, ctx.Result);
        }

        // A containment strip facing the road, split around the side's openings. With a material
        // it is also rendered (the rock face, the tunnel walls); without, it's an invisible wall.
        static void Walled(Context ctx, string part, float from, float to, Func<float, StripEdge[]> edges, string material) {
            List<Vector2> pieces = new List<Vector2> { new Vector2(from, to) };
            foreach (RouteOpening opening in ctx.Openings) {
                List<Vector2> next = new List<Vector2>();
                foreach (Vector2 piece in pieces) {
                    if (opening.To <= piece.x || opening.From >= piece.y) {
                        next.Add(piece);
                        continue;
                    }
                    if (opening.From > piece.x) {
                        next.Add(new Vector2(piece.x, opening.From));
                    }
                    if (opening.To < piece.y) {
                        next.Add(new Vector2(opening.To, piece.y));
                    }
                }
                pieces = next;
            }
            for (int i = 0; i < pieces.Count; i++) {
                if (pieces[i].y - pieces[i].x < 0.25f) {
                    continue;
                }
                string name = pieces.Count > 1 ? part + i : part;
                Strip(ctx, name, pieces[i].x, pieces[i].y, edges, StripFacing.TowardRoad, material, true, true);
            }
        }

        static Mesh Save(Mesh mesh, string meshFolder, string name, ProfileBuildResult result) {
            Mesh saved = RoadMeshBuilder.Save(mesh, meshFolder, name);
            result.Meshes.Add(saved);
            return saved;
        }

        static GameObject Create(Transform parent, string name, Vector3 origin, Mesh mesh, Material material,
                                 bool collider, bool containment, ProfileBuildResult result) {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = origin;
            go.layer = Layers.World;
            go.isStatic = true;
            if (material != null) {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            if (collider) {
                MeshCollider meshCollider = go.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
                if (containment) {
                    go.tag = Tags.Containment;
                    result.Containment.Add(meshCollider);
                }
            }
            return go;
        }
    }
}
