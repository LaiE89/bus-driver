using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DialogueTrigger : MonoBehaviour {
    public Dialogue dialogue;
    
    [Header("Trigger methods")]
    public bool whenEnabled;

    void OnEnable() {
        if (whenEnabled) {
            TriggerDialogue();
        }
    }

    public void TriggerDialogue() {
        if (PlayerModeController.Instance == null) {
            Debug.LogWarning("DialogueTrigger needs a PlayerModeController in the scene");
            return;
        }
        if (dialogue.isObjective) {
            if (PlayerModeController.Instance.objectivesController != null) {
                PlayerModeController.Instance.objectivesController.StartDialogue(dialogue);
            }
        }else if (PlayerModeController.Instance.dialogueController != null) {
            PlayerModeController.Instance.dialogueController.StartDialogue(dialogue);
        }
    }
}
