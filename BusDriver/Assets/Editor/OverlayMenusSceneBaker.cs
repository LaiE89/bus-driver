using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using BusDriver.Gameplay.Player;
using BusDriver.UI.Screens;

namespace BusDriver.Editor.Builders {
    // Bakes Pause + Game Over overlay UI into BusRoute (or the open scene) so play mode
    // never creates those panels at runtime.
    public static class OverlayMenusSceneBaker {
        const string ScenePath = "Assets/Scenes/BusRoute.unity";
        const string OptionsPrefabPath = "Assets/Prefabs/Level Essentials/In Canvas/Options Menu.prefab";
        const string ControlsPrefabPath = "Assets/Prefabs/Level Essentials/In Canvas/Controls Menu.prefab";
        const int UILayer = 5;

        [MenuItem("Tools/Bus Driver/Bake Overlay Menus Into Scene")]
        public static void BakeIntoBusRoute() {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null) {
                Debug.LogError("OverlayMenusSceneBaker: missing scene at " + ScenePath);
                return;
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BakeOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Overlay menus baked into " + ScenePath);
        }

        // When BusRoute is open and Game Over is still missing, bake once after scripts reload.
        [InitializeOnLoadMethod]
        static void AutoBakeIfNeeded() {
            EditorApplication.delayCall += () => {
                if (EditorApplication.isPlayingOrWillChangePlaymode) {
                    return;
                }
                try {
                    if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) {
                        return;
                    }
                }catch {
                }
                var scene = EditorSceneManager.GetActiveScene();
                if (scene.path != ScenePath || !scene.isLoaded) {
                    return;
                }
                Canvas canvas = Object.FindAnyObjectByType<Canvas>();
                Transform hud = canvas != null ? canvas.transform.Find("HUD") : null;
                Transform gameOverPanel = hud != null ? hud.Find("GameOverPanel") : null;
                GameOverMenu existingMenu = Object.FindAnyObjectByType<GameOverMenu>(FindObjectsInactive.Include);
                if (gameOverPanel != null && existingMenu != null) {
                    SceneController controller = Object.FindAnyObjectByType<SceneController>();
                    if (controller != null) {
                        SerializedObject so = new SerializedObject(controller);
                        SerializedProperty gameOverProp = so.FindProperty("gameOverMenu");
                        if (gameOverProp != null && gameOverProp.objectReferenceValue == null) {
                            gameOverProp.objectReferenceValue = existingMenu;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            EditorSceneManager.MarkSceneDirty(scene);
                            EditorSceneManager.SaveScene(scene);
                        }
                    }
                    return;
                }
                BakeOpenScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("OverlayMenusSceneBaker: auto-baked pause/game over into BusRoute.");
            };
        }

        [MenuItem("Tools/Bus Driver/Bake Overlay Menus Into Open Scene")]
        public static void BakeOpenScene() {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) {
                Debug.LogError("OverlayMenusSceneBaker: no Canvas in the open scene.");
                return;
            }

            Transform hud = canvas.transform.Find("HUD");
            if (hud == null) {
                GameObject hudGo = new GameObject("HUD", typeof(RectTransform));
                hudGo.layer = UILayer;
                hudGo.transform.SetParent(canvas.transform, false);
                Stretch(hudGo.GetComponent<RectTransform>());
                hud = hudGo.transform;
            }

