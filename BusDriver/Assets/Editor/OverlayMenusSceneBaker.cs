using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Bakes Pause + Game Over overlay UI into BusRoute (or the open scene) so play mode
// never creates those panels at runtime.
public static class OverlayMenusSceneBaker {
    const string ScenePath = "Assets/Scenes/BusRoute.unity";
    const string OptionsPrefabPath = "Assets/Prefabs/Level Essentials/In Canvas/Options Menu.prefab";
    const string ControlsPrefabPath = "Assets/Prefabs/Level Essentials/In Canvas/Controls Menu.prefab";
    const int UILayer = 5;
    const float PromptHeight = 310f;

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
            Transform dialoguePanel = canvas != null ? canvas.transform.Find("Dialogue Controller/Panel") : null;
            Transform ratingLabel = hud != null ? hud.Find("DriverPanel/RatingText") : null;
            Transform quotaLabel = hud != null ? hud.Find("DriverPanel/QuotaText") : null;
            Transform quotaPanel = hud != null ? hud.Find("QuotaFulfilledPanel") : null;
            Transform controlsLabel = hud != null ? hud.Find("DriverPanel/ControlsText") : null;
            Transform statusLabel = hud != null ? hud.Find("StatusText") : null;
            QuotaFulfilledMenu existingQuota = Object.FindAnyObjectByType<QuotaFulfilledMenu>(FindObjectsInactive.Include);
            RectTransform promptRect = hud != null ? hud.Find("PromptText") as RectTransform : null;
            bool promptRaised = promptRect != null
                && Mathf.Approximately(promptRect.anchoredPosition.y, PromptHeight);
            if (gameOverPanel != null && existingMenu != null && dialoguePanel != null && ratingLabel != null
                && quotaLabel != null && quotaPanel != null && existingQuota != null
                && controlsLabel == null && statusLabel == null && promptRaised) {
                SceneController controller = Object.FindAnyObjectByType<SceneController>();
                if (controller != null) {
                    SerializedObject so = new SerializedObject(controller);
                    SerializedProperty gameOverProp = so.FindProperty("gameOverMenu");
                    SerializedProperty quotaProp = so.FindProperty("quotaFulfilledMenu");
                    bool dirty = false;
                    if (gameOverProp != null && gameOverProp.objectReferenceValue == null) {
                        gameOverProp.objectReferenceValue = existingMenu;
                        dirty = true;
                    }
                    if (quotaProp != null && quotaProp.objectReferenceValue == null) {
                        quotaProp.objectReferenceValue = existingQuota;
                        dirty = true;
                    }
                    if (dirty) {
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

        PauseMenu pauseMenu = EnsurePauseMenu(canvas, hud);
        GameOverMenu gameOverMenu = EnsureGameOverMenu(canvas, hud);
        QuotaFulfilledMenu quotaMenu = EnsureQuotaFulfilledMenu(canvas, hud);
        EnsureOptionsAndControls(canvas, pauseMenu);
        EnsureHudExtras(hud);
        DialogueController dialogue = EnsureDialogue(canvas);
        EnsureEventSystem();
        if (canvas.GetComponent<GraphicRaycaster>() == null) {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        SceneController controller = Object.FindAnyObjectByType<SceneController>();
        if (controller != null) {
            if (controller.GetComponent<RideRatings>() == null) {
                controller.gameObject.AddComponent<RideRatings>();
            }
            SerializedObject so = new SerializedObject(controller);
            SerializedProperty pauseProp = so.FindProperty("pauseMenu");
            SerializedProperty gameOverProp = so.FindProperty("gameOverMenu");
            SerializedProperty quotaProp = so.FindProperty("quotaFulfilledMenu");
            SerializedProperty dialogueProp = so.FindProperty("dialogueController");
            if (pauseProp != null) {
                pauseProp.objectReferenceValue = pauseMenu;
            }
            if (gameOverProp != null) {
                gameOverProp.objectReferenceValue = gameOverMenu;
            }
            if (quotaProp != null) {
                quotaProp.objectReferenceValue = quotaMenu;
            }
            if (dialogueProp != null && dialogue != null) {
                dialogueProp.objectReferenceValue = dialogue;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("Overlay menus baked into open scene.");
    }

    static PauseMenu EnsurePauseMenu(Canvas canvas, Transform hud) {
        PauseMenu menu = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        if (menu == null) {
            menu = canvas.gameObject.AddComponent<PauseMenu>();
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
        Button resume = CreateMenuButton(panel, "Resume Button", "RESUME", new Vector2(0f, 98f), 74f);
        Button options = CreateMenuButton(panel, "Options Button", "OPTIONS", new Vector2(0f, -40f), 74f);
        Button mainMenu = CreateMenuButton(panel, "Main Menu Button", "MAIN MENU", new Vector2(0f, -189f), 72f);

        SerializedObject so = new SerializedObject(menu);
        so.FindProperty("pauseRoot").objectReferenceValue = panel.gameObject;
        so.FindProperty("resumeButton").objectReferenceValue = resume;
        so.FindProperty("optionsButton").objectReferenceValue = options;
        so.FindProperty("mainMenuButton").objectReferenceValue = mainMenu;
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

    static QuotaFulfilledMenu EnsureQuotaFulfilledMenu(Canvas canvas, Transform hud) {
        QuotaFulfilledMenu menu = Object.FindAnyObjectByType<QuotaFulfilledMenu>(FindObjectsInactive.Include);
        if (menu == null) {
            menu = canvas.gameObject.AddComponent<QuotaFulfilledMenu>();
        }

        Transform panel = hud.Find("QuotaFulfilledPanel");
        if (panel == null) {
            GameObject panelGo = new GameObject("QuotaFulfilledPanel", typeof(RectTransform));
            panelGo.layer = UILayer;
            panelGo.transform.SetParent(hud, false);
            Stretch(panelGo.GetComponent<RectTransform>());
            panel = panelGo.transform;
        }

        ClearChildrenExceptDim(panel);
        panel.gameObject.SetActive(false);
        EnsureDim(panel);
        CreateLabel(panel, "QuotaTitle", "QUOTA FULFILLED", 100f, new Vector2(0f, 220f), new Vector2(1400f, 200f));
        Button nextLevel = CreateMenuButton(panel, "Next Level Button", "NEXT LEVEL", new Vector2(0f, 40f), 74f);
        Button mainMenu = CreateMenuButton(panel, "Main Menu Button", "MAIN MENU", new Vector2(0f, -110f), 72f);

        SerializedObject so = new SerializedObject(menu);
        so.FindProperty("quotaRoot").objectReferenceValue = panel.gameObject;
        so.FindProperty("nextLevelButton").objectReferenceValue = nextLevel;
        so.FindProperty("mainMenuButton").objectReferenceValue = mainMenu;
        so.ApplyModifiedPropertiesWithoutUndo();
        return menu;
    }

    // Top-right stack: rating + star, quota progress, speed + gear, then stop request
    static void EnsureHudExtras(Transform hud) {
        DrivingHUD driving = Object.FindAnyObjectByType<DrivingHUD>(FindObjectsInactive.Include);
        if (driving == null) {
            return;
        }
        Transform driverPanel = hud.Find("DriverPanel");
        if (driverPanel == null) {
            return;
        }

        DestroyNamed(driverPanel, "ControlsText");
        DestroyNamed(driverPanel, "GearText");
        DestroyNamed(hud, "StatusText");
        RaisePrompt(hud);

        Color hudColor = new Color(0.85f, 0.9f, 0.85f, 0.9f);
        TMP_Text rating = EnsureCornerLabel(driverPanel, "RatingText", "5.0", 48f,
            new Vector2(1f, 1f), new Vector2(-108f, -40f), new Vector2(520f, 70f),
            TextAlignmentOptions.TopRight, hudColor);
        Image star = EnsureRatingStar(driverPanel);
        TMP_Text quota = EnsureCornerLabel(driverPanel, "QuotaText", "0/3", 36f,
            new Vector2(1f, 1f), new Vector2(-60f, -108f), new Vector2(600f, 50f),
            TextAlignmentOptions.TopRight, new Color(0.85f, 0.9f, 0.85f, 0.75f));
        TMP_Text speed = EnsureCornerLabel(driverPanel, "SpeedText", "0 km/h  N", 40f,
            new Vector2(1f, 1f), new Vector2(-60f, -162f), new Vector2(600f, 55f),
            TextAlignmentOptions.TopRight, hudColor);
        TMP_Text request = EnsureCornerLabel(driverPanel, "StopRequestText", "", 32f,
            new Vector2(1f, 1f), new Vector2(-60f, -222f), new Vector2(600f, 50f),
            TextAlignmentOptions.TopRight, new Color(1f, 0.75f, 0.2f, 0.95f));

        SerializedObject so = new SerializedObject(driving);
        SetHudRef(so, "ratingText", rating);
        SerializedProperty starProp = so.FindProperty("ratingStar");
        if (starProp != null) {
            starProp.objectReferenceValue = star;
        }
        SetHudRef(so, "quotaText", quota);
        SetHudRef(so, "speedText", speed);
        SetHudRef(so, "requestText", request);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Sits above the dialogue panel (which reaches y 260) so the two never overlap
    static void RaisePrompt(Transform hud) {
        Transform prompt = hud.Find("PromptText");
        if (prompt == null) {
            return;
        }
        RectTransform rect = prompt as RectTransform;
        if (rect != null) {
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, PromptHeight);
        }
    }

    static Image EnsureRatingStar(Transform driverPanel) {
        Transform existing = driverPanel.Find("RatingStar");
        Image star = existing != null ? existing.GetComponent<Image>() : null;
        if (star == null) {
            GameObject go = new GameObject("RatingStar", typeof(RectTransform));
            go.layer = UILayer;
            go.transform.SetParent(driverPanel, false);
            star = go.AddComponent<Image>();
        }
        RectTransform rect = star.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-60f, -48f);
        rect.sizeDelta = new Vector2(40f, 40f);
        star.sprite = DrivingHUD.CreateStarSprite();
        star.color = new Color(1f, 0.82f, 0.2f, 1f);
        star.raycastTarget = false;
        star.preserveAspect = true;
        return star;
    }

    static void SetHudRef(SerializedObject so, string prop, TMP_Text value) {
        SerializedProperty property = so.FindProperty(prop);
        if (property != null) {
            property.objectReferenceValue = value;
        }
    }

    static void DestroyNamed(Transform parent, string name) {
        Transform child = parent.Find(name);
        if (child != null) {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    // The controller object stays active so it can run the typewriter coroutine; only
    // the panel under it is toggled, which is what panelRoot is for.
    static DialogueController EnsureDialogue(Canvas canvas) {
        Transform root = canvas.transform.Find("Dialogue Controller");
        if (root == null) {
            GameObject rootGo = new GameObject("Dialogue Controller", typeof(RectTransform));
            rootGo.layer = UILayer;
            rootGo.transform.SetParent(canvas.transform, false);
            Stretch(rootGo.GetComponent<RectTransform>());
            root = rootGo.transform;
        }

        DialogueController controller = root.GetComponent<DialogueController>();
        if (controller == null) {
            controller = root.gameObject.AddComponent<DialogueController>();
        }

        Transform panel = root.Find("Panel");
        if (panel == null) {
            GameObject panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.layer = UILayer;
            panelGo.transform.SetParent(root, false);
            RectTransform rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 40f);
            rect.sizeDelta = new Vector2(1500f, 220f);
            Image background = panelGo.AddComponent<Image>();
            background.sprite = null;
            background.color = new Color(0f, 0f, 0f, 0.72f);
            background.raycastTarget = false;
            panel = panelGo.transform;
        }

        TMP_Text name = EnsureCornerLabel(panel, "NameText", "", 44f,
            new Vector2(0f, 1f), new Vector2(40f, -18f), new Vector2(900f, 60f),
            TextAlignmentOptions.TopLeft, new Color(1f, 0.82f, 0.35f, 1f));
        TMP_Text line = EnsureCornerLabel(panel, "DialogueText", "", 38f,
            new Vector2(0f, 1f), new Vector2(40f, -86f), new Vector2(1420f, 120f),
            TextAlignmentOptions.TopLeft, Color.white);
        line.textWrappingMode = TextWrappingModes.Normal;

        controller.nameText = name as TextMeshProUGUI;
        controller.dialogueText = line as TextMeshProUGUI;
        controller.panelRoot = panel.gameObject;
        controller.textSound = "Dialogue";
        panel.gameObject.SetActive(false);
        return controller;
    }

    static TMP_Text EnsureCornerLabel(Transform parent, string name, string text, float fontSize,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size, TextAlignmentOptions alignment, Color color) {
        Transform existing = parent.Find(name);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;
        if (label == null) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            go.transform.SetParent(parent, false);
            label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
        }
        TMP_FontAsset font = ResolveTmpFont();
        if (font != null && label.font == null) {
            label.font = font;
        }
        RectTransform rect = label.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    static void EnsureOptionsAndControls(Canvas canvas, PauseMenu pauseMenu) {
        SerializedObject so = new SerializedObject(pauseMenu);
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
        if (Object.FindAnyObjectByType<EventSystem>() != null) {
            return;
        }
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
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
