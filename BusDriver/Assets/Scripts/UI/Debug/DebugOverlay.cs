using System.Collections.Generic;
using System.Text;
using BusDriver.Gameplay.Debug;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.UI.Debug {
    // The F1 overlay (§4.18): every registered section's readout, refreshed four times a second,
    // and a button per cheat. Development builds and the Editor only: in a release build it
    // removes itself before anything can see it. The mouse is free for the cheat buttons whenever
    // the cursor is (paused, on a screen).
    public sealed class DebugOverlay : MonoBehaviour, IShiftBindable {
        const float RefreshSeconds = 0.25f;

        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text body;
        [SerializeField] RectTransform cheatList;
        [SerializeField] Button cheatTemplate;

        readonly StringBuilder text = new StringBuilder(1024);
        readonly List<Button> cheatButtons = new List<Button>();
        InputService input;
        DebugRegistry registry;
        float nextRefresh;

        public bool IsOpen { get { return panel != null && panel.activeSelf; } }
        // Tests
        internal string Text { get { return body != null ? body.text : ""; } }
        internal int CheatButtonCount { get { return cheatButtons.Count; } }

        void Awake() {
            if (!DevBuild.IsEnabled()) {
                Destroy(gameObject);
                return;
            }
            if (cheatTemplate != null) {
                cheatTemplate.gameObject.SetActive(false);
            }
            SetOpen(false);
        }

        public void Bind(ShiftServices shift) {
            input = shift.Game.Input;
            registry = shift.Debug;
            registry.OnChanged += RebuildCheats;
            RebuildCheats();
        }

        void OnDestroy() {
            if (registry != null) {
                registry.OnChanged -= RebuildCheats;
            }
        }

        void Update() {
            if (input != null && input.Actions.DebugOverlay.WasPressedThisFrame()) {
                Toggle();
            }
            if (IsOpen && Time.unscaledTime >= nextRefresh) {
                Refresh();
            }
        }

        public void Toggle() {
            SetOpen(!IsOpen);
        }

        public void SetOpen(bool open) {
            if (panel == null) {
                return;
            }
            panel.SetActive(open);
            if (open) {
                Refresh();
            }
        }

        void Refresh() {
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            if (body == null || registry == null) {
                return;
            }
            text.Clear();
            IReadOnlyList<IDebugSection> sections = registry.Sections;
            for (int i = 0; i < sections.Count; i++) {
                text.Append("<b>").Append(sections[i].Title).Append("</b>\n");
                sections[i].Write(text);
                text.Append('\n');
            }
            body.SetText(text);
        }

        void RebuildCheats() {
            for (int i = 0; i < cheatButtons.Count; i++) {
                Destroy(cheatButtons[i].gameObject);
            }
            cheatButtons.Clear();
            if (registry == null || cheatTemplate == null || cheatList == null) {
                return;
            }
            IReadOnlyList<DebugCheat> cheats = registry.Cheats;
            for (int i = 0; i < cheats.Count; i++) {
                DebugCheat cheat = cheats[i];
                Button button = Instantiate(cheatTemplate, cheatList);
                button.gameObject.SetActive(true);
                button.name = "Cheat " + cheat.Label;
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) {
                    label.text = cheat.Group + ": " + cheat.Label;
                }
                button.onClick.AddListener(() => cheat.Run());
                cheatButtons.Add(button);
            }
        }
    }
}
