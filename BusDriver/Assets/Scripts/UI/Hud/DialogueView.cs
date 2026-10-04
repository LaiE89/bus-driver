using BusDriver.Gameplay.Dialogue;
using BusDriver.Gameplay.Flow;
using TMPro;
using UnityEngine;

namespace BusDriver.UI.Hud {
    // The dialogue box (§4.13, was the MVP's DialogueController half that touched the canvas).
    // Draws whatever DialogueService currently has and hides itself the rest of the time, so a
    // night with nobody talking costs nothing.
    public sealed class DialogueView : MonoBehaviour, IShiftBindable {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text speakerText;
        [SerializeField] TMP_Text lineText;

        DialogueService dialogue;
        bool shown;

        // Tests
        internal string ShownText { get { return lineText != null ? lineText.text : ""; } }
        internal bool IsShown { get { return panel != null && panel.activeSelf; } }

        public void Bind(ShiftServices shift) {
            dialogue = shift.Dialogue;
            if (dialogue != null) {
                dialogue.OnChanged += Refresh;
            }
            Refresh();
        }

        void OnDestroy() {
            if (dialogue != null) {
                dialogue.OnChanged -= Refresh;
            }
        }

        // OnChanged fires per character, so the panel is only toggled when it actually changes
        void Refresh() {
            bool play = dialogue != null && dialogue.IsPlaying;
            if (play != shown) {
                shown = play;
                if (panel != null) {
                    panel.SetActive(play);
                }
            }
            if (!play) {
                return;
            }
            if (speakerText != null) {
                speakerText.text = dialogue.Speaker;
            }
            if (lineText != null) {
                lineText.text = dialogue.VisibleText;
            }
        }
    }
}
