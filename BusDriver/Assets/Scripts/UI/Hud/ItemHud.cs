using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Input;
using BusDriver.Gameplay.Items;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Shift;
using BusDriver.UI.Screens;
using BusDriver.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BusDriver.UI.Hud {
    // Depot shop at the start of each night, and the carried items in the top-left of the HUD.
    // Coffee is the only item: buying it spends the wallet, and its key drinks it for sanity.
    public sealed class ItemHud : MonoBehaviour {
        ShiftServices shift;
        ItemService items;
        ShiftDirector director;
        TMP_Text fontSource;
        TMP_Text slotsText;
        GameObject shop;
        TMP_Text walletText;
        TMP_Text priceText;
        TMP_Text statusText;
        TMP_Text carriedText;
        Button buyButton;
        Button startButton;
        Sprite buttonSprite;
        bool shopOpen;

        static readonly Color ScreenBackground = new Color(0f, 0f, 0f, 1f);
        static readonly Color RuleColor = new Color(0.92f, 0.92f, 0.9f, 0.18f);
        static readonly Color PanelColor = new Color(0.14f, 0.14f, 0.13f, 1f);

        public void Bind(ShiftServices services, TMP_Text font) {
            Unbind();
            shift = services;
            items = services.Items;
            director = services.Director;
            fontSource = font;
            if (items != null) {
                items.OnChanged += Refresh;
            }
            if (director != null) {
                director.OnStateChanged += HandleState;
            }
            if (shift.Game != null) {
                shift.Game.Input.OnBindingsChanged += Refresh;
            }
            EnsureSlotsText();
            HandleState(director != null ? director.State : ShiftState.None);
            Refresh();
        }

        void OnDestroy() {
            Unbind();
        }

        void Unbind() {
            if (items != null) {
                items.OnChanged -= Refresh;
            }
            if (director != null) {
                director.OnStateChanged -= HandleState;
            }
            if (shift != null && shift.Game != null) {
                shift.Game.Input.OnBindingsChanged -= RefreshSlots;
            }
        }

        void HandleState(ShiftState state) {
            if (state == ShiftState.Depot) {
                ShowShop();
            }else {
                HideShop();
            }
            RefreshSlots();
        }

        void Update() {
            if (items == null || shift == null || shift.Game == null) {
                return;
            }
            if (shopOpen) {
                return;
            }
            BusDriverActions actions = shift.Game.Input.Actions;
            if (actions.Item1.WasPerformedThisFrame()) {
                TryUse(0);
            }else if (actions.Item2.WasPerformedThisFrame()) {
                TryUse(1);
            }else if (actions.Item3.WasPerformedThisFrame()) {
                TryUse(2);
            }
        }

        void TryUse(int slot) {
            if (!items.Use(slot)) {
                return;
            }
            if (shift.Game.Audio != null) {
                shift.Game.Audio.Play(SoundIds.UiClick);
            }
            RefreshSlots();
        }

        void Refresh() {
            RefreshShop();
            RefreshSlots();
        }

        void EnsureSlotsText() {
            if (slotsText != null) {
                return;
            }
            slotsText = MakeText("ItemSlots", transform, "", ThemeRole.Hud, TextAlignmentOptions.TopLeft);
            RectTransform rect = slotsText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(40f, -36f);
            rect.sizeDelta = new Vector2(520f, 130f);
            slotsText.raycastTarget = false;
        }

        void RefreshSlots() {
            if (slotsText == null || items == null) {
                return;
            }
            bool driving = director != null && director.State == ShiftState.Driving
                && shift.Mode != null && shift.Mode.Mode != PlayerMode.OnFoot
                && (shift.Game == null || !shift.Game.Pause.IsPaused);
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            if (driving) {
                string[] slots = items.Slots;
                for (int i = 0; i < slots.Length; i++) {
                    string name = ItemService.DisplayName(slots[i]);
                    if (name == "") {
                        continue;
                    }
                    if (text.Length > 0) {
                        text.Append('\n');
                    }
                    text.Append(Key("Item" + (i + 1))).Append("   ").Append(name);
                }
            }
            slotsText.text = text.ToString();
            slotsText.gameObject.SetActive(text.Length > 0);
        }

        string Key(string actionId) {
            if (shift.Game == null) {
                return "?";
            }
            return shift.Game.Input.GetDisplayString(actionId).ToUpperInvariant();
        }

        void ShowShop() {
            EnsureShop();
            shop.SetActive(true);
            shopOpen = true;
            RefreshShop();
            FocusShop();
        }

        void HideShop() {
            shopOpen = false;
            if (shop != null) {
                shop.SetActive(false);
            }
        }

        void EnsureShop() {
            if (shop != null) {
                return;
            }
            shop = new GameObject("DepotShop", typeof(RectTransform));
            shop.transform.SetParent(transform, false);
            Stretch((RectTransform)shop.transform);
            Image dim = shop.AddComponent<Image>();
            dim.color = ScreenBackground;
            dim.raycastTarget = true;

            // Title, the night, then the same kind of sentence the other screens use as a hint
            TMP_Text title = MakeText("Title", shop.transform, UIText.DepotTitle, ThemeRole.Title, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0f, 430f), new Vector2(1400f, 120f));
            TMP_Text night = MakeText("Night", shop.transform, "", ThemeRole.Body, TextAlignmentOptions.Center);
            Place(night.rectTransform, new Vector2(0f, 330f), new Vector2(1400f, 44f));
            if (director != null) {
                night.text = string.Format(UIText.DepotNightLine, director.NightIndex, UIText.RouteName);
            }
            TMP_Text hint = MakeText("Hint", shop.transform, UIText.DepotHint, ThemeRole.Hint, TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0f, 268f), new Vector2(1200f, 40f));

            const float panelWidth = 640f;
            const float panelHeight = 400f;
            const float inner = 560f;
            RectTransform walletPanel = Panel("Wallet Panel", new Vector2(-370f, -20f), new Vector2(panelWidth, panelHeight));
            RectTransform shopPanel = Panel("Shop Panel", new Vector2(370f, -20f), new Vector2(panelWidth, panelHeight));

            TMP_Text walletLabel = MakeText("Wallet Label", walletPanel, UIText.DepotWalletLabel, ThemeRole.Caption, TextAlignmentOptions.Center);
            Place(walletLabel.rectTransform, new Vector2(0f, 140f), new Vector2(inner, 36f));
            Paint(walletLabel, ThemeRole.Caption, ThemeColor.Disabled, 22f);
            walletText = MakeText("Wallet", walletPanel, "", ThemeRole.Screen, TextAlignmentOptions.Center);
            Place(walletText.rectTransform, new Vector2(0f, 52f), new Vector2(inner, 90f));
            Paint(walletText, ThemeRole.Screen, ThemeColor.Highlight, 0f);

            Image carryRule = NewImage("Carry Rule", walletPanel, RuleColor, false);
            Place(carryRule.rectTransform, new Vector2(0f, -16f), new Vector2(inner, 1f));
            TMP_Text carrying = MakeText("Carrying Label", walletPanel, UIText.DepotCarrying, ThemeRole.Caption, TextAlignmentOptions.Center);
            Place(carrying.rectTransform, new Vector2(0f, -58f), new Vector2(inner, 32f));
            Paint(carrying, ThemeRole.Caption, ThemeColor.Disabled, 22f);
            carriedText = MakeText("Carried", walletPanel, "", ThemeRole.Body, TextAlignmentOptions.Center);
            Place(carriedText.rectTransform, new Vector2(0f, -120f), new Vector2(inner, 48f));

            TMP_Text forSale = MakeText("For Sale", shopPanel, UIText.DepotForSale, ThemeRole.Caption, TextAlignmentOptions.Center);
            Place(forSale.rectTransform, new Vector2(0f, 140f), new Vector2(inner, 36f));
            Paint(forSale, ThemeRole.Caption, ThemeColor.Disabled, 22f);

            RectTransform row = NewRect("Coffee Row", shopPanel);
            Place(row, new Vector2(0f, 64f), new Vector2(inner, 48f));
            TMP_Text item = MakeText("Coffee", row, UIText.DepotCoffee, ThemeRole.Body, TextAlignmentOptions.MidlineLeft);
            AnchorLeft(item.rectTransform, new Vector2(320f, 48f));
            priceText = MakeText("Price", row, "", ThemeRole.Body, TextAlignmentOptions.MidlineRight);
            AnchorRight(priceText.rectTransform, new Vector2(220f, 48f));
            Paint(priceText, ThemeRole.Body, ThemeColor.Highlight, 0f);

            RectTransform meta = NewRect("Coffee Meta", shopPanel);
            Place(meta, new Vector2(0f, 16f), new Vector2(inner, 32f));
            TMP_Text coffeeHint = MakeText("Coffee Hint", meta, UIText.DepotCoffeeHint, ThemeRole.Caption, TextAlignmentOptions.MidlineLeft);
            AnchorLeft(coffeeHint.rectTransform, new Vector2(280f, 32f));
            Paint(coffeeHint, ThemeRole.Caption, ThemeColor.Text, 22f);
            statusText = MakeText("Status", meta, "", ThemeRole.Caption, TextAlignmentOptions.MidlineRight);
            AnchorRight(statusText.rectTransform, new Vector2(280f, 32f));

            buyButton = MakeButton("Buy", shopPanel, UIText.DepotBuy, new Vector2(0f, -120f), new Vector2(420f, 72f), Buy);
            startButton = MakeButton("Start", shop.transform, UIText.DepotStart, new Vector2(0f, -340f), new Vector2(520f, 80f), StartShift);

            Navigation buyNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = startButton };
            Navigation startNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = buyButton };
            buyButton.navigation = buyNav;
            startButton.navigation = startNav;
        }

        void RefreshShop() {
            if (!shopOpen || items == null || walletText == null) {
                return;
            }
            walletText.text = Money.Format(items.WalletCents);
            if (priceText != null) {
                priceText.text = Money.Format(items.CoffeePrice);
            }
            bool slot = items.EmptySlot >= 0;
            bool afford = items.CanAffordCoffee;
            bool canBuy = slot && afford;
            buyButton.interactable = canBuy;
            if (statusText != null) {
                if (!slot) {
                    statusText.text = UIText.DepotSlotsFull;
                }else if (!afford) {
                    statusText.text = UIText.DepotCantAfford;
                }else {
                    statusText.text = "";
                }
                Paint(statusText, ThemeRole.Caption, ThemeColor.Disabled, 22f);
            }
            Paint(buyButton.GetComponentInChildren<TMP_Text>(), ThemeRole.Button, canBuy ? ThemeColor.Text : ThemeColor.Disabled, 0f);
            if (carriedText != null) {
                string carried = CarriedLine();
                carriedText.text = carried;
                bool empty = carried == UIText.DepotEmpty;
                Paint(carriedText, ThemeRole.Caption, empty ? ThemeColor.Disabled : ThemeColor.Text, 22f);
            }
            FocusShop();
        }

        string CarriedLine() {
            System.Text.StringBuilder line = new System.Text.StringBuilder();
            string[] slots = items.Slots;
            for (int i = 0; i < slots.Length; i++) {
                string name = ItemService.DisplayName(slots[i]);
                if (name == "") {
                    continue;
                }
                if (line.Length > 0) {
                    line.Append("      ");
                }
                line.Append(Key("Item" + (i + 1))).Append("  ").Append(name);
            }
            return line.Length > 0 ? line.ToString() : UIText.DepotEmpty;
        }

        void FocusShop() {
            if (!shopOpen || EventSystem.current == null || startButton == null) {
                return;
            }
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            bool buyOk = buyButton != null && buyButton.interactable;
            bool lost = selected == null || !selected.activeInHierarchy || (buyButton != null && selected == buyButton.gameObject && !buyOk);
            if (lost) {
                EventSystem.current.SetSelectedGameObject(buyOk ? buyButton.gameObject : startButton.gameObject);
            }
        }

        void Buy() {
            if (items == null || !items.TryBuyCoffee()) {
                RefreshShop();
                return;
            }
            if (shift.Game != null && shift.Game.Audio != null) {
                shift.Game.Audio.Play(SoundIds.UiMoneyDown);
            }
            Refresh();
        }

        void StartShift() {
            if (director == null) {
                return;
            }
            if (shift.Game != null && shift.Game.Audio != null) {
                shift.Game.Audio.Play(SoundIds.UiClick);
            }
            director.ConfirmDepot();
        }

        UITheme Theme {
            get {
                if (shift != null && shift.Game != null && shift.Game.Config != null) {
                    return shift.Game.Config.uiTheme as UITheme;
                }
                return null;
            }
        }

        TMP_Text MakeText(string name, Transform parent, string text, ThemeRole role, TextAlignmentOptions align) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = align;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UITheme theme = Theme;
            if (theme != null) {
                go.AddComponent<ThemedText>().Configure(theme, role);
            }else if (fontSource != null) {
                label.font = fontSource.font;
                label.fontSharedMaterial = fontSource.fontSharedMaterial;
                label.color = fontSource.color;
                label.fontSize = 28f;
            }
            return label;
        }

        // Role, and a palette colour when one is asked for. size 0 keeps the role's size.
        void Paint(TMP_Text label, ThemeRole role, ThemeColor color, float size) {
            ThemedText themed = label != null ? label.GetComponent<ThemedText>() : null;
            UITheme theme = Theme;
            if (themed == null || theme == null) {
                return;
            }
            themed.SetRole(role, color, size);
        }

        // Ghost button, same wash as the menu and the summary: quiet until it is selected
        Button MakeButton(string name, Transform parent, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, size);
            Image image = go.AddComponent<Image>();
            Sprite sprite = ButtonSprite();
            if (sprite != null) {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
            image.color = Color.white;
            Button button = go.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 0.24f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 0.39f);
            colors.selectedColor = new Color(0.96f, 0.96f, 0.96f, 0.3f);
            colors.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            TMP_Text label = MakeText("Label", go.transform, text, ThemeRole.Button, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return button;
        }

        // The sliced sprite the other screens already use, so the highlight matches them
        Sprite ButtonSprite() {
            if (buttonSprite != null) {
                return buttonSprite;
            }
            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
            for (int i = 0; i < buttons.Length; i++) {
                Image image = buttons[i].targetGraphic as Image;
                if (image != null && image.sprite != null) {
                    buttonSprite = image.sprite;
                    return buttonSprite;
                }
            }
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) {
                pixels[i] = Color.white;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            buttonSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            return buttonSprite;
        }

        RectTransform Panel(string name, Vector2 position, Vector2 size) {
            Image image = NewImage(name, shop.transform, PanelColor, false);
            Place(image.rectTransform, position, size);
            return image.rectTransform;
        }

        static Image NewImage(string name, Transform parent, Color color, bool raycast) {
            Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        static RectTransform NewRect(string name, Transform parent) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size) {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void AnchorLeft(RectTransform rect, Vector2 size) {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        static void AnchorRight(RectTransform rect, Vector2 size) {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
