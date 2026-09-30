using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BusDriver.Gameplay.Player;

namespace BusDriver.UI.Screens {
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
            if (SceneController.Instance == null) {
                Debug.LogWarning("DialogueTrigger needs a SceneController in the scene");
                return;
            }
            if (dialogue.isObjective) {
                if (SceneController.Instance.objectivesController != null) {
                    SceneController.Instance.objectivesController.StartDialogue(dialogue);
                }
            }else if (SceneController.Instance.dialogueController != null) {
                SceneController.Instance.dialogueController.StartDialogue(dialogue);
            }
        }
    }
}
