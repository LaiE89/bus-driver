using UnityEngine;

namespace BusDriver.UI.Screens {
    // Inherited and unused by the design; T-M1-19 deletes the dialogue system. Its controllers
    // were reached through SceneController, which T-M1-15 split up.
    public class DialogueTrigger : MonoBehaviour {
        public Dialogue dialogue;
        [SerializeField] DialogueController dialogueController;
        [SerializeField] DialogueController objectivesController;

        [Header("Trigger methods")]
        public bool whenEnabled;

        void OnEnable() {
            if (whenEnabled) {
                TriggerDialogue();
            }
        }

        public void TriggerDialogue() {
            DialogueController target = dialogue.isObjective ? objectivesController : dialogueController;
            if (target != null) {
                target.StartDialogue(dialogue);
            }
        }
    }
}
