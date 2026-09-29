using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

// In-game pause overlay: Resume / Options / Main Menu, laid out like the title menu.
public class PauseMenu : MonoBehaviour {
    const string OptionsPrefabPath = "Assets/Prefabs/Level Essentials/In Canvas/Options Menu.prefab";
    const string ControlsPrefabPath = "Assets/Prefabs/Level Essentials/In Canvas/Controls Menu.prefab";

    [SerializeField] GameObject pauseRoot;
    [SerializeField] GameObject optionsRoot;
    [SerializeField] GameObject controlsRoot;
    [SerializeField] Button resumeButton;
    [SerializeField] Button optionsButton;
    [SerializeField] Button mainMenuButton;

    public bool IsOpen { get { return pauseRoot != null && pauseRoot.activeSelf; } }
    public bool OptionsOpen {
        get {
            return (optionsRoot != null && optionsRoot.activeSelf)
                || (controlsRoot != null && controlsRoot.activeSelf);
        }
    }

    // Builds or upgrades the pause UI when the scene still has the old text-only panel.
    public static PauseMenu EnsureInScene() {
        PauseMenu existing = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        if (existing != null && existing.resumeButton != null && existing.pauseRoot != null) {
            existing.EnsureVisuals();
            existing.EnsureControlsPrefab();
            return existing;
        }

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) {
            return existing;
        }

        PauseMenu menu = existing != null ? existing : canvas.gameObject.AddComponent<PauseMenu>();
        Transform hud = canvas.transform.Find("HUD");
        Transform pausePanel = hud != null ? hud.Find("PausePanel") : null;
        if (pausePanel == null) {
            GameObject panelGo = new GameObject("PausePanel", typeof(RectTransform));
            panelGo.layer = 5;
            panelGo.transform.SetParent(hud != null ? hud : canvas.transform, false);
            Stretch(panelGo.GetComponent<RectTransform>());
            pausePanel = panelGo.transform;
        }

        // Strip the old "PAUSED / ESC" label and any leftover children except Dim
        for (int i = pausePanel.childCount - 1; i >= 0; i--) {
            Transform child = pausePanel.GetChild(i);
            if (child.name == "Dim") {
                continue;
            }
            SafeDestroyUI(child.gameObject);
        }

        // Build buttons while the panel is inactive so Selectables never register, then get destroyed mid-list
        bool pauseWasActive = pausePanel.gameObject.activeSelf;
        pausePanel.gameObject.SetActive(false);

        EnsureDim(pausePanel);

        CreateLabel(pausePanel, "PauseTitle", "PAUSED", 120f, new Vector2(0f, 304f), new Vector2(1220f, 200f));
        menu.pauseRoot = pausePanel.gameObject;
        menu.resumeButton = CreateMenuButton(pausePanel, "Resume Button", "RESUME", new Vector2(0f, 98f), 74f);
        menu.optionsButton = CreateMenuButton(pausePanel, "Options Button", "OPTIONS", new Vector2(0f, -40f), 74f);
        menu.mainMenuButton = CreateMenuButton(pausePanel, "Main Menu Button", "MAIN MENU", new Vector2(0f, -189f), 72f);

        if (menu.optionsRoot == null) {
            menu.optionsRoot = InstantiateUnder(canvas.transform, OptionsPrefabPath, "Options Menu");
        }
        menu.EnsureControlsPrefab();

        if (canvas.GetComponent<GraphicRaycaster>() == null) {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null) {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        menu.WireButtons();
        menu.pauseRoot.SetActive(false);
        if (pauseWasActive) {
            // leave closed; Show() will activate
        }
        if (menu.optionsRoot != null) {
            menu.optionsRoot.SetActive(false);
        }
        if (menu.controlsRoot != null) {
            menu.controlsRoot.SetActive(false);
        }
        return menu;
    }

    void EnsureVisuals() {
        if (pauseRoot != null) {
            EnsureDim(pauseRoot.transform);
        }
        ApplyMenuButtonVisuals(resumeButton);
        ApplyMenuButtonVisuals(optionsButton);
        ApplyMenuButtonVisuals(mainMenuButton);
        FitButtonToLabel(resumeButton);
        FitButtonToLabel(optionsButton);
        FitButtonToLabel(mainMenuButton);
    }

    void EnsureControlsPrefab() {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) {
            canvas = FindAnyObjectByType<Canvas>();
        }
        if (canvas == null) {
            return;
        }

