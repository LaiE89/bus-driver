using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Input;
using BusDriver.Gameplay.Player;
using BusDriver.UI.Menu;

namespace BusDriver.UI.Screens {
    // The Controls screen (was ControlsMenu): one row per keyboard/mouse binding of Driving, OnFoot
    // and Global except Look, each with a rebind button and a reset button, plus reset-all (§4.10).
    // Rows are built at runtime from InputService, styled on the prefab's first legacy row, so a new
    // action shows up without touching the prefab. Screens.prefab replaces the prefab in T-M1-16.
    public class ControlsScreen : MonoBehaviour, IGameBindable {
        // The row the old prefab builder baked first; it's the style template for every row
        const string LabelTemplateName = "Switch Camera";
        const string ButtonTemplateName = "Switch Camera Button";
        // The other baked rows, hidden in favour of the generated list
        static readonly string[] LegacyRowNames = {
            "HANDBRAKE", "Handbrake Button", "DOORS", "Doors Button", "LEAVE SEAT", "Leave Seat Button", "INTERACT", "Interact Button",
        };

        [SerializeField] int rowsPerColumn = 10;
        [SerializeField] float firstRowY = 380f;
        [SerializeField] float rowPitch = 42f;
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
        SoundController soundController;
        TMP_Text statusText;
        bool built;

        public void Bind(GameServices game) {
            input = game.Input;
            settings = game.Settings;
            input.OnBindingsChanged += Refresh;
        }

        void Awake() {
            if (SceneController.Instance != null) {
                soundController = SceneController.Instance.soundController;
            }else {
                soundController = MainMenu.soundController;
            }
        }

        void Start() {
            Build();
            Refresh();
            SelectFirst();
        }

        void OnEnable() {
            if (built) {
                SetStatus("");
                Refresh();
                SelectFirst();
            }
        }

        void OnDisable() {
            if (input != null) {
                input.CancelRebind();
            }
        }

        void OnDestroy() {
            if (input != null) {
                input.OnBindingsChanged -= Refresh;
            }
        }

        // Called when the pause screen opens the controls
        public void FixingText() {
            Refresh();
        }

        public void Refresh() {
            for (int i = 0; i < rows.Count; i++) {
                Row row = rows[i];
                row.KeyText.text = input.GetBindingDisplayString(row.Binding.Action, row.Binding.BindingIndex).ToUpperInvariant();
            }
        }

        // The prefab's Reset button calls this by name
        public void ResetKeybinds() {
            PlayUISound();
            if (input != null) {
                input.ResetAll();
            }
            SetStatus(UIText.AllControlsReset);
        }

        // The prefab's Back button calls this by name. Rebinds are already saved as they happen.
        public void ApplyingKeybinds() {
            PlayUISound();
            if (settings != null) {
                settings.Save();
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
            for (int i = 0; i < LegacyRowNames.Length; i++) {
                Transform legacy = transform.Find(LegacyRowNames[i]);
                if (legacy != null) {
                    legacy.gameObject.SetActive(false);
                }
            }

            List<RebindableBinding> bindings = input.RebindableBindings();
            for (int i = 0; i < bindings.Count; i++) {
                int column = i / rowsPerColumn;
                float x = column == 0 ? -columnOffset : columnOffset;
                float y = firstRowY - (i % rowsPerColumn) * rowPitch;
                rows.Add(CreateRow(bindings[i], labelTemplate, buttonTemplate, x, y));
            }
            statusText = CloneLabel(labelTemplate, "Rebind Status", new Vector2(0f, firstRowY - rowsPerColumn * rowPitch - 20f), new Vector2(1200f, 30f));
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.text = "";
            Place(transform.Find("Reset Button"), new Vector2(0f, firstRowY - rowsPerColumn * rowPitch - 90f));
            Place(transform.Find("Back Button"), new Vector2(0f, firstRowY - rowsPerColumn * rowPitch - 180f));

            labelTemplate.gameObject.SetActive(false);
            buttonTemplate.gameObject.SetActive(false);
        }

        Row CreateRow(RebindableBinding binding, Transform labelTemplate, Transform buttonTemplate, float x, float y) {
            TMP_Text label = CloneLabel(labelTemplate, binding.ActionId + " label", new Vector2(x - 190f, y), new Vector2(320f, 30f));
            label.text = binding.Label.ToUpperInvariant();
            label.alignment = TextAlignmentOptions.Right;

            Row row = new Row { Binding = binding };
            row.KeyButton = CloneButton(buttonTemplate, binding.ActionId + " key", new Vector2(x + 60f, y), new Vector2(180f, 30f));
            row.KeyText = row.KeyButton.GetComponentInChildren<TMP_Text>(true);
            row.KeyButton.onClick.AddListener(() => StartRebind(row));

            row.ResetButton = CloneButton(buttonTemplate, binding.ActionId + " reset", new Vector2(x + 215f, y), new Vector2(110f, 30f));
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
            if (soundController != null) {
                soundController.Play("UI Click");
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
