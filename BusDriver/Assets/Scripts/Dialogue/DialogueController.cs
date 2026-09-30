using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueController : MonoBehaviour {

    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogueText;
    public string textSound;
    public Animator animator;
    // Shown and hidden directly when there is no Animator to drive the "isOpen" state
    public GameObject panelRoot;
    public bool isPlaying;
    private Queue<string> sentences;
    // Passengers talk one after another, so a second line waits its turn
    private readonly Queue<Dialogue> pending = new Queue<Dialogue>();

    void Start() {
        sentences = new Queue<string>();
    }

    public void QueueDialogue(Dialogue dialogue) {
        if (dialogue == null) {
            return;
        }
        if (isPlaying) {
            pending.Enqueue(dialogue);
            return;
        }
        StartDialogue(dialogue);
    }

    public void StartDialogue(Dialogue dialogue) {
        if (dialogueText == null) {
            return;
        }
        isPlaying = true;
        sentences = new Queue<string>();
        SetOpen(true);
        if (nameText != null) {
            nameText.text = dialogue.name;
        }
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
                if (textSound != null) {
                    if (SceneController.Instance != null && SceneController.Instance.soundController != null) {
                        SceneController.Instance.soundController.PlayOneShot(textSound);
                    }
                }
            }
            yield return new WaitForSeconds(0.01f);
        }
        yield return new WaitForSeconds(2f);
        DisplayNextSentence(dialogue);
    }

    void EndDialogue() {
        isPlaying = false;
        SetOpen(false);
        if (pending.Count > 0) {
            StartDialogue(pending.Dequeue());
        }
    }

    void SetOpen(bool open) {
        if (animator != null) {
            animator.SetBool("isOpen", open);
            return;
        }
        if (panelRoot != null) {
            panelRoot.SetActive(open);
        }
    }
}
