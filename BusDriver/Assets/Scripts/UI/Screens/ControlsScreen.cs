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
using BusDriver.Gameplay.Player;
using BusDriver.UI.Menu;

namespace BusDriver.UI.Screens {
    // The Controls screen (was ControlsMenu), a ScreenView in Screens.prefab (T-M1-16): one row per
    // keyboard/mouse binding of Driving, OnFoot and Global except Look, each with a rebind button
    // and a reset button, plus reset-all (§4.10). Rows are built at runtime from InputService,
    // cloned from the prefab's template row, so a new action shows up without a rebuild.
    public class ControlsScreen : ScreenView, IGameBindable {
        public const string LabelTemplateName = "Row Label Template";
        public const string ButtonTemplateName = "Row Button Template";
        public const string ResetButtonName = "Reset Button";
        public const string BackButtonName = "Back Button";

        [SerializeField] ScreenRouter router;
        [SerializeField] float firstRowY = 360f;
        // Room for a 44 px row, which is what the 32pt row text needs
        [SerializeField] float rowPitch = 54f;
        [SerializeField] float columnOffset = 430f;

        sealed class Row {
            public RebindableBinding Binding;
            public TMP_Text KeyText;
            public Button KeyButton;
            public Button ResetButton;
        }

        readonly List<Row> rows = new List<Row>();
        InputService input;
        SettingsService settings;
        IAudioService audio;
        TMP_Text statusText;
        bool built;

        public void Bind(GameServices game) {
            input = game.Input;
            settings = game.Settings;
            audio = game.Audio;
            input.OnBindingsChanged += Refresh;
        }

        protected override void Awake() {
            base.Awake();
            Button reset = ChildButton(ResetButtonName);
            Button back = ChildButton(BackButtonName);
            if (reset != null) {
                reset.onClick.AddListener(ResetKeybinds);
            }
            if (back != null) {
                back.onClick.AddListener(Back);
            }
        }

        public override void OnOpened() {
            Build();
            SetStatus("");
            Refresh();
            SelectFirst();
        }

        // Rebinds are saved as they happen; this catches a reset-all
        public override void OnClosed() {
            if (input != null) {
                input.CancelRebind();
            }
            if (settings != null) {
                settings.Save();
            }
        }

        public void Back() {
            PlayUISound();
            if (router.Top == this) {
                router.Pop();
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

        public void Refresh() {
            for (int i = 0; i < rows.Count; i++) {
                Row row = rows[i];
                row.KeyText.text = input.GetBindingDisplayString(row.Binding.Action, row.Binding.BindingIndex).ToUpperInvariant();
            }
        }

        public void ResetKeybinds() {
            PlayUISound();
            if (input != null) {
                input.ResetAll();
            }
            SetStatus(UIText.AllControlsReset);
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
            List<RebindableBinding> bindings = input.RebindableBindings();
            // The screen has room for exactly two columns, so they split whatever the asset has:
            // the list grows as actions are added and the layout keeps up on its own
            int perColumn = Mathf.Max(1, (bindings.Count + 1) / 2);
            for (int i = 0; i < bindings.Count; i++) {
                int column = i / perColumn;
                float x = column == 0 ? -columnOffset : columnOffset;
                float y = firstRowY - (i % perColumn) * rowPitch;
                rows.Add(CreateRow(bindings[i], labelTemplate, buttonTemplate, x, y));
            }
            float belowRows = firstRowY - perColumn * rowPitch;
            statusText = CloneLabel(labelTemplate, "Rebind Status", new Vector2(0f, belowRows - 20f), new Vector2(1200f, 44f));
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.text = "";
            Place(transform.Find(ResetButtonName), new Vector2(0f, belowRows - 110f));
            Place(transform.Find(BackButtonName), new Vector2(0f, belowRows - 215f));

            labelTemplate.gameObject.SetActive(false);
            buttonTemplate.gameObject.SetActive(false);
        }

        Row CreateRow(RebindableBinding binding, Transform labelTemplate, Transform buttonTemplate, float x, float y) {
            TMP_Text label = CloneLabel(labelTemplate, binding.ActionId + " label", new Vector2(x - 230f, y), new Vector2(380f, 44f));
            label.text = binding.Label.ToUpperInvariant();
            label.alignment = TextAlignmentOptions.Right;

            Row row = new Row { Binding = binding };
            row.KeyButton = CloneButton(buttonTemplate, binding.ActionId + " key", new Vector2(x + 90f, y), new Vector2(240f, 44f));
            row.KeyText = row.KeyButton.GetComponentInChildren<TMP_Text>(true);
            row.KeyButton.onClick.AddListener(() => StartRebind(row));

            // Wide enough for RESET at the row's text size, and clear of the key button
            row.ResetButton = CloneButton(buttonTemplate, binding.ActionId + " reset", new Vector2(x + 325f, y), new Vector2(180f, 44f));
            row.ResetButton.GetComponentInChildren<TMP_Text>(true).text = UIText.Reset;
            row.ResetButton.onClick.AddListener(() => ResetRow(row));
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
            // Drop the persistent listeners the template was baked with
            button.onClick = new Button.ButtonClickedEvent();
            return button;
        }

        static void Place(Transform target, Vector2 position, Vector2? size = null) {
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

        void ResetRow(Row row) {
            PlayUISound();
            if (input != null) {
                input.ResetBinding(row.Binding.ActionId);
            }
            SetStatus("");
        }

        // Controller-ready rule 4 (§4.10): focus is always on a Selectable
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

        // Tests: the rows as built
        internal int RowCount { get { return rows.Count; } }
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
