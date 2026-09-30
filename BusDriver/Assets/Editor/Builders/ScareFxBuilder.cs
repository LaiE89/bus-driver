using System;
using System.IO;
using BusDriver.Core.Util;
using UnityEditor;
using UnityEngine;
using static BusDriver.Editor.Builders.BuilderUtil;

namespace BusDriver.Editor.Builders {
    // Part of BuildAll step 7 (T-M4-05): the greybox scare head (a pale sphere with dark eye
    // sockets and a mouth, on the ScareFx layer, no collider) and the generated scare textures:
    // the default overlay flash and the CCTV static noise. Both are deterministic (fixed seed), so
    // a rebuild writes the same bytes. Phase B replaces them with T_Scare_* art (Appendix A.5).
    public static class ScareFxBuilder {
        public const string PrefabFolder = PrefabBuilder.Folder + "/Scares";
        public const string ScareHeadPath = PrefabFolder + "/ScareHead_Greybox.prefab";
        public const string TextureFolder = GeneratedRoot + "/Textures/Scares";
        public const string FaceOverlayPath = TextureFolder + "/T_Scare_Face_Greybox.png";
        public const string StaticNoisePath = TextureFolder + "/T_Static_Noise.png";
        const int Seed = 1234;

        public static void Build() {
            EnsureFolder(TextureFolder);
            WriteTexture(FaceOverlayPath, FaceOverlay(), TextureWrapMode.Clamp);
            WriteTexture(StaticNoisePath, StaticNoise(), TextureWrapMode.Repeat);
            BuildScareHead();
        }

        static void BuildScareHead() {
            EnsureFolder(PrefabFolder);
            GameObject root = new GameObject("ScareHead_Greybox");
            // +Z is the face: ScarePlayer turns it to the camera
            GameObject skull = Prim(PrimitiveType.Sphere, "Head", root.transform, Vector3.zero, new Vector3(0.3f, 0.36f, 0.3f), MaterialLibraryBuilder.Get("ScareSkin"));
            foreach (float x in new[] { -0.28f, 0.28f }) {
                Prim(PrimitiveType.Sphere, x < 0f ? "SocketL" : "SocketR", skull.transform, new Vector3(x, 0.12f, 0.38f), new Vector3(0.3f, 0.22f, 0.25f), MaterialLibraryBuilder.Get("Face"));
            }
            Prim(PrimitiveType.Sphere, "Mouth", skull.transform, new Vector3(0f, -0.25f, 0.4f), new Vector3(0.28f, 0.34f, 0.2f), MaterialLibraryBuilder.Get("Face"));
            SetLayerRecursively(root, Layers.ScareFx);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>()) {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            SaveOrOverwritePrefab(root, ScareHeadPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        // A pale face out of the dark, 512×288 (16:9): the greybox flash
        static Texture2D FaceOverlay() {
            const int w = 512;
            const int h = 288;
            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    float u = (x - w * 0.5f) / (h * 0.5f);
                    float v = (y - h * 0.52f) / (h * 0.5f);
                    float face = Ellipse(u, v, 0.55f, 0.8f);
                    float eyes = Mathf.Max(Ellipse(u + 0.2f, v - 0.18f, 0.12f, 0.16f), Ellipse(u - 0.2f, v - 0.18f, 0.12f, 0.16f));
                    float mouth = Ellipse(u, v + 0.38f, 0.14f, 0.2f);
                    float skin = Mathf.Clamp01(face - eyes - mouth);
                    float shade = 0.02f + 0.72f * skin * (1f - 0.35f * Mathf.Abs(u));
                    pixels[y * w + x] = new Color(shade, shade * 1.02f, shade * 0.95f, 1f);
                }
            }
            texture.SetPixels(pixels);
            return texture;
        }

        // 1 inside, 0 outside, with a soft edge
        static float Ellipse(float u, float v, float rx, float ry) {
            float d = (u * u) / (rx * rx) + (v * v) / (ry * ry);
            return Mathf.Clamp01((1.08f - d) * 12f);
        }

        // Grey noise with a faint scanline, tiled across the screen
        static Texture2D StaticNoise() {
            const int size = 256;
            System.Random random = new System.Random(Seed);
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++) {
                float line = (y % 4) < 2 ? 1f : 0.8f;
                for (int x = 0; x < size; x++) {
                    float grey = (float)random.NextDouble() * line;
                    pixels[y * size + x] = new Color(grey, grey, grey, 1f);
                }
            }
            texture.SetPixels(pixels);
            return texture;
        }

        static void WriteTexture(string path, Texture2D texture, TextureWrapMode wrap) {
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            string full = Path.GetFullPath(path);
            // Rewriting identical bytes would still touch the file and churn the import
            if (!File.Exists(full) || !BytesEqual(File.ReadAllBytes(full), png)) {
                File.WriteAllBytes(full, png);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool dirty = importer.mipmapEnabled || importer.wrapMode != wrap || importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (dirty) {
                importer.mipmapEnabled = false;
                importer.wrapMode = wrap;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        static bool BytesEqual(byte[] a, byte[] b) {
            if (a.Length != b.Length) {
                return false;
            }
            for (int i = 0; i < a.Length; i++) {
                if (a[i] != b[i]) {
                    return false;
                }
            }
            return true;
        }

        public static Texture2D FaceOverlayTexture() {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(FaceOverlayPath);
        }

        public static Texture2D StaticNoiseTexture() {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(StaticNoisePath);
        }

        public static GameObject ScareHead() {
            GameObject head = AssetDatabase.LoadAssetAtPath<GameObject>(ScareHeadPath);
            if (head == null) {
                throw new InvalidOperationException("no " + ScareHeadPath + "; ScareFxBuilder runs in PrefabBuilder");
            }
            return head;
        }
    }
}
