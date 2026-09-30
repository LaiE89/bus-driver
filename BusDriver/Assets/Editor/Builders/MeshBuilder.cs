using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Editor.Builders {
    // BuildAll step 3 (§4.15): the procedural shared meshes the environment views use, written to
    // Generated/Meshes/Shared in place (GUIDs survive rebuilds). Faceted low-poly: every triangle
    // has its own vertices, so RecalculateNormals gives flat shading. Deterministic: the rock's
    // jitter comes from a fixed seed.
    public static class MeshBuilder {
        public const string Folder = BuilderUtil.GeneratedRoot + "/Meshes/Shared";
        const int RockSeed = 1234;

        public struct TreeShape {
            public string Name;
            public float Height;
            public float Radius;
            public int Tiers;
        }

        // Conifer.A tall and narrow, Conifer.B shorter and fuller
        public static readonly TreeShape[] Trees = {
            new TreeShape { Name = "Tree_ConiferA", Height = 11f, Radius = 2.4f, Tiers = 3 },
            new TreeShape { Name = "Tree_ConiferB", Height = 8.5f, Radius = 3.1f, Tiers = 4 },
        };

        public static string PathOf(string name) {
            return Folder + "/" + name + ".asset";
        }

        [MenuItem("Tools/Bus Driver/Builders/Shared Meshes")]
        public static void Build() {
            BuilderUtil.EnsureFolder(Folder);
            foreach (TreeShape tree in Trees) {
                BuilderUtil.SaveMesh(TreeMesh(tree, 10, tree.Tiers, 8), PathOf(tree.Name + "_LOD0"));
                BuilderUtil.SaveMesh(TreeMesh(tree, 6, 1, 4), PathOf(tree.Name + "_LOD1"));
            }
            BuilderUtil.SaveMesh(RockMesh(), PathOf("RockChunk"));
            BuilderUtil.SaveMesh(ChevronMesh(), PathOf("Chevron"));
            BuilderUtil.SaveMesh(HoodMesh(), PathOf(HoodMeshName));
            AssetDatabase.SaveAssets();
        }

        // Builders run this step before the prefabs, so a missing mesh is a builder bug
        public static Mesh Get(string name) {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(PathOf(name));
            if (mesh == null) {
                throw new System.InvalidOperationException($"no shared mesh '{name}'; run MeshBuilder first");
            }
            return mesh;
        }

        // ------------------------------------------------------------------ meshes

        sealed class Faceted {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int>[] Submeshes;

            public Faceted(int submeshes) {
                Submeshes = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++) {
                    Submeshes[i] = new List<int>();
                }
            }

            // Unity's front face: the normal is cross(b − a, c − a)
            public void Triangle(int submesh, Vector3 a, Vector3 b, Vector3 c) {
                int start = Vertices.Count;
                Vertices.Add(a);
                Vertices.Add(b);
                Vertices.Add(c);
                Submeshes[submesh].Add(start);
                Submeshes[submesh].Add(start + 1);
                Submeshes[submesh].Add(start + 2);
            }

            public Mesh ToMesh(string name) {
                Mesh mesh = new Mesh { name = name };
                mesh.SetVertices(Vertices);
                mesh.subMeshCount = Submeshes.Length;
                for (int i = 0; i < Submeshes.Length; i++) {
                    mesh.SetTriangles(Submeshes[i], i);
                }
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        static Vector3 Ring(float angle, float radius, float y) {
            return new Vector3(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius);
        }

        // Submesh 0 is the trunk (Bark), 1 the foliage (Foliage). Pivot at the trunk's base.
        static Mesh TreeMesh(TreeShape shape, int sides, int tiers, int trunkSides) {
            Faceted mesh = new Faceted(2);
            float trunkRadius = 0.18f;
            float trunkTop = shape.Height * 0.3f;
            float step = Mathf.PI * 2f / trunkSides;
            for (int i = 0; i < trunkSides; i++) {
                float a0 = i * step;
                float a1 = (i + 1) * step;
                Vector3 b0 = Ring(a0, trunkRadius, 0f);
                Vector3 b1 = Ring(a1, trunkRadius, 0f);
                Vector3 t0 = Ring(a0, trunkRadius * 0.7f, trunkTop);
                Vector3 t1 = Ring(a1, trunkRadius * 0.7f, trunkTop);
                mesh.Triangle(0, b0, t1, t0);
                mesh.Triangle(0, b0, b1, t1);
            }

            // Stacked cones, each smaller and higher, overlapping the one below
            float foliageBottom = shape.Height * 0.18f;
            float tierHeight = (shape.Height - foliageBottom) / (tiers * 0.7f + 0.3f);
            step = Mathf.PI * 2f / sides;
            for (int tier = 0; tier < tiers; tier++) {
                float bottom = foliageBottom + tier * tierHeight * 0.7f;
                float top = tier == tiers - 1 ? shape.Height : bottom + tierHeight;
                float radius = shape.Radius * (1f - tier / (float)(tiers + 1));
                Vector3 apex = new Vector3(0f, top, 0f);
                Vector3 centre = new Vector3(0f, bottom, 0f);
                for (int i = 0; i < sides; i++) {
                    Vector3 r0 = Ring(i * step, radius, bottom);
                    Vector3 r1 = Ring((i + 1) * step, radius, bottom);
                    mesh.Triangle(1, r0, r1, apex);
                    mesh.Triangle(1, r0, centre, r1);
                }
            }
            return mesh.ToMesh(shape.Name);
        }

        // A jittered, flattened octahedron subdivided once: about 1 m across, pivot at its base
        static Mesh RockMesh() {
            System.Random random = new System.Random(RockSeed);
            Vector3[] corners = {
                Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back,
            };
            int[] faces = {
                0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4,
                1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2,
            };
            Dictionary<Vector3, Vector3> jittered = new Dictionary<Vector3, Vector3>();
            Vector3 Jitter(Vector3 p) {
                Vector3 key = p.normalized;
                Vector3 value;
                if (!jittered.TryGetValue(key, out value)) {
                    float scale = 0.8f + (float)random.NextDouble() * 0.35f;
                    value = new Vector3(key.x * 0.6f, key.y * 0.35f + 0.3f, key.z * 0.55f) * scale;
                    value.y = Mathf.Max(0f, value.y);
                    jittered[key] = value;
                }
                return value;
            }
            Faceted mesh = new Faceted(1);
            for (int f = 0; f < faces.Length; f += 3) {
                Vector3 a = corners[faces[f]];
                Vector3 b = corners[faces[f + 1]];
                Vector3 c = corners[faces[f + 2]];
                Vector3 ab = (a + b) * 0.5f;
                Vector3 bc = (b + c) * 0.5f;
                Vector3 ca = (c + a) * 0.5f;
                mesh.Triangle(0, Jitter(a), Jitter(ab), Jitter(ca));
                mesh.Triangle(0, Jitter(ab), Jitter(b), Jitter(bc));
                mesh.Triangle(0, Jitter(ca), Jitter(bc), Jitter(c));
                mesh.Triangle(0, Jitter(ab), Jitter(bc), Jitter(ca));
            }
            return mesh.ToMesh("RockChunk");
        }

        // A flat ">" pointing +X, 0.6 m × 0.75 m, facing −Z (toward approaching traffic)
        public const string HoodMeshName = "HoodCone";

        // The HoodUp decoy's dark cone over a passenger's head (§4.14): pivot at its base centre,
        // 0.36 m across and 0.42 m tall, closed underneath
        static Mesh HoodMesh() {
            Faceted mesh = new Faceted(1);
            const int sides = 12;
            const float radius = 0.18f;
            const float height = 0.42f;
            Vector3 tip = new Vector3(0f, height, 0f);
            float step = Mathf.PI * 2f / sides;
            for (int i = 0; i < sides; i++) {
                Vector3 a = Ring(i * step, radius, 0f);
                Vector3 b = Ring((i + 1) * step, radius, 0f);
                mesh.Triangle(0, a, b, tip);
                mesh.Triangle(0, a, Vector3.zero, b);
            }
            return mesh.ToMesh(HoodMeshName);
        }

        static Mesh ChevronMesh() {
            Faceted mesh = new Faceted(1);
            Vector3 topLeft = new Vector3(-0.3f, 0.375f, 0f);
            Vector3 topInner = new Vector3(-0.1f, 0.375f, 0f);
            Vector3 tip = new Vector3(0.3f, 0f, 0f);
            Vector3 notch = new Vector3(0.1f, 0f, 0f);
            Vector3 bottomInner = new Vector3(-0.1f, -0.375f, 0f);
            Vector3 bottomLeft = new Vector3(-0.3f, -0.375f, 0f);
            mesh.Triangle(0, topLeft, topInner, tip);
            mesh.Triangle(0, topLeft, tip, notch);
            mesh.Triangle(0, notch, tip, bottomInner);
            mesh.Triangle(0, notch, bottomInner, bottomLeft);
            return mesh.ToMesh("Chevron");
        }
    }
}
