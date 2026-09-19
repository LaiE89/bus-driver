using System.Collections;
using UnityEngine;
using TMPro;

public class ControlsMenu : MonoBehaviour {
    // Both mouse buttons and mouse movement are reserved for pointing, clicking
    // and aiming the head, so the camera swap is the only rebindable control and
    // it has to stay on the keyboard.
    public const KeyCode defaultSwitchCameraKey = KeyCode.Space;
    public static KeyCode switchCameraKey = defaultSwitchCameraKey;

    [SerializeField] TMP_Text switchCameraText;
    SoundController soundController;
    Event keyEvent;
    bool waitingForKey;
    KeyCode newKey;

    public void Awake() {
        waitingForKey = false;
        if (PlayerModeController.Instance != null) {
            soundController = PlayerModeController.Instance.soundController;
        }else {
            soundController = MainMenu.soundController;
        }
    }

    public void Start() {
        FixingText();
    }

    void OnGUI() {
        if (waitingForKey) {
            keyEvent = Event.current;
            if (keyEvent.isKey) {
                newKey = keyEvent.keyCode;
                waitingForKey = false;
            }
        }
    }

    public void StartGetButtonDown(TMP_Text text) {
        text.text = "PRESS A KEY";
        soundController.Play("UI Click");
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
        yield return new WaitForEndOfFrame();
        yield return WaitForKey();
        if (newKey != KeyCode.None) {
            switchCameraKey = newKey;
        }
        FixingText();
    }

    public void ResetKeybinds() {
        soundController.Play("UI Click");
        switchCameraKey = defaultSwitchCameraKey;
        FixingText();
    }

    public void FixingText() {
        switchCameraText.text = switchCameraKey.ToString().ToUpper();
    }

    public void ApplyingKeybinds() {
        soundController.Play("UI Click");
    }
}
