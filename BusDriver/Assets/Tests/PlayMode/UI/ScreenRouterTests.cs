using System.Collections;
using System.Collections.Generic;
using System.IO;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Input;
using BusDriver.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BusDriver.Tests.PlayMode.UI {
    // ROADMAP §4.13: the screen stack, focus, Cancel and the pause hand-off
    public class ScreenRouterTests {
        readonly List<Object> created = new List<Object>();
        InputActionAsset actions;
        PauseService pause;
        ScreenRouter router;
        EventSystem events;
        Transform canvas;

        [SetUp]
        public void SetUp() {
            actions = InputActionAsset.FromJson(File.ReadAllText("Assets/Input/BusDriver.inputactions"));
            created.Add(actions);
            InputService input = new InputService(actions, null);
            pause = new PauseService(input);
            pause.CanPause = () => true;

            events = EventSystem.current;
            if (events == null) {
                GameObject es = new GameObject("TestEventSystem");
                created.Add(es);
                events = es.AddComponent<EventSystem>();
            }
            GameObject root = new GameObject("TestCanvas", typeof(RectTransform));
            created.Add(root);
            root.AddComponent<Canvas>();
            canvas = root.transform;
            router = root.AddComponent<ScreenRouter>();
            router.Bind(pause, input);
        }

        [TearDown]
        public void TearDown() {
            router.Clear();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            actions.Disable();
            foreach (Object o in created) {
                Object.Destroy(o);
            }
            created.Clear();
        }

        ScreenView MakeScreen(string name, int buttons) {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            ScreenView view = go.AddComponent<ScreenView>();
            for (int i = 0; i < buttons; i++) {
                GameObject b = new GameObject(name + " button " + i, typeof(RectTransform));
                b.transform.SetParent(go.transform, false);
                b.AddComponent<Image>();
                b.AddComponent<Button>();
            }
            go.SetActive(false);
            return view;
        }

        static GameObject ButtonOf(ScreenView view, int index) {
            return view.GetComponentsInChildren<Button>(true)[index].gameObject;
        }

        [UnityTest]
        public IEnumerator FocusIsRestoredAfterAPop() {
            ScreenView a = MakeScreen("A", 2);
            ScreenView b = MakeScreen("B", 1);
            router.Push(a);
            Assert.AreEqual(ButtonOf(a, 0), events.currentSelectedGameObject, "the first Selectable has focus");
            events.SetSelectedGameObject(ButtonOf(a, 1));
            yield return null;

            router.Push(b);
            Assert.AreEqual(ButtonOf(b, 0), events.currentSelectedGameObject);
            Assert.IsFalse(a.gameObject.activeSelf, "a screen that hides the ones below hides them");

            router.Pop();
            Assert.IsTrue(a.gameObject.activeSelf);
            Assert.AreEqual(ButtonOf(a, 1), events.currentSelectedGameObject, "focus goes back where it was");
            Assert.IsTrue(a.GetComponent<CanvasGroup>().interactable);
        }

        [UnityTest]
        public IEnumerator CancelPopsTheTopScreen() {
            ScreenView a = MakeScreen("A", 1);
            ScreenView b = MakeScreen("B", 1);
            router.Push(a);
            router.Push(b);
            router.Back();
            yield return null;
            Assert.AreEqual(1, router.Count);
            Assert.AreSame(a, router.Top);
            Assert.IsFalse(b.IsOpen);
        }

        [UnityTest]
        public IEnumerator CancelOnAnEmptyStackAsksPauseServiceToPause() {
            ScreenView pauseView = MakeScreen("Pause", 2);
            router.PauseScreenView = pauseView;
            router.Back();
            Assert.IsTrue(pause.IsPaused, "Back with nothing open pauses");
            Assert.AreSame(pauseView, router.Top, "pausing pushes the pause screen");
            yield return null;

            router.Back();
            Assert.AreEqual(0, router.Count);
            Assert.IsFalse(pause.IsPaused, "backing out of the pause screen resumes");
        }

        [UnityTest]
        public IEnumerator PausingIsRefusedWhenNotAllowed() {
            pause.CanPause = () => false;
            router.Back();
            yield return null;
            Assert.IsFalse(pause.IsPaused);
            Assert.AreEqual(0, router.Count);
        }

        [UnityTest]
        public IEnumerator AConfirmDialogFocusesTheSafeChoiceAndBackCancels() {
            ScreenView a = MakeScreen("A", 1);
            router.Push(a);
            GameObject dialogObject = new GameObject("Confirm", typeof(RectTransform));
            dialogObject.transform.SetParent(canvas, false);
            dialogObject.SetActive(false);
            ConfirmDialog dialog = dialogObject.AddComponent<ConfirmDialog>();
            int confirmed = 0;
            int cancelled = 0;
            dialog.Ask(router, "Leaving now ends your run.", "LEAVE", "STAY", () => confirmed++, () => cancelled++);
            Assert.AreSame(dialog, router.Top);
            Assert.IsTrue(a.gameObject.activeSelf, "a dialog overlays the screen below");
            yield return null;

            router.Back();
            Assert.AreEqual(0, confirmed);
            Assert.AreEqual(1, cancelled, "Back is the safe choice");
            Assert.AreSame(a, router.Top);
        }

        [UnityTest]
        public IEnumerator FocusIsNeverLostWhileAScreenIsOpen() {
            ScreenView a = MakeScreen("A", 1);
            router.Push(a);
            events.SetSelectedGameObject(null);
            yield return null;
            yield return null;
            Assert.AreEqual(ButtonOf(a, 0), events.currentSelectedGameObject);
        }
    }
}
