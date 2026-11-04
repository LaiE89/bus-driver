using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusDriver.Editor.Art {
    // The city prop/building kit delivered as OBJs (final_expo_27THoct) in Assets/Art/Environment/City.
    //
    // The OBJs are exported at 1/100 scale and all share one palette atlas, PolygonCity_Texture.png,
    // which wasn't in the delivery. So:
    // - CityKitModelImport imports every mesh ×100, without materials or colliders
    // - Run() makes one URP Lit material per look (tinted), each sampling the atlas: the real one
    //   when Textures/T_PolygonCity_BaseColor.png exists, else a generated greyscale stand-in that
    //   only shades each palette swatch a little lighter or darker so details still read
    // - and one prefab per mesh in Prefabs/, its pivot moved to the ground centre of the footprint
    //   (ART_CONTRACT §2.1); the meshes' fronts already face +Z. A shop's or the shelter's
    //   <name>_Glass mesh goes into the same prefab, on the Glass material.
    //
    // Six of the glass OBJs were exported Z-up and 100× too small (Shop_01/03/04/05, Shop_Corner_01,
    // BusStop_01); the copies in Meshes/ were converted to match the others before import.
    //
    // Re-running overwrites everything in place, so GUIDs and the scene's references survive. When
    // the real atlas arrives, drop it in as Textures/T_PolygonCity_BaseColor.png and run it again.
    public static class CityKitImport {
        public const string Folder = "Assets/Art/Environment/City";
        public const string MeshFolder = Folder + "/Meshes";
        public const string PrefabFolder = Folder + "/Prefabs";
        const string MaterialFolder = Folder + "/Materials";
        const string TextureFolder = Folder + "/Textures";
        public const string AtlasPath = TextureFolder + "/T_PolygonCity_BaseColor.png";
        const string StandInPath = TextureFolder + "/T_PolygonCity_StandIn_BaseColor.png";
        const int StandInSize = 256;
        // The atlas's swatch grid the stand-in shades, cells per side
        const int StandInCells = 64;

        // Look → tint (sRGB, ART_CONTRACT §2.4: 30–220) and smoothness. With the real atlas the
        // tint is white, except Glass, which stays see-through.
        struct Look {
            public string Name;
            public Color32 Tint;
            public float Smoothness;
            public bool Transparent;
            public Look(string name, byte r, byte g, byte b, float smoothness = 0.15f, bool transparent = false) {
                Name = name;
                Tint = new Color32(r, g, b, 255);
                Smoothness = smoothness;
                Transparent = transparent;
            }
        }

        static readonly Look[] Looks = {
            new Look("Brick", 140, 104, 88),
            new Look("Plaster", 158, 150, 134),
            new Look("Roof", 70, 70, 74),
            new Look("Metal", 112, 118, 116, 0.35f),
            new Look("Dumpster", 64, 92, 72, 0.3f),
            new Look("Hazard", 196, 96, 38, 0.3f),
            new Look("Cardboard", 156, 122, 84),
            new Look("TrashBag", 36, 36, 40, 0.55f),
            new Look("Wood", 118, 90, 64),
            new Look("Plant", 72, 98, 62),
            new Look("Sign", 176, 170, 160, 0.25f),
            new Look("Concrete", 128, 126, 120),
            new Look("Glass", 60, 74, 84, 0.9f, true),
        };

        // Mesh name prefix → look; the first match wins, so the specific names come first
        static readonly string[,] LookByPrefix = {
            { "SM_Bld_Shop_Cover", "Sign" },
            { "SM_Bld_Cover", "Metal" },
            { "SM_Bld_Shop_Corner_02", "Plaster" },
            { "SM_Bld_Shop_02", "Plaster" },
            { "SM_Bld_Shop_05", "Plaster" },
            { "SM_Bld_Shop", "Brick" },
            { "SM_Bld_Apartment_Roof", "Roof" },
            { "SM_Bld_Apartment", "Brick" },
            { "SM_Bld_OfficeOld", "Plaster" },
            { "SM_Bld_FireEscape", "Metal" },
            { "SM_Bld_Spire", "Roof" },
            { "SM_Prop_Skip", "Dumpster" },
            { "SM_Prop_Cone", "Hazard" },
            { "SM_Prop_Barrier", "Hazard" },
            { "SM_Prop_CarboardBox", "Cardboard" },
            { "SM_Prop_TrashBag", "TrashBag" },
            { "SM_Prop_ParkBench", "Wood" },
            { "SM_Env_Fence", "Wood" },
            { "SM_Prop_PotPlant", "Plant" },
            { "SM_Env_Tree", "Plant" },
            { "SM_Env_Grass", "Plant" },
            { "SM_Prop_Billboard_01", "Sign" },
            { "SM_Prop_Billboard_Roof", "Sign" },
            { "SM_Prop_Sign", "Sign" },
            { "SM_Prop_Newspaper", "Sign" },
            { "SM_Env_", "Concrete" },
            { "SM_Prop_Manhole", "Concrete" },
            { "SM_Prop_Sidewalk_Panel", "Concrete" },
        };

        [MenuItem("Tools/Bus Driver/Art/Import City Kit")]
        public static void Run() {
            EnsureFolder(MaterialFolder);
            EnsureFolder(TextureFolder);
            EnsureFolder(PrefabFolder);
            bool realAtlas = File.Exists(AtlasPath);
            if (!realAtlas) {
                WriteStandIn();
            }
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(realAtlas ? AtlasPath : StandInPath);

            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            foreach (Look look in Looks) {
                materials[look.Name] = BuildMaterial(look, atlas, realAtlas);
            }

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { MeshFolder })) {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.EndsWith(GlassSuffix)) {
                    AssetDatabase.DeleteAsset(PrefabPath(name));
                    continue;
                }
                Mesh mesh = MeshAt(path);
                if (mesh == null) {
                    Debug.LogWarning("CityKitImport: no mesh in " + path);
                    continue;
                }
                Mesh glass = MeshAt(MeshFolder + "/" + name + GlassSuffix + ".obj");
                BuildPrefab(name, mesh, materials[LookOf(name)], glass, materials["Glass"]);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"CityKitImport: {count} prefabs, atlas: {(realAtlas ? "PolygonCity" : "stand-in (PolygonCity_Texture.png missing)")}");
        }

        const string GlassSuffix = "_Glass";

        static Mesh MeshAt(string path) {
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path)) {
                if (sub is Mesh mesh) {
                    return mesh;
                }
            }
            return null;
        }

        public static string PrefabPath(string meshName) {
            return PrefabFolder + "/" + meshName + ".prefab";
        }

        static string LookOf(string meshName) {
            for (int i = 0; i < LookByPrefix.GetLength(0); i++) {
                string key = LookByPrefix[i, 0];
                if (key.StartsWith("_") ? meshName.EndsWith(key) : meshName.StartsWith(key)) {
                    return LookByPrefix[i, 1];
                }
            }
            return "Metal";
        }

        // Greyscale, one shade per swatch cell (a fixed hash, so it's the same every run)
        static void WriteStandIn() {
            Texture2D texture = new Texture2D(StandInSize, StandInSize, TextureFormat.RGB24, false);
            int cell = StandInSize / StandInCells;
            Color32[] pixels = new Color32[StandInSize * StandInSize];
            for (int cy = 0; cy < StandInCells; cy++) {
                for (int cx = 0; cx < StandInCells; cx++) {
                    uint h = (uint)(cx * 73856093) ^ (uint)(cy * 19349663);
                    h ^= h >> 13;
                    h *= 0x5bd1e995;
                    h ^= h >> 15;
                    byte v = (byte)(200 + h % 46);
                    for (int y = 0; y < cell; y++) {
                        for (int x = 0; x < cell; x++) {
                            pixels[(cy * cell + y) * StandInSize + cx * cell + x] = new Color32(v, v, v, 255);
                        }
                    }
                }
            }
            texture.SetPixels32(pixels);
            File.WriteAllBytes(StandInPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(StandInPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(StandInPath);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static Material BuildMaterial(Look look, Texture2D atlas, bool realAtlas) {
            string path = MaterialFolder + "/M_City_" + look.Name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (material == null) {
                material = new Material(lit);
                AssetDatabase.CreateAsset(material, path);
            } else {
                material.shader = lit;
            }
            Color tint = realAtlas && !look.Transparent ? Color.white : (Color)look.Tint;
            material.SetTexture("_BaseMap", look.Transparent ? null : atlas);
            material.SetColor("_BaseColor", look.Transparent ? new Color(tint.r, tint.g, tint.b, 0.45f) : tint);
            material.SetFloat("_Smoothness", look.Smoothness);
            material.SetFloat("_Metallic", 0f);
            if (look.Transparent) {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            } else {
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_ZWrite", 1f);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_Cull", (float)CullMode.Back);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = -1;
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Root at the ground centre of the footprint; the mesh on a child, shifted so its bounds'
        // XZ centre is under the root. Y stays as authored (the kit's y = 0 is the ground).
        static void BuildPrefab(string name, Mesh mesh, Material material, Mesh glass, Material glassMaterial) {
            GameObject root = new GameObject(name);
            Vector3 offset = new Vector3(-mesh.bounds.center.x, 0f, -mesh.bounds.center.z);
            MeshChild(root.transform, name, mesh, material, offset);
            if (glass != null) {
                MeshChild(root.transform, name + GlassSuffix, glass, glassMaterial, offset).shadowCastingMode = ShadowCastingMode.Off;
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
            Object.DestroyImmediate(root);
        }

        static MeshRenderer MeshChild(Transform parent, string name, Mesh mesh, Material material, Vector3 offset) {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = offset;
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        static void EnsureFolder(string path) {
            if (AssetDatabase.IsValidFolder(path)) {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    // Every OBJ in the city kit: ×100 (the export is in hundredths of a metre), the mesh only
    sealed class CityKitModelImport : AssetPostprocessor {
        void OnPreprocessModel() {
            if (!assetPath.StartsWith(CityKitImport.MeshFolder + "/")) {
                return;
            }
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.globalScale = 100f;
            importer.useFileScale = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.generateSecondaryUV = false;
        }
    }
}
