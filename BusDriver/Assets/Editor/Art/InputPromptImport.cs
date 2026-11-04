using System.Collections.Generic;
using System.IO;
using BusDriver.Editor.Builders;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace BusDriver.Editor.Art {
    // Packs the controller button glyphs (Kenney's CC0 "Input Prompts" 1.5, filled black/white,
    // 128 px) in Art/_Source/UI/InputPrompts into one atlas and a TMP sprite asset, so HUD prompts
    // can show <sprite name="ps_cross"> instead of the word (§4.13). Each PNG's file name is its
    // sprite name, and DeviceFamilies.GamepadGlyphName must use the same names. Safe to run again:
    // the atlas and the asset are overwritten in place, so GUIDs and references survive.
    public static class InputPromptImport {
        public const string SourceFolder = "Assets/Art/_Source/UI/InputPrompts";
        public const string Folder = "Assets/Art/UI/InputPrompts";
        public const string AtlasPath = Folder + "/InputPrompts_Atlas.png";
        public const string SpriteAssetPath = Folder + "/InputPrompts.asset";

        const int Cell = 128;
        // Transparent gutter round each glyph so mipmaps don't bleed neighbours together
        const int Padding = 4;
        const int Columns = 8;
        // Glyph size relative to the font's ascender (TMP's default sprite size), and where its
        // middle sits: about half the cap height, so it lines up with the prompt's words
        const float GlyphScale = 1.35f;
        const float CentreAboveBaseline = 0.42f;

        [MenuItem("Tools/Bus Driver/Art/Import Input Prompts")]
        public static void ImportMenu() {
            TMP_SpriteAsset asset = Build();
            AssignToHud(asset);
        }

        // The sprite asset, building it first if it isn't there yet
        public static TMP_SpriteAsset LoadOrBuild() {
            TMP_SpriteAsset asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
            return asset != null ? asset : Build();
        }

        public static TMP_SpriteAsset Build() {
            string[] files = Directory.GetFiles(SourceFolder, "*.png");
            System.Array.Sort(files, System.StringComparer.Ordinal);
            if (files.Length == 0) {
                Debug.LogError("[InputPromptImport] no glyph PNGs in " + SourceFolder);
                return null;
            }

            // ---- atlas
            int stride = Cell + Padding * 2;
            int rows = (files.Length + Columns - 1) / Columns;
            Texture2D atlas = new Texture2D(Columns * stride, rows * stride, TextureFormat.RGBA32, false);
            Color32[] clear = new Color32[atlas.width * atlas.height];
            atlas.SetPixels32(clear);
            List<string> names = new List<string>();
            List<RectInt> rects = new List<RectInt>();
            for (int i = 0; i < files.Length; i++) {
                Texture2D glyph = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                glyph.LoadImage(File.ReadAllBytes(files[i]));
                if (glyph.width != Cell || glyph.height != Cell) {
                    Debug.LogError($"[InputPromptImport] {files[i]} is {glyph.width}x{glyph.height}, expected {Cell}x{Cell}");
                    Object.DestroyImmediate(glyph);
                    continue;
                }
                int column = names.Count % Columns;
                int row = names.Count / Columns;
                // Row 0 at the top of the image; texture coordinates count from the bottom
                int x = column * stride + Padding;
                int y = atlas.height - (row + 1) * stride + Padding;
                atlas.SetPixels32(x, y, Cell, Cell, glyph.GetPixels32());
                Object.DestroyImmediate(glyph);
                names.Add(Path.GetFileNameWithoutExtension(files[i]));
                rects.Add(new RectInt(x, y, Cell, Cell));
            }
            atlas.Apply();
            BuilderUtil.EnsureFolder(Folder);
            File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);

            // ---- sprite asset
            TMP_SpriteAsset asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);
            if (asset == null) {
                asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(asset, SpriteAssetPath);
            }
            // version's setter is internal. Without it TMP "upgrades" the asset on load and rebuilds
            // the tables from the (empty) legacy sprite list.
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("m_Version").stringValue = "1.1.0";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(asset.name);
            asset.spriteSheet = sheet;

            Material material = asset.material;
            if (material == null) {
                material = new Material(Shader.Find("TextMeshPro/Sprite"));
                material.name = asset.name + " Material";
                AssetDatabase.AddObjectToAsset(material, asset);
                asset.material = material;
            }
            material.SetTexture(ShaderUtilities.ID_MainTex, sheet);

            float bearingY = Cell * CentreAboveBaseline / GlyphScale + Cell * 0.5f;
            asset.spriteGlyphTable.Clear();
            asset.spriteCharacterTable.Clear();
            for (int i = 0; i < names.Count; i++) {
                RectInt r = rects[i];
                TMP_SpriteGlyph glyph = new TMP_SpriteGlyph((uint)i,
                    new GlyphMetrics(Cell, Cell, 0f, bearingY, Cell + Padding * 2),
                    new GlyphRect(r.x, r.y, r.width, r.height), GlyphScale, 0);
                asset.spriteGlyphTable.Add(glyph);
                TMP_SpriteCharacter character = new TMP_SpriteCharacter(0xFFFE, glyph);
                character.name = names[i];
                character.scale = 1f;
                asset.spriteCharacterTable.Add(character);
            }
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[InputPromptImport] {names.Count} glyphs → {SpriteAssetPath}");
            return asset;
        }

        // Sets the sprite asset on the HUD prefab's prompt text without rebuilding the prefab
        // (UIPrefabBuilder does the same on a full build)
        static void AssignToHud(TMP_SpriteAsset asset) {
            if (asset == null) {
                return;
            }
            GameObject root = PrefabUtility.LoadPrefabContents(UIPrefabBuilder.HudPath);
            try {
                bool found = false;
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) {
                    if (text.name == UIPrefabBuilder.PromptTextName) {
                        text.spriteAsset = asset;
                        found = true;
                    }
                }
                if (!found) {
                    Debug.LogError("[InputPromptImport] no " + UIPrefabBuilder.PromptTextName + " in " + UIPrefabBuilder.HudPath);
                    return;
                }
                PrefabUtility.SaveAsPrefabAsset(root, UIPrefabBuilder.HudPath);
            }finally {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
