using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Input;
using BusDriver.UI.Menu;
using BusDriver.UI.Theme;

namespace BusDriver.UI.Screens {
    // Controls screen (§4.10). Layout mirrors OptionsScreen: Title role header, Body row labels
    // left of centre with the control at x=240, and Apply / Reset All / Back on the same footer
    // band. Rebinds preview live; Apply commits to settings, Back discards.
    public class ControlsScreen : ScreenView, IGameBindable {
        public const string LabelTemplateName = "Row Label Template";
        public const string ButtonTemplateName = "Row Button Template";
        public const string ApplyButtonName = "Apply Button";
        public const string ResetButtonName = "Reset Button";
        public const string BackButtonName = "Back Button";
        public const string HintName = "Hint";
        public const string TitleName = "Title";

        [SerializeField] ScreenRouter router;

        // Same geometry as UIPrefabBuilder.BuildOptions / Row()
        const float TitleY = 400f;
        const float HintY = 310f;
        const float FirstRowY = 250f;
        // Options uses 80; nine bindings need a slightly tighter pitch to clear the footer
        const float RowPitch = 60f;
        const float FooterY = -380f;
        const float LabelX = -260f;
        const float ControlX = 240f;
        static readonly Vector2 LabelSize = new Vector2(460f, 44f);
        static readonly Vector2 ControlSize = new Vector2(420f, 44f);
        static readonly Vector2 FooterApplySize = new Vector2(260f, 70f);
        static readonly Vector2 FooterMidSize = new Vector2(300f, 70f);
        static readonly Vector2 FooterBackSize = new Vector2(260f, 70f);

        sealed class Row {
            public RebindableBinding Binding;
            public TMP_Text KeyText;
            public Button KeyButton;
        }

        readonly List<Row> rows = new List<Row>();
        InputService input;
        IAudioService audio;
        TMP_Text statusText;
        Button applyButton;
        Button resetButton;
        Button backButton;
        bool built;
        bool dirty;

        public void Bind(GameServices game) {
            input = game.Input;
            audio = game.Audio;
            input.OnBindingsChanged += Refresh;
        }

        protected override void Awake() {
            base.Awake();
            applyButton = ChildButton(ApplyButtonName);
            resetButton = ChildButton(ResetButtonName);
            backButton = ChildButton(BackButtonName);
            if (resetButton != null) {
                resetButton.onClick.AddListener(ResetKeybinds);
            }
            if (backButton != null) {
                backButton.onClick.AddListener(Back);
            }
            if (applyButton != null) {
                applyButton.onClick.AddListener(Apply);
            }
        }

        public override void OnOpened() {
            Build();
            if (input != null) {
                input.BeginBindingEdit();
            }
            dirty = false;
            SetStatus("");
            Refresh();
            SelectFirst();
        }

        public override void OnClosed() {
            if (input != null) {
                input.CancelRebind();
                input.DiscardBindingEdit();
            }
            dirty = false;
        }

        public void Apply() {
            PlayUISound();
            if (input == null) {
                return;
            }
            input.CommitBindingEdit();
            input.BeginBindingEdit();
            dirty = false;
            SetStatus(UIText.ControlsSaved);
        }

        public void Back() {
            PlayUISound();
            if (input != null) {
                input.DiscardBindingEdit();
            }
            dirty = false;
            if (router.Top == this) {
                router.Pop();
            }
        }

        public void ResetKeybinds() {
            PlayUISound();
            if (input != null) {
                input.ResetAll();
            }
            dirty = true;
            SetStatus(UIText.AllControlsReset);
        }

        public void Refresh() {
            if (input == null) {
                return;
            }
            for (int i = 0; i < rows.Count; i++) {
                Row row = rows[i];
                row.KeyText.text = input.GetBindingDisplayString(row.Binding.Action, row.Binding.BindingIndex).ToUpperInvariant();
            }
        }

        Button ChildButton(string childName) {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<Button>() : null;
        }

        void OnDestroy() {
            if (input != null) {
                input.OnBindingsChanged -= Refresh;
            }
        }

        void Build() {
            if (built || input == null) {
                return;
            }
            built = true;
            Transform labelTemplate = transform.Find(LabelTemplateName);
            Transform buttonTemplate = transform.Find(ButtonTemplateName);
            if (labelTemplate == null || buttonTemplate == null) {
                Log.Error(LogCat.Input, "ControlsScreen: the prefab lost its template row");
                return;
            }

            LayoutChrome(labelTemplate, buttonTemplate);

            List<RebindableBinding> bindings = input.RebindableBindings();
            for (int i = 0; i < bindings.Count; i++) {
                rows.Add(CreateRow(bindings[i], labelTemplate, buttonTemplate, FirstRowY - i * RowPitch));
            }

            float belowRows = FirstRowY - Mathf.Max(bindings.Count, 1) * RowPitch;
            statusText = CloneLabel(labelTemplate, "Rebind Status", new Vector2(0f, belowRows), new Vector2(900f, 44f));
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.text = "";
            // Body + highlight, same family as Options row labels
            ThemedText statusTheme = statusText.GetComponent<ThemedText>();
            if (statusTheme != null) {
                statusTheme.SetRole(ThemeRole.Body, ThemeColor.Highlight, 0f);
            }

            PlaceFooter();

            labelTemplate.gameObject.SetActive(false);
            buttonTemplate.gameObject.SetActive(false);
        }

