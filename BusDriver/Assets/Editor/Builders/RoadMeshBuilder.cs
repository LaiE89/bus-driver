using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Rules;
using BusDriver.Core.Util;
using UnityEngine;

namespace BusDriver.Editor.Builders {
    // The road ribbon (§3.1, §3.4; T-M2-03): 100 m chunks of asphalt plus shoulders, one row per
    // metre, u across the ribbon and v = distance / 4. The ribbon narrows to 6.5 m with a 0.3 m
    // outer shoulder through the cliff. Centre-line dashes are separate, non-static meshes: small
    // emissive props render magenta when static-batched (§4.17). RouteBuilder (T-M2-07) calls Build.
    public static class RoadMeshBuilder {
        public const int RoadSubmesh = 0;
        public const int ShoulderSubmesh = 1;
        public const float DashLength = 2f;
        public const float DashSpacing = 6f;
        public const float DashWidth = 0.15f;
        public const float DashLift = 0.02f;

        // The four ribbon edges at a distance, as unsigned distances from the centreline:
        // left shoulder, left road edge, right road edge, right shoulder
        public static void CrossSection(RouteDefinition route, float distance, out float leftShoulder, out float leftRoad,
                                        out float rightRoad, out float rightShoulder) {
            float half = route.RoadWidthAt(distance) * 0.5f;
            bool cliff = route.InZone(RouteZoneKind.Cliff, distance);
            leftRoad = half;
            rightRoad = half;
            leftShoulder = half + (cliff ? route.cliffOuterShoulder : route.shoulderWidth);
            rightShoulder = half + route.shoulderWidth;
        }

        // Unsigned distance from the centreline to the outer edge of the shoulder on one side
        public static float ShoulderEdge(RouteDefinition route, float distance, RouteSide side) {
            CrossSection(route, distance, out float leftShoulder, out _, out _, out float rightShoulder);
            return side == RouteSide.Left ? leftShoulder : rightShoulder;
        }

        // One chunk's ribbon; submesh 0 is the road, submesh 1 both shoulders. Vertices are
        // relative to origin.
        public static Mesh RoadMesh(RouteDefinition route, RoutePath path, float from, float to, Vector3 origin) {
            List<float> rows = RouteStrip.Rows(from, to, route.generation.sampleStep);
            // Columns run left to right, so the strip is built on the right side with signed laterals
            return RouteStrip.Build(path, rows, RouteSide.Right, d => {
                CrossSection(route, d, out float ls, out float lr, out float rr, out float rs);
                return new[] {
                    new StripEdge(-ls, 0f), new StripEdge(-lr, 0f), new StripEdge(rr, 0f), new StripEdge(rs, 0f),
                };
            }, origin, StripFacing.Up, route.generation.uvLength,
                new[] { ShoulderSubmesh, RoadSubmesh, ShoulderSubmesh }, "Road");
        }

        // The dashes starting inside [from, to), each 2 m long with a mid row so it follows curves
        public static Mesh DashMesh(RouteDefinition route, RoutePath path, float from, float to, Vector3 origin) {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            float first = Mathf.Ceil(from / DashSpacing) * DashSpacing;
            for (float start = first; start < to && start + DashLength <= path.TotalLength; start += DashSpacing) {
                int baseIndex = vertices.Count;
                for (int r = 0; r <= 2; r++) {
                    RoutePose pose = path.Evaluate(start + DashLength * r / 2f);
                    Vector3 lift = Vector3.up * DashLift;
                    vertices.Add(pose.Position - pose.Right * (DashWidth * 0.5f) + lift - origin);
                    vertices.Add(pose.Position + pose.Right * (DashWidth * 0.5f) + lift - origin);
                    uvs.Add(new Vector2(0f, r / 2f));
                    uvs.Add(new Vector2(1f, r / 2f));
                }
                for (int r = 0; r < 2; r++) {
                    int a = baseIndex + r * 2;
                    // Left to right with rows forward: (a, next, b) faces up (see RouteStrip)
                    triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(a + 2); triangles.Add(a + 3);
                }
            }
            Mesh mesh = new Mesh { name = "Dashes" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // The chunk boundaries: every chunkLength, the last one shorter
        public static List<Vector2> Chunks(RouteDefinition route, RoutePath path) {
            List<Vector2> chunks = new List<Vector2>();
            float length = route.generation.chunkLength;
            for (float from = 0f; from < path.TotalLength - 1e-3f; from += length) {
                chunks.Add(new Vector2(from, Mathf.Min(from + length, path.TotalLength)));
            }
            return chunks;
        }

        // Builds every chunk under parent: the ribbon with a MeshCollider (static), and its dashes
        // (non-static, no collider). meshFolder null keeps the meshes in memory (tests).
        public static List<GameObject> Build(RouteDefinition route, RoutePath path, Transform parent, string meshFolder) {
            Material asphalt = MaterialLibraryBuilder.Get("Asphalt");
            Material shoulder = MaterialLibraryBuilder.Get("Gravel");
            Material paint = MaterialLibraryBuilder.Get("LinePaint");
            List<GameObject> built = new List<GameObject>();
            List<Vector2> chunks = Chunks(route, path);
            for (int i = 0; i < chunks.Count; i++) {
                float from = chunks[i].x;
                float to = chunks[i].y;
                Vector3 origin = path.Evaluate(from).Position;
                string name = $"Road_{i:000}";

                Mesh road = Save(RoadMesh(route, path, from, to, origin), meshFolder, name);
                GameObject chunk = new GameObject($"Road {from:0}–{to:0}");
                chunk.transform.SetParent(parent, false);
                chunk.transform.position = origin;
                chunk.layer = Layers.World;
                chunk.AddComponent<MeshFilter>().sharedMesh = road;
                chunk.AddComponent<MeshRenderer>().sharedMaterials = new[] { asphalt, shoulder };
                chunk.AddComponent<MeshCollider>().sharedMesh = road;
                chunk.isStatic = true;

                Mesh dashes = Save(DashMesh(route, path, from, to, origin), meshFolder, name + "_Dashes");
                GameObject dashObject = new GameObject("Dashes");
                dashObject.transform.SetParent(chunk.transform, false);
                dashObject.layer = Layers.World;
                dashObject.AddComponent<MeshFilter>().sharedMesh = dashes;
                MeshRenderer dashRenderer = dashObject.AddComponent<MeshRenderer>();
                dashRenderer.sharedMaterial = paint;
                dashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dashObject.isStatic = false;
                built.Add(chunk);
            }
            return built;
        }

        public static Mesh Save(Mesh mesh, string meshFolder, string name) {
            mesh.name = name;
            return meshFolder == null ? mesh : BuilderUtil.SaveMesh(mesh, meshFolder + "/" + name + ".asset");
        }
    }
}
