using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using BusDriver.UI.Menu;

namespace BusDriver.Editor.Build {
    // Adds the bottom-right build label to the legacy, hand-authored Menu scene (T-M0-07).
    // Idempotent. Goes away with Menu.unity when MenuBuilder generates the menu (T-M1-16).
    public static class MenuBuildLabelPatch {
        const string MenuScenePath = "Assets/Scenes/Menu.unity";
        const string LabelName = "Build Label";

        [MenuItem("Tools/Bus Driver/Add Build Label To Menu")]
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
            if (label.GetComponent<BuildLabelView>() == null) {
                label.AddComponent<BuildLabelView>();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BUILD] build label added to " + MenuScenePath);
        }
    }
}