        void LayoutChrome(Transform labelTemplate, Transform buttonTemplate) {
            // Match Options: Title role at y=400, 1220×160
            Transform title = transform.Find(TitleName);
            if (title != null) {
                Place(title, new Vector2(0f, TitleY), new Vector2(1220f, 160f));
                ThemedText titleTheme = title.GetComponent<ThemedText>();
                if (titleTheme != null) {
                    titleTheme.SetRole(ThemeRole.Title, ThemeColor.Text, 0f);
                }
            }

            if (transform.Find(HintName) == null) {
                TMP_Text hint = CloneLabel(labelTemplate, HintName, new Vector2(0f, HintY), new Vector2(900f, 44f));
                hint.text = UIText.ControlsHint;
                hint.alignment = TextAlignmentOptions.Center;
                ThemedText hintTheme = hint.GetComponent<ThemedText>();
                if (hintTheme != null) {
                    hintTheme.SetRole(ThemeRole.Body, ThemeColor.Disabled, 0f);
                }
            }

            EnsureApplyButton(buttonTemplate);
        }

        void PlaceFooter() {
            // Same band and sizes as Options (Apply | mid | Back)
            Place(applyButton != null ? applyButton.transform : null, new Vector2(-300f, FooterY), FooterApplySize);
            Place(resetButton != null ? resetButton.transform : null, new Vector2(0f, FooterY), FooterMidSize);
            Place(backButton != null ? backButton.transform : null, new Vector2(300f, FooterY), FooterBackSize);
            SetFooterLabel(applyButton, UIText.Apply);
            SetFooterLabel(resetButton, UIText.ResetAll);
            SetFooterLabel(backButton, UIText.Back);
        }

        void EnsureApplyButton(Transform buttonTemplate) {
            if (applyButton != null) {
                return;
            }
            // Clone Back so the footer keeps ThemeRole.Button like Options
            Transform source = backButton != null ? backButton.transform : buttonTemplate;
            applyButton = CloneButton(source, ApplyButtonName, new Vector2(-300f, FooterY), FooterApplySize);
            applyButton.onClick.AddListener(Apply);
        }

        static void SetFooterLabel(Button button, string label) {
            if (button == null) {
                return;
            }
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) {
                text.text = label;
            }
            // Leave ThemeRole.Button alone — Options footer uses the same
        }

        Row CreateRow(RebindableBinding binding, Transform labelTemplate, Transform buttonTemplate, float y) {
            // Same as UIPrefabBuilder.Row: Body label at -260, control at +240
            TMP_Text label = CloneLabel(labelTemplate, binding.ActionId + " label", new Vector2(LabelX, y), LabelSize);
            label.text = binding.Label.ToUpperInvariant();
            label.alignment = TextAlignmentOptions.Right;
            ThemedText labelTheme = label.GetComponent<ThemedText>();
            if (labelTheme != null) {
                labelTheme.SetRole(ThemeRole.Body, ThemeColor.Text, 0f);
            }

            Row row = new Row { Binding = binding };
            row.KeyButton = CloneButton(buttonTemplate, binding.ActionId + " key", new Vector2(ControlX, y), ControlSize);
            row.KeyText = row.KeyButton.GetComponentInChildren<TMP_Text>(true);
            if (row.KeyText != null) {
                ThemedText keyTheme = row.KeyText.GetComponent<ThemedText>();
                if (keyTheme != null) {
                    // Body on the key caption so rows read as Options labels, not menu buttons
                    keyTheme.SetRole(ThemeRole.Body, ThemeColor.Text, 0f);
                }
            }
            row.KeyButton.onClick.AddListener(() => StartRebind(row));
            return row;
        }

        TMP_Text CloneLabel(Transform template, string name, Vector2 position, Vector2 size) {
            GameObject clone = Instantiate(template.gameObject, template.parent);
            clone.name = name;
            clone.SetActive(true);
            Place(clone.transform, position, size);
            return clone.GetComponent<TMP_Text>();
        }

        Button CloneButton(Transform template, string name, Vector2 position, Vector2 size) {
            GameObject clone = Instantiate(template.gameObject, template.parent);
            clone.name = name;
            clone.SetActive(true);
            Place(clone.transform, position, size);
            Button button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            return button;
        }

        static void Place(Transform target, Vector2 position, Vector2? size = null) {
            if (target == null) {
                return;
            }
            RectTransform rect = target as RectTransform;
            if (rect == null) {
                return;
            }
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            if (size.HasValue) {
                rect.sizeDelta = size.Value;
            }
        }

        void StartRebind(Row row) {
            if (input == null) {
                return;
            }
            PlayUISound();
            row.KeyText.text = "...";
            SetStatus(string.Format(UIText.PressAKey, row.Binding.Label));
            input.StartRebind(row.Binding.ActionId, row.Binding.BindingIndex, result => {
                if (result.Success) {
                    dirty = true;
                    SetStatus("");
                }else if (result.Cancelled) {
                    SetStatus("");
                }else {
                    SetStatus(result.Error);
                }
                Refresh();
                if (row.KeyButton != null && EventSystem.current != null) {
                    EventSystem.current.SetSelectedGameObject(row.KeyButton.gameObject);
                }
            });
        }

        void SelectFirst() {
            if (rows.Count > 0 && EventSystem.current != null) {
                EventSystem.current.SetSelectedGameObject(rows[0].KeyButton.gameObject);
            }
        }

        void SetStatus(string message) {
            if (statusText != null) {
                statusText.text = message ?? "";
            }
        }

        void PlayUISound() {
            if (audio != null) {
                audio.Play(SoundIds.UiClick);
            }
        }

        internal int RowCount { get { return rows.Count; } }
        internal bool IsDirty { get { return dirty; } }

        internal string KeyTextFor(string actionId, int bindingIndex) {
            for (int i = 0; i < rows.Count; i++) {
                if (rows[i].Binding.ActionId == actionId && rows[i].Binding.BindingIndex == bindingIndex) {
                    return rows[i].KeyText.text;
                }
            }
            return null;
        }
    }
}
