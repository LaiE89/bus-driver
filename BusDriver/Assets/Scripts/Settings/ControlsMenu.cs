using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using BusDriver.UI.Menu;

namespace BusDriver.UI.Screens {
    public class ControlsMenu : MonoBehaviour, IGameBindable {
        public const KeyCode defaultSwitchCameraKey = KeyCode.Space;
        public static KeyCode switchCameraKey = defaultSwitchCameraKey;

        [SerializeField] TMP_Text switchCameraText;
        [SerializeField] TMP_Text handbrakeText;
        [SerializeField] TMP_Text doorsText;
        [SerializeField] TMP_Text leaveSeatText;
        [SerializeField] TMP_Text interactText;

        SoundController soundController;
        SettingsService settings;
        Event keyEvent;
        bool waitingForKey;
        KeyCode newKey;
        string pendingBind;

        const string BindSwitchCamera = "switchCamera";
        const string BindHandbrake = "handbrake";
        const string BindDoors = "doors";
        const string BindLeaveSeat = "leaveSeat";
        const string BindInteract = "interact";

        public void Awake() {
            waitingForKey = false;
            if (SceneController.Instance != null) {
                soundController = SceneController.Instance.soundController;
            }else {
                soundController = MainMenu.soundController;
            }
            if (Application.isPlaying) {
                EnsureDoorsRowVisible();
                LayoutBindRows();
                ResolveTextRefs();
            }
        }

        // Rebinds are written to settings.json at once (D56)
        public void Bind(GameServices game) {
            settings = game.Settings;
        }

        void SaveBindings() {
            if (settings != null) {
                settings.Save();
            }
        }

        public void Start() {
            FixingText();
        }

        void OnGUI() {
            if (!waitingForKey) {
                return;
            }
            keyEvent = Event.current;
            if (keyEvent == null) {
                return;
            }
            if (keyEvent.isKey && keyEvent.keyCode != KeyCode.None) {
                if (keyEvent.keyCode == KeyCode.Escape) {
                    waitingForKey = false;
                    pendingBind = null;
                    FixingText();
                    return;
                }
                newKey = keyEvent.keyCode;
                waitingForKey = false;
            }else if (keyEvent.isMouse) {
                if (keyEvent.button == 0) {
                    newKey = KeyCode.Mouse0;
                }else if (keyEvent.button == 1) {
                    newKey = KeyCode.Mouse1;
                }else if (keyEvent.button == 2) {
                    newKey = KeyCode.Mouse2;
                }else {
                    return;
                }
                waitingForKey = false;
            }
        }

        public void StartGetButtonDown(TMP_Text text) {
            StartRebind(BindSwitchCamera);
        }

        public void StartRebindSwitchCamera() {
            StartRebind(BindSwitchCamera);
        }

        public void StartRebindHandbrake() {
            StartRebind(BindHandbrake);
        }

        public void StartRebindDoors() {
            StartRebind(BindDoors);
        }

        public void StartRebindLeaveSeat() {
            StartRebind(BindLeaveSeat);
        }

        public void StartRebindInteract() {
            StartRebind(BindInteract);
        }

        public void StartRebind(string bindId) {
            pendingBind = bindId;
            TMP_Text text = TextFor(bindId);
            if (text != null) {
                text.text = "PRESS KEY";
            }
            if (soundController != null) {
                soundController.Play("UI Click");
            }
            StartCoroutine(AssignKey());
        }

        IEnumerator WaitForKey() {
            while (waitingForKey) {
                yield return null;
            }
        }

        IEnumerator AssignKey() {
            yield return new WaitForEndOfFrame();
            waitingForKey = true;
            newKey = KeyCode.None;
            yield return new WaitForEndOfFrame();
            yield return WaitForKey();
            if (newKey != KeyCode.None && !string.IsNullOrEmpty(pendingBind)) {
                SetBind(pendingBind, newKey);
            }
            pendingBind = null;
            FixingText();
            SaveBindings();
        }

