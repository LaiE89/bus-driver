using BusDriver.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.Editor.Builders {
    // Helpers for generated uGUI (§4.13): 1920×1080 scaled overlay canvases, and every text a TMP
    // label with a ThemedText role (T-M1-12), so a theme edit restyles all of it.
    public static class UIBuild {
        public const int UILayer = 5;

        static readonly Color ButtonNormal = new Color(1f, 1f, 1f, 0f);
        static readonly Color ButtonHighlighted = new Color(0.96f, 0.96f, 0.96f, 0.24f);
        static readonly Color ButtonPressed = new Color(0.78f, 0.78f, 0.78f, 0.39f);
        static readonly Color ButtonSelected = new Color(0.96f, 0.96f, 0.96f, 0.3f);
        static readonly Color ButtonDisabled = new Color(0.78f, 0.78f, 0.78f, 0.5f);

        public static UITheme ThemeAsset {
            get {
                UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(UIThemeSeed.AssetPath);
                if (theme == null) {
                    throw new System.InvalidOperationException("no " + UIThemeSeed.AssetPath + "; DataSeeder runs before the UI builders");
                }
                return theme;
            }
        }

        public static Sprite Builtin(string name) {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/" + name + ".psd");
        }

        static DefaultControls.Resources Resources {
            get {
                return new DefaultControls.Resources {
                    standard = Builtin("UISprite"),
                    background = Builtin("Background"),
                    inputField = Builtin("InputFieldBackground"),
                    knob = Builtin("Knob"),
                    checkmark = Builtin("Checkmark"),
                    dropdown = Builtin("DropdownArrow"),
                    mask = Builtin("UIMask"),
                };
            }
        }

        static TMP_DefaultControls.Resources TmpResources {
            get {
                return new TMP_DefaultControls.Resources {
                    standard = Builtin("UISprite"),
                    background = Builtin("Background"),
                    inputField = Builtin("InputFieldBackground"),
                    knob = Builtin("Knob"),
                    checkmark = Builtin("Checkmark"),
                    dropdown = Builtin("DropdownArrow"),
                    mask = Builtin("UIMask"),
                };
            }
        }

        // --------------------------------------------------------------- layout

        public static GameObject UIObject(string name, Transform parent) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            if (parent != null) {
                go.transform.SetParent(parent, false);
            }
            return go;
        }

        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder) {
            GameObject go = UIObject(name, parent);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Panel(string name, Transform parent) {
            RectTransform rect = (RectTransform)UIObject(name, parent).transform;
            Stretch(rect);
            return rect;
        }

        public static void Stretch(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Anchor doubles as pivot, so position is an inset from that point
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size) {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static Image Fill(string name, Transform parent, Color color, bool blocksRaycasts) {
            Image image = Panel(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = blocksRaycasts;
            return image;
        }

        // ------------------------------------------------------------------ text

        public static TMP_Text Label(string name, Transform parent, string text, ThemeRole role, TextAlignmentOptions alignment,
            Vector2 anchor, Vector2 position, Vector2 size) {
            GameObject go = UIObject(name, parent);
            Place((RectTransform)go.transform, anchor, position, size);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Theme(label, role);
            return label;
        }

        // Styles an existing TMP text (the ones TMP's default controls create)
        public static ThemedText Theme(TMP_Text text, ThemeRole role) {
            ThemedText themed = text.GetComponent<ThemedText>();
            if (themed == null) {
                themed = text.gameObject.AddComponent<ThemedText>();
            }
            themed.Configure(ThemeAsset, role);
            return themed;
        }

        // --------------------------------------------------------------- controls

        public static Button CreateButton(string name, Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size) {
            GameObject go = UIObject(name, parent);
            Place((RectTransform)go.transform, anchor, position, size);
            Image image = go.AddComponent<Image>();
            image.sprite = Builtin("UISprite");
            image.type = Image.Type.Sliced;
            Button button = go.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonNormal;
            colors.highlightedColor = ButtonHighlighted;
            colors.pressedColor = ButtonPressed;
            colors.selectedColor = ButtonSelected;
            colors.disabledColor = ButtonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.targetGraphic = image;
            TMP_Text label = Label(name + " Text", go.transform, text, ThemeRole.Button, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Stretch(label.rectTransform);
            return button;
        }

        public static Slider CreateSlider(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, float min, float max) {
            GameObject go = DefaultControls.CreateSlider(Resources);
            go.name = name;
            go.transform.SetParent(parent, false);
            BuilderUtil.SetLayerRecursively(go, UILayer);
            Place((RectTransform)go.transform, anchor, position, size);
            Slider slider = go.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            return slider;
        }

        public static TMP_Dropdown CreateDropdown(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size) {
            GameObject go = TMP_DefaultControls.CreateDropdown(TmpResources);
            go.name = name;
            go.transform.SetParent(parent, false);
            BuilderUtil.SetLayerRecursively(go, UILayer);
            Place((RectTransform)go.transform, anchor, position, size);
            TMP_Dropdown dropdown = go.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            foreach (TMP_Text text in go.GetComponentsInChildren<TMP_Text>(true)) {
                Theme(text, ThemeRole.Body).SetPaletteColor(ThemeColor.Disabled);
            }
            // The list items sit on a light background, so the caption keeps TMP's dark text too
            SizeDropdownList(go.transform, size.y);
            return dropdown;
        }

        // TMP's defaults lay the list out for a 14pt font, so the 32pt Body role is clipped top and
        // bottom. Rows match the closed control, and the list shows four of them before scrolling.
        static void SizeDropdownList(Transform dropdown, float rowHeight) {
            SetHeight(dropdown.Find("Template/Viewport/Content/Item"), rowHeight);
            SetHeight(dropdown.Find("Template/Viewport/Content"), rowHeight);
            SetHeight(dropdown.Find("Template"), rowHeight * 4f);
        }

        // Only the height: every one of these rects stretches to its parent's width
        static void SetHeight(Transform target, float height) {
            RectTransform rect = target as RectTransform;
            if (rect == null) {
                return;
            }
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        public static Toggle CreateToggle(string name, Transform parent, Vector2 anchor, Vector2 position) {
            GameObject go = DefaultControls.CreateToggle(Resources);
            go.name = name;
            go.transform.SetParent(parent, false);
            BuilderUtil.SetLayerRecursively(go, UILayer);
            Place((RectTransform)go.transform, anchor, position, new Vector2(40f, 40f));
            // Its label is a legacy Text; the row label says what it is instead
            Transform label = go.transform.Find("Label");
            if (label != null) {
                Object.DestroyImmediate(label.gameObject);
            }
            Transform background = go.transform.Find("Background");
            if (background != null) {
                Place((RectTransform)background, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            }
            return go.GetComponent<Toggle>();
        }
    }
}
