using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusDriver.Editor.Art {
    // Sets up the artist's bus (Bus_Asset_001, Appendix A.2) in Assets/Art/Vehicles/Bus: texture and
    // model import settings, the URP mask maps and the materials. Safe to run again after the artist
    // re-delivers: every output is overwritten in place, so GUIDs and references survive.
    //
    // The delivery's packing differs from Appendix A.0, so it's converted from Art/_Source:
    // - T_*_ORM (R occlusion, G roughness, B metallic) → T_*_MaskMap (R metallic, G AO, A smoothness),
    //   which URP Lit reads as both its metallic/smoothness map (R, A) and its occlusion map (G)
    // - T_*_O (opacity) → the alpha of T_*_BaseColor (Trim: cutout, Window: transparent)
    public static class BusArtImport {
        public const string Folder = "Assets/Art/Vehicles/Bus";
        public const string SourceFolder = "Assets/Art/_Source/Vehicles/Bus";
        public const string ModelPath = Folder + "/SK_Bus_00.fbx";
        public const string DoorClipPath = Folder + "/A_Bus_DoorOpen.fbx";
        const string TextureFolder = Folder + "/Textures";
        const string MaterialFolder = Folder + "/Materials";

        enum Surface { Opaque, Cutout, Transparent }

        struct MaterialSpec {
            public string Material;
            public string TextureSet;
            public Surface Surface;
            public MaterialSpec(string material, string textureSet, Surface surface = Surface.Opaque) {
                Material = material;
                TextureSet = textureSet;
                Surface = surface;
            }
        }

        // The FBX's material names → the texture set each one uses
        static readonly MaterialSpec[] Materials = {
            new MaterialSpec("M_Bus_Accessory_00", "T_Bus_Accessory_00"),
            new MaterialSpec("M_Bus_Control_00", "T_Bus_Control_00"),
            new MaterialSpec("M_Bus_Exterior_00", "T_Bus_Exterior_00"),
            new MaterialSpec("M_Bus_Interior_00", "T_Bus_Interior_00"),
            new MaterialSpec("M_Bus_Seats_00", "T_Bus_Seats_00"),
            new MaterialSpec("M_Bus_Trim_00", "T_Bus_Trim_00", Surface.Cutout),
            new MaterialSpec("M_Bus_Wheel_00", "T_Bus_Wheel_00"),
            new MaterialSpec("M_Bus_Window_00", "T_Bus_Window_00", Surface.Transparent),
            // The dash monitor's screen: no textures; the GPS canvas sits on top of it (D38)
            new MaterialSpec("M_Monitor_00", null),
        };

        [MenuItem("Tools/Bus Driver/Art/Import Bus")]
        public static void Run() {
            AssetDatabase.StartAssetEditing();
            try {
                foreach (MaterialSpec spec in Materials) {
                    if (spec.TextureSet == null) {
                        continue;
                    }
                    WriteMaskMap(spec.TextureSet);
                    if (spec.Surface != Surface.Opaque) {
                        WriteBaseColorWithOpacity(spec.TextureSet);
                    }
                }
            }finally {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();

            foreach (MaterialSpec spec in Materials) {
                if (spec.TextureSet == null) {
                    continue;
                }
                ConfigureTexture(TexturePath(spec.TextureSet, "BaseColor"), TextureImporterType.Default, true, spec.Surface != Surface.Opaque);
                ConfigureTexture(TexturePath(spec.TextureSet, "Normal"), TextureImporterType.NormalMap, false, false);
                ConfigureTexture(TexturePath(spec.TextureSet, "MaskMap"), TextureImporterType.Default, false, true);
            }

            foreach (MaterialSpec spec in Materials) {
                BuildMaterial(spec);
            }
            AssetDatabase.SaveAssets();

            ConfigureModel(ModelPath, false);
            ConfigureModel(DoorClipPath, true);
            Debug.Log("[ART] bus imported: " + ModelPath);
        }

        static string TexturePath(string set, string suffix) {
            return $"{TextureFolder}/{set}_{suffix}.png";
        }

        // ------------------------------------------------------------- textures

        static Texture2D LoadSource(string path) {
            if (!File.Exists(path)) {
                return null;
            }
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }

        static void WriteMaskMap(string set) {
            string source = $"{SourceFolder}/{set}_ORM.png";
            Texture2D orm = LoadSource(source);
            if (orm == null) {
                Debug.LogWarning("[ART] no " + source);
                return;
            }
            Color[] pixels = orm.GetPixels();
            for (int i = 0; i < pixels.Length; i++) {
                Color p = pixels[i];
                pixels[i] = new Color(p.b, p.r, 0f, 1f - p.g);
            }
            WritePng(TexturePath(set, "MaskMap"), orm.width, orm.height, pixels);
            Object.DestroyImmediate(orm);
        }

        static void WriteBaseColorWithOpacity(string set) {
            Texture2D color = LoadSource($"{SourceFolder}/{set}_BC.png");
            Texture2D opacity = LoadSource($"{SourceFolder}/{set}_O.png");
            if (color == null || opacity == null) {
                Debug.LogWarning("[ART] missing the base colour or opacity source for " + set);
                return;
            }
            Color[] pixels = color.GetPixels();
            for (int y = 0; y < color.height; y++) {
                for (int x = 0; x < color.width; x++) {
                    // The opacity map may be a different size: sample it by UV
                    float u = (x + 0.5f) / color.width;
                    float v = (y + 0.5f) / color.height;
                    Color o = opacity.GetPixelBilinear(u, v);
                    int i = y * color.width + x;
                    pixels[i].a = o.r;
                }
            }
            WritePng(TexturePath(set, "BaseColor"), color.width, color.height, pixels);
            Object.DestroyImmediate(color);
            Object.DestroyImmediate(opacity);
        }

        static void WritePng(string path, int width, int height, Color[] pixels) {
            Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            output.SetPixels(pixels);
            output.Apply();
            File.WriteAllBytes(path, output.EncodeToPNG());
            Object.DestroyImmediate(output);
        }

        static void ConfigureTexture(string path, TextureImporterType type, bool sRGB, bool alpha) {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) {
                Debug.LogWarning("[ART] no texture at " + path);
                return;
            }
            importer.textureType = type;
            importer.sRGBTexture = sRGB;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = alpha && sRGB;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------- materials

        static Material BuildMaterial(MaterialSpec spec) {
            string path = $"{MaterialFolder}/{spec.Material}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null) {
                Directory.CreateDirectory(MaterialFolder);
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = lit;
            mat.SetColor("_BaseColor", Color.white);

            if (spec.TextureSet == null) {
                // A dark, slightly glossy screen
                mat.SetColor("_BaseColor", new Color(0.02f, 0.025f, 0.03f));
                mat.SetFloat("_Smoothness", 0.85f);
                mat.SetFloat("_Metallic", 0f);
            }else {
                Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(spec.TextureSet, "BaseColor"));
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(spec.TextureSet, "Normal"));
                Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(spec.TextureSet, "MaskMap"));
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetTexture("_MainTex", baseMap);
                SetMap(mat, "_BumpMap", "_NORMALMAP", normal);
                SetMap(mat, "_MetallicGlossMap", "_METALLICSPECGLOSSMAP", mask);
                SetMap(mat, "_OcclusionMap", "_OCCLUSIONMAP", mask);
                mat.SetFloat("_BumpScale", 1f);
                mat.SetFloat("_OcclusionStrength", 1f);
                // With a metallic map, smoothness = map alpha × this
                mat.SetFloat("_Smoothness", 1f);
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            ApplySurface(mat, spec.Surface);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void SetMap(Material mat, string property, string keyword, Texture texture) {
            mat.SetTexture(property, texture);
            if (texture != null) {
                mat.EnableKeyword(keyword);
            }else {
                mat.DisableKeyword(keyword);
            }
        }

        // The property values and keywords URP's own material inspector would set
        static void ApplySurface(Material mat, Surface surface) {
            bool transparent = surface == Surface.Transparent;
            bool cutout = surface == Surface.Cutout;
            mat.SetFloat("_Surface", transparent ? 1f : 0f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            mat.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            mat.SetFloat("_SrcBlendAlpha", 1f);
            mat.SetFloat("_DstBlendAlpha", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            mat.SetFloat("_ZWrite", transparent ? 0f : 1f);
            mat.SetFloat("_Cull", transparent ? (float)CullMode.Off : (float)CullMode.Back);
            mat.SetOverrideTag("RenderType", transparent ? "Transparent" : cutout ? "TransparentCutout" : "Opaque");
            if (transparent) {
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }else {
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            if (cutout) {
                mat.EnableKeyword("_ALPHATEST_ON");
            }else {
                mat.DisableKeyword("_ALPHATEST_ON");
            }
            mat.renderQueue = transparent ? (int)RenderQueue.Transparent : cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            mat.SetShaderPassEnabled("DepthOnly", !transparent);
            mat.SetShaderPassEnabled("ShadowCaster", !transparent);
        }

        // ------------------------------------------------------------- models

        // Centimetres in the file (Maya), converted to metres by the file scale. The FBX carries
        // Maya lights and cameras for previewing; the game owns its own (§4.14), so they're dropped.
        static void ConfigureModel(string path, bool animationOnly) {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) {
                Debug.LogWarning("[ART] no model at " + path);
                return;
            }
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.optimizeGameObjects = false;
            importer.importAnimation = animationOnly;
            importer.materialImportMode = animationOnly ? ModelImporterMaterialImportMode.None : ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            if (!animationOnly) {
                foreach (MaterialSpec spec in Materials) {
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{spec.Material}.mat");
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), spec.Material), mat);
                }
            }
            importer.SaveAndReimport();
        }
    }
}
