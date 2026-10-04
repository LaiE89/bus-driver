using System;
using System.Collections.Generic;
using System.IO;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BusDriver.Tests.EditMode.Input {
    // ROADMAP §4.10: the action asset, its contexts and the rebinding rules. Every test works on a
    // copy of the asset, so nothing it enables or overrides leaks into the real one.
    public class InputActionsTests {
        const string AssetPath = "Assets/Input/BusDriver.inputactions";

        // The §4.10 table, "Map/Action"
        static readonly string[] RoadmapIds = {
            "Driving/Throttle", "Driving/Steer", "Driving/Handbrake", "Driving/Look", "Driving/CycleCamera",
            "Driving/Doors", "Driving/LeaveSeat", "Driving/Item1", "Driving/Item2", "Driving/Item3", "Driving/ResetBus",
            "Driving/AcceptRider", "Driving/RefuseRider",
            "OnFoot/Move", "OnFoot/Look", "OnFoot/Interact", "OnFoot/Kick",
            "Global/Pause", "Global/DebugOverlay", "Global/ToggleArt",
            "UI/Navigate", "UI/Submit", "UI/Cancel", "UI/Point", "UI/Click", "UI/ScrollWheel",
        };

        // Development-only actions have no pad binding in the §4.10 table; the pointer actions are
        // mouse by nature (a pad drives UI with Navigate/Submit/Cancel)
        static readonly string[] NoPadBinding = { "Global/DebugOverlay", "Global/ToggleArt", "UI/Point", "UI/Click", "UI/ScrollWheel" };

        // Both mean "back" on Esc; ScreenRouter handles them as one press (D57)
        static readonly string[][] AllowedSharedKeys = { new[] { "UI/Cancel", "Global/Pause" } };

        InputActionAsset copy;

        [SetUp]
        public void SetUp() {
            copy = InputActionAsset.FromJson(File.ReadAllText(AssetPath));
        }

        [TearDown]
        public void TearDown() {
            copy.Disable();
            UnityEngine.Object.DestroyImmediate(copy);
        }

        static string Id(InputAction action) {
            return action.actionMap.name + "/" + action.name;
        }

        [Test]
        public void EveryRoadmapActionExists() {
            List<string> ids = new List<string>();
            foreach (InputAction action in copy) {
                ids.Add(Id(action));
            }
            CollectionAssert.AreEquivalent(RoadmapIds, ids);
            CollectionAssert.AreEquivalent(RoadmapIds, BusDriverActions.AllIds);
            Assert.DoesNotThrow(() => new BusDriverActions(copy));
        }

        [Test]
        public void TheAssetIsTheProjectWideActionsAndInTheConfig() {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.IsNotNull(asset);
            Assert.AreSame(asset, InputSystem.actions, "project-wide actions");
            GameRootConfig config = AssetDatabase.LoadAssetAtPath<GameRootConfig>(GameRootConfig.AssetPath);
            Assert.AreSame(asset, config.inputActions, "GameRootConfig.inputActions");
            Assert.IsFalse(File.Exists("Assets/InputSystem_Actions.inputactions"), "the stock actions asset should be gone");
        }

        // Controller-ready rule 2 (§4.10)
        [Test]
        public void EveryActionHasAGamepadBinding() {
            List<string> missing = new List<string>();
            foreach (InputAction action in copy) {
                if (Array.IndexOf(NoPadBinding, Id(action)) >= 0) {
                    continue;
                }
                bool found = false;
                foreach (InputBinding binding in action.bindings) {
                    if (!binding.isComposite && binding.groups != null && binding.groups.Contains(InputService.GamepadGroup)) {
                        found = true;
                    }
                }
                if (!found) {
                    missing.Add(Id(action));
                }
            }
            Assert.IsEmpty(missing, "actions without a gamepad binding");
        }

        [Test]
        public void TheDefaultsFollowD48() {
            Assert.AreEqual("<Mouse>/rightButton", KeyboardPath("OnFoot/Interact"));
            // Same pairing as Accept/Refuse on the step: right talks, left kicks
            Assert.AreEqual("<Mouse>/leftButton", KeyboardPath("OnFoot/Kick"));
            Assert.AreEqual("<Keyboard>/q", KeyboardPath("Driving/Doors"));
            Assert.AreEqual("<Keyboard>/e", KeyboardPath("Driving/LeaveSeat"));
            Assert.AreEqual("<Keyboard>/space", KeyboardPath("Driving/CycleCamera"));
            // The MVP's door decision: interact waves them on, the kick button turns them away
            Assert.AreEqual("<Mouse>/rightButton", KeyboardPath("Driving/AcceptRider"));
            Assert.AreEqual("<Mouse>/leftButton", KeyboardPath("Driving/RefuseRider"));
        }

        string KeyboardPath(string id) {
            foreach (InputBinding binding in copy.FindAction(id, true).bindings) {
                if (binding.groups == InputService.KeyboardMouseGroup) {
                    return binding.path;
                }
            }
            return null;
        }

        [Test]
        public void NoTwoActionsInOneContextShareADefaultKey() {
            List<string> clashes = new List<string>();
            foreach (InputContext context in Enum.GetValues(typeof(InputContext))) {
                Dictionary<string, string> owner = new Dictionary<string, string>();
                foreach (string mapName in InputService.MapsFor(context)) {
                    foreach (InputAction action in copy.FindActionMap(mapName, true).actions) {
                        if (context == InputContext.Cinematic && action.name != "Pause") {
                            continue;
                        }
                        foreach (InputBinding binding in action.bindings) {
                            if (binding.isComposite || binding.groups != InputService.KeyboardMouseGroup) {
                                continue;
                            }
                            string key = binding.path.ToLowerInvariant();
                            string previous;
                            if (owner.TryGetValue(key, out previous) && previous != Id(action) && !Allowed(previous, Id(action))) {
                                clashes.Add($"{context}: {key} is {previous} and {Id(action)}");
                            }
                            owner[key] = Id(action);
                        }
                    }
                }
            }
            Assert.IsEmpty(clashes);
        }

        static bool Allowed(string a, string b) {
            foreach (string[] pair in AllowedSharedKeys) {
                if ((pair[0] == a && pair[1] == b) || (pair[0] == b && pair[1] == a)) {
                    return true;
                }
            }
            return false;
        }

        [Test]
        public void PauseIsLiveInEveryContextExceptNone() {
            InputService input = new InputService(copy, null);
            foreach (InputContext context in Enum.GetValues(typeof(InputContext))) {
                input.SetContext(context);
                Assert.AreEqual(context != InputContext.None, input.Actions.Pause.enabled, context.ToString());
            }
        }

        [Test]
        public void ContextsEnableOnlyTheirMaps() {
            InputService input = new InputService(copy, null);
            input.SetContext(InputContext.Driving);
            Assert.IsTrue(input.Actions.Throttle.enabled);
            Assert.IsFalse(input.Actions.Interact.enabled);
            Assert.IsFalse(input.Actions.Submit.enabled);
            input.SetContext(InputContext.OnFoot);
            Assert.IsFalse(input.Actions.Throttle.enabled);
            Assert.IsTrue(input.Actions.Interact.enabled);
            input.SetContext(InputContext.Cinematic);
            Assert.IsTrue(input.Actions.Pause.enabled);
            Assert.IsFalse(input.Actions.DebugOverlay.enabled);
            Assert.IsFalse(input.Actions.Throttle.enabled);
            input.SetContext(InputContext.None);
            Assert.IsFalse(copy.enabled);
        }

        [Test]
        public void AnOverrideWinsUntilCleared() {
            InputService input = new InputService(copy, null);
            List<InputContext> raised = new List<InputContext>();
            input.OnContextChanged += raised.Add;
            input.SetContext(InputContext.Driving);
            input.SetOverride(InputContext.Screen);
            Assert.AreEqual(InputContext.Screen, input.EffectiveContext);
            Assert.IsFalse(input.Actions.Throttle.enabled);
            Assert.IsTrue(input.Actions.Submit.enabled);
            input.SetContext(InputContext.OnFoot);
            Assert.AreEqual(InputContext.Screen, input.EffectiveContext, "the base context changes under the override");
            input.ClearOverride();
            Assert.AreEqual(InputContext.OnFoot, input.EffectiveContext);
            CollectionAssert.AreEqual(new[] { InputContext.Driving, InputContext.Screen, InputContext.OnFoot }, raised);
        }

        [Test]
        public void TickPutsTheContextBackWhenSomethingElseEnablesActions() {
            InputService input = new InputService(copy, null);
            input.SetContext(InputContext.Driving);
            // What an InputSystemUIInputModule does in OnEnable
            input.Actions.Submit.Enable();
            input.Tick();
            Assert.IsFalse(input.Actions.Submit.enabled);
            Assert.IsTrue(input.Actions.Throttle.enabled);
        }

        // ------------------------------------------------------------------ rebinding (§4.10)

        static int KeyboardIndex(InputAction action) {
            for (int i = 0; i < action.bindings.Count; i++) {
                if (action.bindings[i].groups == InputService.KeyboardMouseGroup) {
                    return i;
                }
            }
            return -1;
        }

        [Test]
        public void RebindingCycleCameraToCPersists() {
            InputService input = new InputService(copy, null);
            InputAction cycle = input.Actions.CycleCamera;
            string error;
            Assert.IsTrue(input.TryApplyBinding("CycleCamera", KeyboardIndex(cycle), "<Keyboard>/c", out error), error);
            StringAssert.AreEqualIgnoringCase("C", input.GetDisplayString("CycleCamera"));
            string json = input.SaveOverrides();

            InputActionAsset second = InputActionAsset.FromJson(File.ReadAllText(AssetPath));
            try {
                InputService reloaded = new InputService(second, null);
                reloaded.LoadOverrides(json);
                StringAssert.AreEqualIgnoringCase("C", reloaded.GetDisplayString("CycleCamera"));
            }finally {
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void AKeyUsedInTheSameMapIsRejected() {
            InputService input = new InputService(copy, null);
            string error;
            Assert.IsFalse(input.TryApplyBinding("CycleCamera", KeyboardIndex(input.Actions.CycleCamera), "<Keyboard>/q", out error));
            Assert.AreEqual("Already used by Doors", error);
            StringAssert.AreEqualIgnoringCase("Space", input.GetDisplayString("CycleCamera"));
        }

        [Test]
        public void AKeyUsedInGlobalIsRejected() {
            InputService input = new InputService(copy, null);
            string error;
            Assert.IsFalse(input.TryApplyBinding("Interact", KeyboardIndex(input.Actions.Interact), "<Keyboard>/escape", out error));
            Assert.AreEqual("Already used by Pause", error);
            // A Global action clashes with every map, because Global is live in every context
            Assert.IsFalse(input.TryApplyBinding("Pause", KeyboardIndex(input.Actions.Pause), "<Keyboard>/w", out error));
            Assert.AreEqual("Already used by Throttle", error);
        }

        [Test]
        public void AKeyUsedOnlyInAnotherContextIsAllowed() {
            InputService input = new InputService(copy, null);
            string error;
            Assert.IsTrue(input.TryApplyBinding("Interact", KeyboardIndex(input.Actions.Interact), "<Keyboard>/q", out error), error);
        }

        [Test]
        public void InteractTakesAMouseButtonButNeverMouseMovement() {
            InputService input = new InputService(copy, null);
            int index = KeyboardIndex(input.Actions.Interact);
            string error;
            Assert.IsTrue(input.TryApplyBinding("Interact", index, "<Mouse>/middleButton", out error), error);
            Assert.IsFalse(input.TryApplyBinding("Interact", index, "<Mouse>/delta", out error));
            Assert.AreEqual("Mouse movement can't be bound", error);
            Assert.IsFalse(input.TryApplyBinding("Interact", index, "<Mouse>/position", out error));
            Assert.IsFalse(input.TryApplyBinding("Interact", index, "<Mouse>/scroll/y", out error));
            Assert.AreEqual("<Mouse>/middleButton", input.Actions.Interact.bindings[index].effectivePath);
        }

        [Test]
        public void RebindableRowsCoverEveryKeyboardBindingButLook() {
            InputService input = new InputService(copy, null);
            List<string> labels = new List<string>();
            foreach (RebindableBinding row in input.RebindableBindings()) {
                labels.Add(row.ActionId + ":" + row.Label);
            }
            CollectionAssert.Contains(labels, "Driving/Throttle:Throttle (positive)");
            CollectionAssert.Contains(labels, "Driving/Throttle:Throttle (negative)");
            CollectionAssert.Contains(labels, "OnFoot/Move:Move (up)");
            CollectionAssert.Contains(labels, "Global/Pause:Pause");
            foreach (string label in labels) {
                StringAssert.DoesNotContain("Look", label);
                StringAssert.DoesNotStartWith("UI/", label);
            }
        }

        [Test]
        public void ResetAllRestoresTheDefaults() {
            InputService input = new InputService(copy, null);
            string error;
            input.TryApplyBinding("CycleCamera", KeyboardIndex(input.Actions.CycleCamera), "<Keyboard>/c", out error);
            input.ResetAll();
            StringAssert.AreEqualIgnoringCase("Space", input.GetDisplayString("CycleCamera"));
        }
    }
}
