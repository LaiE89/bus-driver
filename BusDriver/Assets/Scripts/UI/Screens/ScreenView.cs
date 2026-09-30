using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.UI.Screens {
    // One modal panel on the ScreenRouter stack (§4.13). The router shows, hides and focuses it
    // through its CanvasGroup; subclasses react in the On* hooks. Every screen must be usable with
    // Navigate, Submit and Cancel alone (controller-ready rule 4, §4.10).
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenView : MonoBehaviour {
        [Tooltip("Focused when the screen opens; the first Selectable child if empty")]
        [SerializeField] Selectable firstSelected;
        [Tooltip("Hide the screens below while this one is on top (off for dialogs that overlay)")]
        [SerializeField] bool hidesScreensBelow = true;
        [Tooltip("Cancel (Esc / B) pops this screen")]
        [SerializeField] bool cancelPops = true;

        CanvasGroup group;

        public virtual bool HidesScreensBelow { get { return hidesScreensBelow; } }
        public virtual bool CancelPops { get { return cancelPops; } }
        public bool IsOpen { get; private set; }

        protected virtual void Awake() {
            group = GetComponent<CanvasGroup>();
        }

        public Selectable FirstSelectable {
            get {
                if (firstSelected != null && firstSelected.IsInteractable() && firstSelected.gameObject.activeInHierarchy) {
                    return firstSelected;
                }
                Selectable[] selectables = GetComponentsInChildren<Selectable>(false);
                for (int i = 0; i < selectables.Length; i++) {
                    if (selectables[i].IsInteractable()) {
                        return selectables[i];
                    }
                }
                return null;
            }
        }

        // Router only. visible: drawn at all; interactive: receives input (the top screen).
        internal void SetState(bool open, bool visible, bool interactive) {
            if (group == null) {
                group = GetComponent<CanvasGroup>();
            }
            IsOpen = open;
            gameObject.SetActive(visible);
            group.alpha = visible ? 1f : 0f;
            group.interactable = interactive;
            group.blocksRaycasts = interactive;
        }

        // Pushed onto the stack
        public virtual void OnOpened() { }
        // Popped or cleared off the stack
        public virtual void OnClosed() { }
        // Cancel while on top, when cancelPops is off
        public virtual void OnCancel() { }
    }
}
