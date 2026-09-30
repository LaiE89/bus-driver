using System.Collections.Generic;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BusDriver.UI.Screens {
    // The modal screen stack (§4.13). Push/Pop/Replace, focus on the first Selectable of the top
    // screen and back to the covered screen's selection on pop. "Back" (UI/Cancel or Global/Pause,
    // one press per frame) pops the top screen, or asks PauseService to pause when nothing is open.
    // Pausing pushes the pause screen; emptying the stack while paused resumes.
    public sealed class ScreenRouter : MonoBehaviour, IGameBindable {
        [Tooltip("Pushed when the game pauses")]
        [SerializeField] ScreenView pauseScreen;

        readonly List<ScreenView> stack = new List<ScreenView>();
        readonly List<GameObject> savedSelection = new List<GameObject>();
        PauseService pause;
        InputService input;

        public int Count { get { return stack.Count; } }
        // Tests set the pause screen without a prefab
        internal ScreenView PauseScreenView { set { pauseScreen = value; } }
        public ScreenView Top { get { return stack.Count > 0 ? stack[stack.Count - 1] : null; } }

        public void Bind(GameServices game) {
            Bind(game.Pause, game.Input);
        }

        internal void Bind(PauseService pauseService, InputService inputService) {
            Unbind();
            pause = pauseService;
            input = inputService;
            pause.OnPauseChanged += HandlePauseChanged;
        }

        void OnDestroy() {
            Unbind();
        }

        void Unbind() {
            if (pause != null) {
                pause.OnPauseChanged -= HandlePauseChanged;
            }
        }

        void Update() {
            if (input != null && (input.Actions.Cancel.WasPerformedThisFrame() || input.Actions.Pause.WasPerformedThisFrame())) {
                Back();
            }
            // Controller-ready rule 4: focus is never lost while a screen is open
            ScreenView top = Top;
            EventSystem events = EventSystem.current;
            if (top != null && events != null && (events.currentSelectedGameObject == null || !events.currentSelectedGameObject.activeInHierarchy)) {
                Focus(top);
            }
        }

        // Esc, or B on a pad
        public void Back() {
            ScreenView top = Top;
            if (top != null) {
                if (top.CancelPops) {
                    Pop();
                }else {
                    top.OnCancel();
                }
                return;
            }
            if (pause != null && pause.IsPauseAllowed) {
                pause.TrySetPaused(true);
            }
        }

        public void Push(ScreenView view) {
            if (view == null || stack.Contains(view)) {
                return;
            }
            EventSystem events = EventSystem.current;
            savedSelection.Add(events != null ? events.currentSelectedGameObject : null);
            stack.Add(view);
            Refresh();
            view.OnOpened();
            Focus(view);
        }

        public void Pop() {
            if (stack.Count == 0) {
                return;
            }
            ScreenView closed = stack[stack.Count - 1];
            GameObject restore = savedSelection[savedSelection.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            savedSelection.RemoveAt(savedSelection.Count - 1);
            closed.SetState(false, false, false);
            Refresh();
            closed.OnClosed();
            EventSystem events = EventSystem.current;
            if (events != null) {
                events.SetSelectedGameObject(restore != null && restore.activeInHierarchy ? restore : null);
            }
            if (stack.Count == 0 && pause != null && pause.IsPaused) {
                pause.TrySetPaused(false);
            }
        }

        public void Replace(ScreenView view) {
            if (stack.Count == 0) {
                Push(view);
                return;
            }
            ScreenView closed = stack[stack.Count - 1];
            stack[stack.Count - 1] = view;
            closed.SetState(false, false, false);
            Refresh();
            closed.OnClosed();
            view.OnOpened();
            Focus(view);
        }

        public void Clear() {
            while (stack.Count > 0) {
                ScreenView closed = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                savedSelection.RemoveAt(savedSelection.Count - 1);
                closed.SetState(false, false, false);
                closed.OnClosed();
            }
        }

        // Top screen: visible and interactive. Below it: visible only down to the first screen
        // that hides the ones under it.
        void Refresh() {
            bool visible = true;
            for (int i = stack.Count - 1; i >= 0; i--) {
                stack[i].SetState(true, visible, i == stack.Count - 1);
                if (stack[i].HidesScreensBelow) {
                    visible = false;
                }
            }
        }

        static void Focus(ScreenView view) {
            EventSystem events = EventSystem.current;
            if (events == null) {
                return;
            }
            UnityEngine.UI.Selectable first = view.FirstSelectable;
            events.SetSelectedGameObject(first != null ? first.gameObject : null);
        }

        void HandlePauseChanged(bool paused) {
            if (paused) {
                if (pauseScreen != null) {
                    Push(pauseScreen);
                }
            }else {
                Clear();
            }
        }
    }
}