            PauseScreen pauseScreen = EnsurePauseScreen(canvas, hud);
            GameOverMenu gameOverMenu = EnsureGameOverMenu(canvas, hud);
            EnsureOptionsAndControls(canvas, pauseScreen);
            EnsureEventSystem();
            if (canvas.GetComponent<GraphicRaycaster>() == null) {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            SceneController controller = Object.FindAnyObjectByType<SceneController>();
            if (controller != null) {
                SerializedObject so = new SerializedObject(controller);
                SerializedProperty gameOverProp = so.FindProperty("gameOverMenu");
                if (gameOverProp != null) {
                    gameOverProp.objectReferenceValue = gameOverMenu;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("Overlay menus baked into open scene.");
        }

        static PauseScreen EnsurePauseScreen(Canvas canvas, Transform hud) {
            PauseScreen menu = Object.FindAnyObjectByType<PauseScreen>(FindObjectsInactive.Include);
            if (menu == null) {
                menu = canvas.gameObject.AddComponent<PauseScreen>();
            }

            Transform panel = hud.Find("PausePanel");
            if (panel == null) {
                GameObject panelGo = new GameObject("PausePanel", typeof(RectTransform));
                panelGo.layer = UILayer;
                panelGo.transform.SetParent(hud, false);
                Stretch(panelGo.GetComponent<RectTransform>());
                panel = panelGo.transform;
            }

            ClearChildrenExceptDim(panel);
            panel.gameObject.SetActive(false);
            EnsureDim(panel);
            CreateLabel(panel, "PauseTitle", "PAUSED", 120f, new Vector2(0f, 304f), new Vector2(1220f, 200f));
            Button resume = CreateMenuButton(panel, "Resume Button", "RESUME", new Vector2(0f, 120f), 74f);
            Button options = CreateMenuButton(panel, "Options Button", "OPTIONS", new Vector2(0f, 0f), 74f);
            Button quitToMenu = CreateMenuButton(panel, "Quit To Menu Button", "QUIT TO MENU", new Vector2(0f, -120f), 72f);
            Button quitGame = CreateMenuButton(panel, "Quit Game Button", "QUIT GAME", new Vector2(0f, -240f), 72f);

            SerializedObject so = new SerializedObject(menu);
            so.FindProperty("pauseRoot").objectReferenceValue = panel.gameObject;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("optionsButton").objectReferenceValue = options;
            so.FindProperty("quitToMenuButton").objectReferenceValue = quitToMenu;
            so.FindProperty("quitGameButton").objectReferenceValue = quitGame;
            so.ApplyModifiedPropertiesWithoutUndo();
            return menu;
        }

        static GameOverMenu EnsureGameOverMenu(Canvas canvas, Transform hud) {
            GameOverMenu menu = Object.FindAnyObjectByType<GameOverMenu>(FindObjectsInactive.Include);
            if (menu == null) {
                menu = canvas.gameObject.AddComponent<GameOverMenu>();
            }

            Transform panel = hud.Find("GameOverPanel");
            if (panel == null) {
                GameObject panelGo = new GameObject("GameOverPanel", typeof(RectTransform));
                panelGo.layer = UILayer;
                panelGo.transform.SetParent(hud, false);
                Stretch(panelGo.GetComponent<RectTransform>());
                panel = panelGo.transform;
            }

            ClearChildrenExceptDim(panel);
            panel.gameObject.SetActive(false);
            EnsureDim(panel);
            CreateLabel(panel, "GameOverTitle", "GAME OVER", 120f, new Vector2(0f, 220f), new Vector2(1220f, 200f));
            Button retry = CreateMenuButton(panel, "Retry Button", "RETRY", new Vector2(0f, 40f), 74f);
            Button mainMenu = CreateMenuButton(panel, "Main Menu Button", "MAIN MENU", new Vector2(0f, -110f), 72f);

            SerializedObject so = new SerializedObject(menu);
            so.FindProperty("gameOverRoot").objectReferenceValue = panel.gameObject;
            so.FindProperty("retryButton").objectReferenceValue = retry;
            so.FindProperty("mainMenuButton").objectReferenceValue = mainMenu;
            so.ApplyModifiedPropertiesWithoutUndo();
            return menu;
        }

        static void EnsureOptionsAndControls(Canvas canvas, PauseScreen pauseScreen) {
            SerializedObject so = new SerializedObject(pauseScreen);
            SerializedProperty optionsProp = so.FindProperty("optionsRoot");
            SerializedProperty controlsProp = so.FindProperty("controlsRoot");

            GameObject optionsRoot = optionsProp.objectReferenceValue as GameObject;
            if (optionsRoot == null) {
                Transform existing = canvas.transform.Find("Options Menu");
                optionsRoot = existing != null
                    ? existing.gameObject
                    : InstantiateMenuPrefab(OptionsPrefabPath, canvas.transform, "Options Menu");
            }
            if (optionsRoot != null) {
                optionsRoot.SetActive(false);
                optionsProp.objectReferenceValue = optionsRoot;
            }

            GameObject controlsRoot = controlsProp.objectReferenceValue as GameObject;
            if (controlsRoot == null) {
                Transform existing = canvas.transform.Find("Controls Menu");
                controlsRoot = existing != null
                    ? existing.gameObject
                    : InstantiateMenuPrefab(ControlsPrefabPath, canvas.transform, "Controls Menu");
            }
            if (controlsRoot != null) {
                controlsRoot.SetActive(false);
                controlsProp.objectReferenceValue = controlsRoot;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject InstantiateMenuPrefab(string assetPath, Transform parent, string name) {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null) {
                Debug.LogWarning("OverlayMenusSceneBaker: missing prefab at " + assetPath);
                return null;
            }
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance != null) {
                instance.name = name;
                instance.SetActive(false);
            }
            return instance;
        }

        static void EnsureEventSystem() {
            EventSystem existing = Object.FindAnyObjectByType<EventSystem>();
            UIInputModuleSetup.Configure(existing != null ? existing.gameObject : new GameObject("EventSystem"));
        }

        static void ClearChildrenExceptDim(Transform panel) {
            for (int i = panel.childCount - 1; i >= 0; i--) {
                Transform child = panel.GetChild(i);
                if (child.name == "Dim") {
                    continue;
                }
                Object.DestroyImmediate(child.gameObject);
            }
        }

        static void EnsureDim(Transform panel) {
            Transform dimTransform = panel.Find("Dim");
            Image dim;
            if (dimTransform == null) {
                GameObject dimObject = new GameObject("Dim", typeof(RectTransform));
                dimObject.layer = UILayer;
                dimObject.transform.SetParent(panel, false);
                dimObject.transform.SetAsFirstSibling();
                Stretch(dimObject.GetComponent<RectTransform>());
                dim = dimObject.AddComponent<Image>();
            }else {
                dimTransform.SetAsFirstSibling();
                dim = dimTransform.GetComponent<Image>();
                if (dim == null) {
                    dim = dimTransform.gameObject.AddComponent<Image>();
                }
            }
            dim.sprite = null;
            dim.type = Image.Type.Sliced;
            dim.color = new Color(0f, 0f, 0f, 0.392f);
            dim.raycastTarget = true;
        }

        static void Stretch(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void CreateLabel(Transform parent, string name, string text, float fontSize, Vector2 anchoredPos, Vector2 size) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            TMP_Text label = go.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = ResolveTmpFont();
            if (font != null) {
                label.font = font;
            }
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        static TMP_FontAsset ResolveTmpFont() {
            if (TMP_Settings.defaultFontAsset != null) {
                return TMP_Settings.defaultFontAsset;
            }
            TMP_FontAsset fromResources = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (fromResources != null) {
                return fromResources;
            }
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset LiberationSans");
            if (guids != null && guids.Length > 0) {
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            return null;
        }

        static Button CreateMenuButton(Transform parent, string name, string label, Vector2 anchoredPos, float height) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(100f, height);

            Image image = go.AddComponent<Image>();
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            ApplyMenuButtonVisuals(button);
            CreateLabel(go.transform, name + " Text", label, 64f, Vector2.zero, new Vector2(100f, height));
            FitButtonToLabel(button);
            return button;
        }

        static void FitButtonToLabel(Button button) {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) {
                return;
            }
            label.textWrappingMode = TextWrappingModes.NoWrap;
            float width;
            if (label.font != null) {
                label.ForceMeshUpdate();
                width = Mathf.Ceil(label.GetPreferredValues(label.text).x);
            }else {
                width = Mathf.Ceil(label.fontSize * Mathf.Max(1, label.text.Length) * 0.6f);
            }
            if (width < 1f) {
                return;
            }
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            RectTransform labelRect = label.rectTransform;
            buttonRect.sizeDelta = new Vector2(width, buttonRect.sizeDelta.y);
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(width, buttonRect.sizeDelta.y);
        }

        static void ApplyMenuButtonVisuals(Button button) {
            Image image = button.targetGraphic as Image;
            if (image == null) {
                image = button.GetComponent<Image>();
                if (image == null) {
                    image = button.gameObject.AddComponent<Image>();
                }
                button.targetGraphic = image;
            }
            image.sprite = WhiteSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.990566f, 0.990566f, 0.990566f, 1f);
            image.raycastTarget = true;

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 0.23529412f);
            colors.pressedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.39215687f);
            colors.selectedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
            colors.disabledColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
        }

        static Sprite WhiteSprite() {
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (sprite != null) {
                return sprite;
            }
            Texture2D tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