        ControlsMenu controls = controlsRoot != null
            ? controlsRoot.GetComponentInChildren<ControlsMenu>(true)
            : null;
        if (controls != null && controls.HasBakedBinds()) {
            return;
        }

        if (controlsRoot != null) {
            SafeDestroyUI(controlsRoot);
            controlsRoot = null;
        }
        controlsRoot = InstantiateUnder(canvas.transform, ControlsPrefabPath, "Controls Menu");
        if (controlsRoot != null) {
            controlsRoot.SetActive(false);
        }
    }

    static void EnsureDim(Transform pausePanel) {
        Transform dimTransform = pausePanel.Find("Dim");
        Image dim;
        if (dimTransform == null) {
            GameObject dimObject = new GameObject("Dim", typeof(RectTransform));
            dimObject.layer = 5;
            dimObject.transform.SetParent(pausePanel, false);
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
        // Same full-screen tint as Controls Menu root Image
        dim.sprite = null;
        dim.type = Image.Type.Sliced;
        dim.color = new Color(0f, 0f, 0f, 0.392f);
        dim.raycastTarget = true;
    }

    void Awake() {
        WireButtons();
        EnsureVisuals();
        if (pauseRoot != null) {
            pauseRoot.SetActive(false);
        }
        if (optionsRoot != null) {
            optionsRoot.SetActive(false);
        }
        if (controlsRoot != null) {
            controlsRoot.SetActive(false);
        }
    }

    void WireButtons() {
        if (resumeButton != null) {
            ReplaceClick(resumeButton, Resume);
        }
        if (optionsButton != null) {
            ReplaceClick(optionsButton, OpenOptions);
        }
        if (mainMenuButton != null) {
            ReplaceClick(mainMenuButton, BackToMainMenu);
        }
        WireSubmenuButtons();
    }

    public void Show() {
        EnsureVisuals();
        if (optionsRoot != null) {
            optionsRoot.SetActive(false);
        }
        if (controlsRoot != null) {
            controlsRoot.SetActive(false);
        }
        if (pauseRoot != null) {
            pauseRoot.SetActive(true);
        }
        PlayUISound();
    }

    public void Hide() {
        if (pauseRoot != null) {
            pauseRoot.SetActive(false);
        }
        if (optionsRoot != null) {
            optionsRoot.SetActive(false);
        }
        if (controlsRoot != null) {
            controlsRoot.SetActive(false);
        }
        RefreshHudHints();
    }

    public void Resume() {
        if (SceneController.Instance != null) {
            SceneController.Instance.SetPaused(false);
        }else {
            Hide();
            ingameMenus.pausedGame = false;
            Time.timeScale = 1f;
        }
        PlayUISound();
    }

    public void OpenOptions() {
        if (pauseRoot != null) {
            pauseRoot.SetActive(false);
        }
        if (controlsRoot != null) {
            controlsRoot.SetActive(false);
        }
        if (optionsRoot != null) {
            optionsRoot.SetActive(true);
            OptionsMenu options = optionsRoot.GetComponentInChildren<OptionsMenu>(true);
            if (options != null) {
                options.InitializeSettings();
            }
        }
        PlayUISound();
    }

    public void OpenControls() {
        if (optionsRoot != null) {
            optionsRoot.SetActive(false);
        }
        if (controlsRoot != null) {
            controlsRoot.SetActive(true);
            ControlsMenu controls = controlsRoot.GetComponentInChildren<ControlsMenu>(true);
            if (controls != null) {
                controls.FixingText();
            }
        }
        PlayUISound();
    }

    public void CloseControls() {
        if (controlsRoot != null) {
            controlsRoot.SetActive(false);
        }
        if (optionsRoot != null) {
            optionsRoot.SetActive(true);
        }
        PlayUISound();
    }

    public void CloseOptions() {
        if (optionsRoot != null) {
            optionsRoot.SetActive(false);
        }
        if (controlsRoot != null) {
            controlsRoot.SetActive(false);
        }
        if (pauseRoot != null) {
            pauseRoot.SetActive(true);
        }
        RefreshHudHints();
        PlayUISound();
    }

    // Escape while a submenu is open: Controls -> Options -> Pause
    public void HandleEscapeFromSubmenu() {
        if (controlsRoot != null && controlsRoot.activeSelf) {
            CloseControls();
            return;
        }
        CloseOptions();
    }

    public void BackToMainMenu() {
        PlayUISound();
        Time.timeScale = 1f;
        ingameMenus.pausedGame = false;
        SceneManager.LoadScene("Menu");
    }

    public void OnOptionsBack() {
        CloseOptions();
    }

    void WireSubmenuButtons() {
        if (optionsRoot != null) {
            Button back = FindDirectChildButton(optionsRoot.transform, "Back Button");
            if (back != null) {
                ReplaceClick(back, CloseOptions);
            }
            Button controls = FindDirectChildButton(optionsRoot.transform, "Controls Button");
            if (controls != null) {
                ReplaceClick(controls, OpenControls);
            }
        }
        if (controlsRoot != null) {
            Button back = FindDirectChildButton(controlsRoot.transform, "Back Button");
            if (back != null) {
                ReplaceClick(back, CloseControls);
            }
        }
    }

    static Button FindDirectChildButton(Transform root, string childName) {
        Transform child = root.Find(childName);
        if (child != null) {
            return child.GetComponent<Button>();
        }
        foreach (Transform t in root) {
            if (t.name.IndexOf(childName, System.StringComparison.OrdinalIgnoreCase) >= 0) {
                Button button = t.GetComponent<Button>();
                if (button != null) {
                    return button;
                }
            }
        }
        return null;
    }

    static void ReplaceClick(Button button, UnityEngine.Events.UnityAction action) {
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
    }

    void RefreshHudHints() {
        DrivingHUD hud = FindAnyObjectByType<DrivingHUD>();
        if (hud != null) {
            hud.RefreshControlsHint();
        }
    }

    void PlayUISound() {
        if (SceneController.Instance != null && SceneController.Instance.soundController != null) {
            SceneController.Instance.soundController.Play("UI Click");
        }
    }

    static GameObject InstantiateUnder(Transform parent, string assetPath, string name) {
        GameObject prefab = LoadMenuPrefab(assetPath);
        if (prefab == null) {
            Debug.LogWarning("PauseMenu: could not load " + assetPath + ". Run Tools/Bus Driver/Build MVP Scene.");
            return null;
        }
        // Instantiate under an inactive holder so Selectables never join the global list
        // until the menu is actually shown (avoids Selectable.OnDisable IndexOutOfRange).
        GameObject holder = new GameObject("PauseMenu_InstantiateHolder");
        holder.SetActive(false);
        GameObject instance = Instantiate(prefab, holder.transform);
        instance.name = name;
        instance.SetActive(false);
        instance.transform.SetParent(parent, false);
        if (Application.isPlaying) {
            Object.Destroy(holder);
        }else {
            Object.DestroyImmediate(holder);
        }
        return instance;
    }

    // Disable Selectables before destroy so Unity's static s_Selectables list stays consistent
    static void SafeDestroyUI(GameObject go) {
        if (go == null) {
            return;
        }
        Selectable[] selectables = go.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < selectables.Length; i++) {
            if (selectables[i] != null) {
                selectables[i].enabled = false;
            }
        }
        go.SetActive(false);
        if (Application.isPlaying) {
            Object.Destroy(go);
        }else {
            Object.DestroyImmediate(go);
        }
    }

    static GameObject LoadMenuPrefab(string assetPath) {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#else
        return null;
#endif
    }

    static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void CreateLabel(Transform parent, string name, string text, float fontSize, Vector2 anchoredPos, Vector2 size) {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        TMP_Text label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    static Button CreateMenuButton(Transform parent, string name, string label, Vector2 anchoredPos, float height) {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
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

    // Button hit/highlight width matches the rendered label width
    static void FitButtonToLabel(Button button) {
        if (button == null) {
            return;
        }
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) {
            return;
        }
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.ForceMeshUpdate();
        float width = Mathf.Ceil(label.GetPreferredValues(label.text).x);
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

    // Exact ColorTint values from Controls Menu Back Button
    static void ApplyMenuButtonVisuals(Button button) {
        if (button == null) {
            return;
        }
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

    static Sprite whiteSprite;

    static Sprite WhiteSprite() {
        if (whiteSprite != null) {
            return whiteSprite;
        }
#if UNITY_EDITOR
        whiteSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (whiteSprite != null) {
            return whiteSprite;
        }
#endif
        Texture2D tex = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return whiteSprite;
    }
}
