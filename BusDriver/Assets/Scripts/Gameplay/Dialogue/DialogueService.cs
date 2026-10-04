using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Dialogue {
    // One rider speaks at a time (§4.13, was the MVP's DialogueController). Lines arrive from the
    // door, the seat and the drop-off; a line that lands while somebody is talking waits its turn
    // rather than cutting them off. Holds no UI: DialogueView draws whatever is current. Pausing
    // zeroes the time scale, so the typewriter stops with everything else.
    public sealed class DialogueService : MonoBehaviour {
        [Tooltip("Typewriter speed, seconds per character")]
        [SerializeField] float secondsPerCharacter = 0.02f;
        [Tooltip("How long a finished line stays on screen")]
        [SerializeField] float holdSeconds = 2f;
        [Tooltip("One blip per this many characters: the voice pool is far too small for one each")]
        [SerializeField] int charactersPerBlip = 3;
        [Tooltip("Lines waiting behind the one on screen; beyond this the oldest is dropped")]
        [SerializeField] int maxQueued = 8;

        struct Pending {
            public string Speaker;
            public string Text;
        }

        readonly Queue<Pending> pending = new Queue<Pending>();
        readonly StringBuilder visible = new StringBuilder();
        GameServices game;
        ShiftDirector director;
        Coroutine running;

        public bool IsPlaying { get { return running != null; } }
        public string Speaker { get; private set; } = "";
        // What the typewriter has revealed so far
        public string VisibleText { get; private set; } = "";
        public int QueuedCount { get { return pending.Count; } }

        // Speaker, text or visibility changed; DialogueView redraws on this
        public event Action OnChanged;

        // ShiftContext, beside the rider services (§4.5 step 5)
        public void Init(ShiftServices shift) {
            game = shift.Game;
            director = shift.Director;
            if (director != null) {
                director.OnStateChanged += HandleStateChanged;
            }
            shift.Debug.Register("Dialogue", WriteDebug);
        }

        void OnDestroy() {
            if (director != null) {
                director.OnStateChanged -= HandleStateChanged;
            }
        }

        // A rider mid-sentence when the driver dies or the shift ends stops there: the line has
        // nowhere left to be read, and the blips would carry on under the death
        void HandleStateChanged(ShiftState state) {
            if (state != ShiftState.Driving) {
                Clear();
            }
        }

        public void Say(string speaker, string text) {
            if (string.IsNullOrEmpty(text)) {
                return;
            }
            Pending line = new Pending { Speaker = string.IsNullOrEmpty(speaker) ? "" : speaker, Text = text };
            if (!IsPlaying) {
                Begin(line);
                return;
            }
            // A long backlog means something is spamming us; the newest line is the relevant one
            while (maxQueued > 0 && pending.Count >= maxQueued) {
                pending.Dequeue();
            }
            pending.Enqueue(line);
        }

        // Nothing carries across a night boundary or a death
        public void Clear() {
            pending.Clear();
            StopRunning();
            Speaker = "";
            VisibleText = "";
            Changed();
        }

        void OnDisable() {
            // The coroutine is already dead; keep the view from showing a half-typed line
            pending.Clear();
            running = null;
            Speaker = "";
            VisibleText = "";
            Changed();
        }

        void Begin(Pending line) {
            StopRunning();
            Speaker = line.Speaker;
            running = StartCoroutine(TypeLine(line.Text));
        }

        IEnumerator TypeLine(string text) {
            visible.Length = 0;
            VisibleText = "";
            Changed();
            float perCharacter = Mathf.Max(0f, secondsPerCharacter);
            for (int i = 0; i < text.Length; i++) {
                visible.Append(text[i]);
                VisibleText = visible.ToString();
                Changed();
                if (charactersPerBlip > 0 && i % charactersPerBlip == 0 && !char.IsWhiteSpace(text[i])) {
                    Blip();
                }
                if (perCharacter > 0f) {
                    yield return new WaitForSeconds(perCharacter);
                }
            }
            if (holdSeconds > 0f) {
                yield return new WaitForSeconds(holdSeconds);
            }
            running = null;
            if (pending.Count > 0) {
                Begin(pending.Dequeue());
                yield break;
            }
            Speaker = "";
            VisibleText = "";
            Changed();
        }

        // ui.type_tick already carries the MVP's Dialogue.wav at its old volume and pitch, so the
        // typewriter sounds the way it used to without a second id for the same clip
        void Blip() {
            if (game != null && game.Audio != null) {
                game.Audio.Play(SoundIds.UiTypeTick);
            }
        }

        void StopRunning() {
            if (running != null) {
                StopCoroutine(running);
                running = null;
            }
        }

        void Changed() {
            if (OnChanged != null) {
                OnChanged();
            }
        }

        void WriteDebug(StringBuilder text) {
            if (!IsPlaying) {
                text.Append("idle");
                if (pending.Count > 0) {
                    text.Append("  queued ").Append(pending.Count);
                }
                text.Append('\n');
                return;
            }
            text.Append(Speaker.Length > 0 ? Speaker : "?").Append(": ").Append(VisibleText).Append('\n');
            text.Append("queued ").Append(pending.Count).Append('\n');
        }
    }
}
