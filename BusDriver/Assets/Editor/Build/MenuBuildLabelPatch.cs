using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BusDriver.UI.Menu;

namespace BusDriver.Editor.Build {
    // Patches the legacy, hand-authored Menu scene: the bottom-right build label (T-M0-07) and the
    // MenuContext scene root (T-M1-04). Idempotent. Goes away with Menu.unity when MenuBuilder
    // generates the menu (T-M1-16).
    // Batch mode:  -executeMethod BusDriver.Editor.Build.MenuBuildLabelPatch.Apply -quit
    public static class MenuBuildLabelPatch {
        const string MenuScenePath = "Assets/Scenes/Menu.unity";
        const string LabelName = "Build Label";
        const string ContextName = "Menu Context";

        [MenuItem("Tools/Bus Driver/Patch Legacy Menu Scene")]
        public static void Apply() {
            Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            Canvas canvas = null;
            foreach (GameObject root in scene.GetRootGameObjects()) {
                if (root.name == "Canvas") {
                    canvas = root.GetComponent<Canvas>();
                }
            }
            if (canvas == null) {
                Debug.LogError("[BUILD] Menu.unity has no root Canvas");
                return;
            }
            BuildLabelView labelView = AddBuildLabel(canvas);
            AddMenuContext(scene, labelView);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BUILD] build label and MenuContext patched into " + MenuScenePath);
        }

        static BuildLabelView AddBuildLabel(Canvas canvas) {
            Transform existing = canvas.transform.Find(LabelName);
            GameObject label = existing != null ? existing.gameObject : new GameObject(LabelName, typeof(RectTransform));
            label.transform.SetParent(canvas.transform, false);
            label.transform.SetAsLastSibling();
            label.layer = canvas.gameObject.layer;

            RectTransform rect = (RectTransform)label.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 16f);
            rect.sizeDelta = new Vector2(600f, 36f);

            TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
            if (text == null) {
                text = label.AddComponent<TextMeshProUGUI>();
            }
            text.text = "0.0.0 (dev)";
            text.fontSize = 20f;
            text.alignment = TextAlignmentOptions.BottomRight;
            text.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            text.raycastTarget = false;
            BuildLabelView view = label.GetComponent<BuildLabelView>();
            if (view == null) {
                view = label.AddComponent<BuildLabelView>();
            }
            return view;
        }

        static void AddMenuContext(Scene scene, BuildLabelView labelView) {
            GameObject host = null;
            foreach (GameObject root in scene.GetRootGameObjects()) {
                if (root.name == ContextName) {
                    host = root;
                }
            }
            if (host == null) {
                host = new GameObject(ContextName);
                SceneManager.MoveGameObjectToScene(host, scene);
            }
            MenuContext context = host.GetComponent<MenuContext>();
            if (context == null) {
                context = host.AddComponent<MenuContext>();
            }
            // MainMenu and the options screen are found by MenuContext itself (IGameBindable)
            SerializedObject so = new SerializedObject(context);
            so.FindProperty("buildLabel").objectReferenceValue = labelView;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
