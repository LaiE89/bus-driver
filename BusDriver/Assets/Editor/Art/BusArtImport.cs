using System.IO;
using UnityEditor;

namespace BusDriver.Editor.Art {
    // The bus FBX is authored in centimetres with a skeleton for the doors and the steering.
    // Normal maps in that folder are marked so the lit materials read them as normals.
    public sealed class BusArtImport : AssetPostprocessor {

        void OnPreprocessModel() {
            if (!assetPath.EndsWith("SKM_Bus_00.fbx")) {
                return;
            }
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.optimizeGameObjects = false;
        }

        void OnPreprocessTexture() {
            if (!assetPath.Contains("/Art/Bus/")) {
                return;
            }
            TextureImporter importer = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("_N.png")) {
                importer.textureType = TextureImporterType.NormalMap;
            }
            if (assetPath.EndsWith("T_Bus_Window_00_BC.png") || assetPath.EndsWith("T_Bus_Window_00_O.png")
                || assetPath.EndsWith("T_Bus_Trim_00_BC.png") || assetPath.EndsWith("T_Bus_Trim_00_O.png")) {
                importer.isReadable = true;
            }
        }
    }

    // A refresh can import the art before this script exists. The next editor load corrects that.
    [InitializeOnLoad]
    static class BusArtEnsure {
        static BusArtEnsure() {
            EditorApplication.delayCall += Ensure;
        }

        static void Ensure() {
            if (!Directory.Exists("Assets/Art/Bus/Resources/Textures")) {
                return;
            }
            AssetDatabase.StartAssetEditing();
            try {
                string[] normals = Directory.GetFiles("Assets/Art/Bus/Resources/Textures", "*_N.png");
                for (int i = 0; i < normals.Length; i++) {
                    string path = normals[i].Replace('\\', '/');
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.NormalMap) {
                        importer.textureType = TextureImporterType.NormalMap;
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                    }
                }
                string[] punched = {
                    "Assets/Art/Bus/Resources/Textures/T_Bus_Window_00_BC.png",
                    "Assets/Art/Bus/Resources/Textures/T_Bus_Window_00_O.png",
                    "Assets/Art/Bus/Resources/Textures/T_Bus_Trim_00_BC.png",
                    "Assets/Art/Bus/Resources/Textures/T_Bus_Trim_00_O.png",
                };
                for (int i = 0; i < punched.Length; i++) {
                    TextureImporter importer = AssetImporter.GetAtPath(punched[i]) as TextureImporter;
                    if (importer != null && !importer.isReadable) {
                        importer.isReadable = true;
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                    }
                }
                ModelImporter model = AssetImporter.GetAtPath("Assets/Art/Bus/Resources/SKM_Bus_00.fbx") as ModelImporter;
                if (model != null && (model.animationType != ModelImporterAnimationType.Generic || model.optimizeGameObjects || model.importCameras)) {
                    model.globalScale = 1f;
                    model.useFileScale = true;
                    model.importCameras = false;
                    model.importLights = false;
                    model.animationType = ModelImporterAnimationType.Generic;
                    model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    model.optimizeGameObjects = false;
                    EditorUtility.SetDirty(model);
                    model.SaveAndReimport();
                }
            }finally {
                AssetDatabase.StopAssetEditing();
            }
        }
    }
}