        public void ResetKeybinds() {
            if (soundController != null) {
                soundController.Play("UI Click");
            }
            switchCameraKey = defaultSwitchCameraKey;
            GameKeys.ResetDefaults();
            FixingText();
            SaveBindings();
        }

        public void FixingText() {
            SetText(switchCameraText, switchCameraKey);
            SetText(handbrakeText, GameKeys.handbrake);
            SetText(doorsText, GameKeys.doors);
            SetText(leaveSeatText, GameKeys.leaveSeat);
            SetText(interactText, GameKeys.interact);
        }

        public bool HasBakedBinds() {
            return switchCameraText != null
                && handbrakeText != null
                && doorsText != null
                && leaveSeatText != null
                && interactText != null;
        }

        public void ApplyingKeybinds() {
            if (soundController != null) {
                soundController.Play("UI Click");
            }
        }

        void EnsureDoorsRowVisible() {
            Transform label = transform.Find("DOORS");
            Transform button = transform.Find("Doors Button");
            if (label != null) {
                label.gameObject.SetActive(true);
            }
            if (button != null) {
                button.gameObject.SetActive(true);
                Selectable selectable = button.GetComponent<Selectable>();
                if (selectable != null) {
                    selectable.enabled = true;
                }
                Button uiButton = button.GetComponent<Button>();
                if (uiButton != null) {
                    uiButton.onClick = new Button.ButtonClickedEvent();
                    uiButton.onClick.AddListener(StartRebindDoors);
                }
            }
        }

        void LayoutBindRows() {
            Transform switchButton = transform.Find("Switch Camera Button");
            Transform switchLabel = transform.Find("Switch Camera");
            if (switchButton == null) {
                return;
            }
            const float spacing = 55f;
            Vector2 buttonBase = switchButton.GetComponent<RectTransform>().anchoredPosition;
            Vector2 labelBase = switchLabel != null
                ? switchLabel.GetComponent<RectTransform>().anchoredPosition
                : buttonBase + new Vector2(-129.186f, -1.45f);

            PlaceRow("HANDBRAKE", "Handbrake Button", labelBase, buttonBase, -spacing);
            PlaceRow("DOORS", "Doors Button", labelBase, buttonBase, -spacing * 2f);
            PlaceRow("LEAVE SEAT", "Leave Seat Button", labelBase, buttonBase, -spacing * 3f);
            PlaceRow("INTERACT", "Interact Button", labelBase, buttonBase, -spacing * 4f);
        }

        void PlaceRow(string labelName, string buttonName, Vector2 labelBase, Vector2 buttonBase, float yOffset) {
            Transform label = transform.Find(labelName);
            Transform button = transform.Find(buttonName);
            if (label != null) {
                label.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(labelBase.x, labelBase.y + yOffset);
            }
            if (button != null) {
                button.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(buttonBase.x, buttonBase.y + yOffset);
            }
        }

        void ResolveTextRefs() {
            if (doorsText == null) {
                Transform doorsButton = transform.Find("Doors Button");
                if (doorsButton != null) {
                    doorsText = doorsButton.GetComponentInChildren<TMP_Text>(true);
                }
            }
        }

        static void SetText(TMP_Text text, KeyCode key) {
            if (text != null) {
                text.text = GameKeys.Label(key);
            }
        }

        static void SetBind(string bindId, KeyCode key) {
            switch (bindId) {
                case BindSwitchCamera: switchCameraKey = key; break;
                case BindHandbrake: GameKeys.handbrake = key; break;
                case BindDoors: GameKeys.doors = key; break;
                case BindLeaveSeat: GameKeys.leaveSeat = key; break;
                case BindInteract: GameKeys.interact = key; break;
            }
        }

        TMP_Text TextFor(string bindId) {
            switch (bindId) {
                case BindSwitchCamera: return switchCameraText;
                case BindHandbrake: return handbrakeText;
                case BindDoors: return doorsText;
                case BindLeaveSeat: return leaveSeatText;
                case BindInteract: return interactText;
                default: return null;
            }
        }
    }
}
