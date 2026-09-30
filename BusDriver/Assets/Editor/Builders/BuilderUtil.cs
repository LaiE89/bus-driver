using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BusDriver.Editor.Builders {
    // Shared helpers for every builder (§4.15), ported from the legacy BusDriverSceneBuilder.
    // Builders overwrite their output in place so GUIDs never change, and write only under
    // Generated/, ProjectSettings/, Resources/GameRootConfig and missing Data/ assets.
    public static class BuilderUtil {
        public const string GeneratedRoot = "Assets/Generated";

        // ------------------------------------------------------------------ assets

        // Creates every missing folder along the path
        public static void EnsureFolder(string path) {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // SaveAsPrefabAsset onto an existing path replaces the content and keeps the .meta,
        // so everything referencing the prefab survives a rebuild
        public static GameObject SaveOrOverwritePrefab(GameObject root, string path) {
            EnsureFolder(Path.GetDirectoryName(path));
            bool success;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out success);
            if (!success) {
                Debug.LogError("BuilderUtil: failed to save prefab " + path);
            }
            return prefab;
        }

        public static void SaveScene(Scene scene, string path) {
            EnsureFolder(Path.GetDirectoryName(path));
            if (!EditorSceneManager.SaveScene(scene, path)) {
                Debug.LogError("BuilderUtil: failed to save scene " + path);
            }
        }

        // Get-or-create, then (re)apply the values, so a material keeps its GUID across builds
        public static Material GetOrCreateMaterial(string path, Color baseColor, Color? emission = null, float smoothness = 0.15f) {
            EnsureFolder(Path.GetDirectoryName(path));
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null && GraphicsSettings.currentRenderPipeline != null) {
                    shader = GraphicsSettings.currentRenderPipeline.defaultShader;
                }
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue) {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                // Anything else and the URP material inspector switches emission back off
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }else {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Writes a generated mesh to its asset path, overwriting the existing asset's data in place so
        // its GUID (and every MeshFilter/MeshCollider pointing at it) survives the rebuild. Returns
        // the asset to reference.
        public static Mesh SaveMesh(Mesh mesh, string path) {
            EnsureFolder(Path.GetDirectoryName(path));
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            string name = existing.name;
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        public static T GetOrCreateAsset<T>(string path) where T : ScriptableObject {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) {
                EnsureFolder(Path.GetDirectoryName(path));
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        // ----------------------------------------------------------------- objects

        public static GameObject Group(string name, Transform parent) {
            GameObject go = new GameObject(name);
            if (parent != null) {
                go.transform.SetParent(parent, false);
            }
            return go;
        }

        public static Transform Node(Transform parent, string name, Vector3 localPos) {
            Transform node = Group(name, parent).transform;
            node.localPosition = localPos;
            return node;
        }

        public static Transform Node(Transform parent, string name, Vector3 localPos, Vector3 localEuler) {
            Transform node = Node(parent, name, localPos);
            node.localRotation = Quaternion.Euler(localEuler);
            return node;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material material, bool keepCollider = false) {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            // Always explicit, the primitive default material is not a URP one
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!keepCollider) {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material material, bool keepCollider = false) {
            return Prim(PrimitiveType.Cube, name, parent, localPos, scale, material, keepCollider);
        }

        public static void SetLayerRecursively(GameObject go, int layer) {
            go.layer = layer;
            foreach (Transform child in go.transform) {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        // ------------------------------------------------------ serialized fields

        // Fails loudly when a serialized field gets renamed (verify.sh greps for this message)
        static SerializedProperty FindProp(SerializedObject so, string prop) {
            SerializedProperty property = so.FindProperty(prop);
            if (property == null) {
                Debug.LogError($"BuilderUtil: no serialized field '{prop}' on {so.targetObject.GetType().Name}");
            }
            return property;
        }

        public static void SetRef(Object target, string prop, Object value) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetRefArray(Object target, string prop, Object[] values) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(Object target, string prop, int value) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string prop, float value) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetBool(Object target, string prop, bool value) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetString(Object target, string prop, string value) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetVector(Object target, string prop, Vector3 value) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetStringArray(Object target, string prop, string[] values) {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = FindProp(so, prop);
            if (property == null) {
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
