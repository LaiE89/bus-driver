using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusDriver.UI.Screens {
    // Every destructive action asks first (§4.13): "Leaving now ends your run." Leave / Stay.
    // It overlays the screen that opened it, and focus starts on the safe choice.
    public sealed class ConfirmDialog : ScreenView {
        [SerializeField] TMP_Text message;
        [SerializeField] Button confirmButton;
        [SerializeField] Button cancelButton;
        [SerializeField] TMP_Text confirmLabel;
        [SerializeField] TMP_Text cancelLabel;

        ScreenRouter router;
        Action onConfirm;
        Action onCancel;

        protected override void Awake() {
            base.Awake();
            if (confirmButton != null) {
                confirmButton.onClick.AddListener(Confirm);
            }
            if (cancelButton != null) {
                cancelButton.onClick.AddListener(Cancel);
            }
        }

        public void Ask(ScreenRouter owner, string text, string confirmText, string cancelText, Action confirmed, Action cancelled) {
            router = owner;
            onConfirm = confirmed;
            onCancel = cancelled;
            if (message != null) {
                message.text = text;
            }
            if (confirmLabel != null) {
                confirmLabel.text = confirmText;
            }
            if (cancelLabel != null) {
                cancelLabel.text = cancelText;
            }
            router.Push(this);
        }

        public override void OnOpened() {
            // The safe choice has focus, so a stray Submit never destroys anything
            if (cancelButton != null && UnityEngine.EventSystems.EventSystem.current != null) {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
            }
        }

        public void Confirm() {
            Action action = onConfirm;
            Close();
            if (action != null) {
                action();
            }
        }

        public void Cancel() {
            Action action = onCancel;
            Close();
            if (action != null) {
                action();
            }
        }

        // It overlays the screen that asked
        public override bool HidesScreensBelow { get { return false; } }

        // Back (Esc / B) is the safe choice too
        public override bool CancelPops { get { return false; } }

        public override void OnCancel() {
            Cancel();
        }

        public override void OnClosed() {
            onConfirm = null;
            onCancel = null;
        }

        void Close() {
            if (router != null && router.Top == this) {
                router.Pop();
            }
        }
    }
}
