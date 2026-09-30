using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;

namespace BusDriver.UI.Screens {
    // Unused by the design; deleted in T-M1-19
    public class DialogueController : MonoBehaviour, IGameBindable {

        public TextMeshProUGUI nameText;
        public TextMeshProUGUI dialogueText;
        public string textSound = SoundIds.UiTypeTick;
        public Animator animator;
        public bool isPlaying;
        private Queue<string> sentences;
        GameServices game;

        public void Bind(GameServices services) {
            game = services;
        }

        void Start() {
            sentences = new Queue<string>();
        }

        public void StartDialogue(Dialogue dialogue) {
            isPlaying = true;
            sentences = new Queue<string>();
            animator.SetBool("isOpen", true);
            nameText.text = dialogue.name;
            if (dialogue.isCenter) {
                dialogueText.alignment = TextAlignmentOptions.Center;
                dialogueText.alignment = TextAlignmentOptions.Top;
            }else {
                dialogueText.alignment = TextAlignmentOptions.TopLeft;
            }
            sentences.Clear();

            foreach(string sentence in dialogue.sentences) {
                sentences.Enqueue(sentence);
            }
            DisplayNextSentence(dialogue);
        }

        public void DisplayNextSentence(Dialogue dialogue) {
            if (sentences.Count == 0) {
                EndDialogue();
                return;
            }

            string sentence = sentences.Dequeue();
            StopAllCoroutines();
            StartCoroutine(TypeSentence(sentence, dialogue));
        }

        IEnumerator TypeSentence (string sentence, Dialogue dialogue) {
            dialogueText.text = "";
            foreach (char letter in sentence.ToCharArray()) {
                dialogueText.text += letter;
                if (dialogue.audioSource != null) {
                    dialogue.audioSource.PlayOneShot(dialogue.audioSource.clip);
                }else {
                    if (!string.IsNullOrEmpty(textSound) && game != null) {
                        game.Audio.Play(textSound);
                    }
                }
                yield return new WaitForSeconds(0.01f);
            }
            yield return new WaitForSeconds(2f);
            DisplayNextSentence(dialogue);
        }

        void EndDialogue() {
            isPlaying = false;
            animator.SetBool("isOpen", false);
        }
    }
}
