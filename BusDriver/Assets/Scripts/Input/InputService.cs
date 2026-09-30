using System;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Flow;
using UnityEngine.InputSystem;

namespace BusDriver.Gameplay.Input {
    // The outcome of a rebind (§4.10). Error is the player-facing reason when it was refused.
    public struct RebindResult {
        public bool Success;
        public bool Cancelled;
        public string Error;
        public string ActionId;
        public int BindingIndex;
    }

    // One row of the Controls screen: a keyboard/mouse binding the player may rebind
    public struct RebindableBinding {
        public InputAction Action;
        public int BindingIndex;
        // "Map/Action"
        public string ActionId;
        // "Throttle (positive)" for a composite part, else the action name
        public string Label;
    }

    // Owns the action asset for the whole game (§4.6, §4.10): which maps are live per input
    // context, rebinding with the §4.10 rules, and the overrides kept in settings.json.
    public sealed class InputService {
        public const string KeyboardMouseGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";
        // Mouse delta is in pixels; the legacy "Mouse X/Y" axes were pixels × 0.1, and the look
        // sensitivity was tuned on those (§4.10: yaw += delta.x × 0.1 × sensitivity × 0.02)
        public const float MouseAxisScale = 0.1f;

        // Mouse axes and position can never be bound (D48); mouse buttons can
        static readonly string[] UnbindablePaths = { "<Mouse>/delta", "<Mouse>/position", "<Mouse>/scroll", "<Pointer>/delta", "<Pointer>/position" };
        static readonly string[] RebindableMaps = { BusDriverActions.DrivingMap, BusDriverActions.OnFootMap, BusDriverActions.GlobalMap };

        readonly SettingsService settings;
        InputActionRebindingExtensions.RebindingOperation rebind;
        InputContext overrideContext;
        bool hasOverride;
        string appliedOverridesJson;

        public BusDriverActions Actions { get; }
        public InputActionAsset Asset { get { return Actions.Asset; } }
        // What the game asked for; PauseService may be overriding it
        public InputContext Context { get; private set; } = InputContext.None;
        public InputContext EffectiveContext { get { return hasOverride ? overrideContext : Context; } }
        public bool IsRebinding { get { return rebind != null; } }

        // The effective context changed (CursorService listens)
        public event Action<InputContext> OnContextChanged;
        // A binding or override set changed (prompts rebuild their text)
        public event Action OnBindingsChanged;

        // settings may be null in tests; nothing is then persisted
        public InputService(InputActionAsset asset, SettingsService settings) {
            Actions = new BusDriverActions(asset);
            this.settings = settings;
            if (settings != null) {
                LoadOverrides(settings.Current.bindingOverridesJson);
                // Restore defaults in Options clears the overrides too (§2.23)
                settings.OnChanged += HandleSettingsChanged;
            }
            ApplyContext();
        }

        // The maps each context enables (§4.10). Cinematic keeps only Global/Pause live.
        public static string[] MapsFor(InputContext context) {
            switch (context) {
                case InputContext.Menu:
                case InputContext.Screen:
                    return new[] { BusDriverActions.UIMap, BusDriverActions.GlobalMap };
                case InputContext.Driving:
                    return new[] { BusDriverActions.DrivingMap, BusDriverActions.GlobalMap };
                case InputContext.OnFoot:
                    return new[] { BusDriverActions.OnFootMap, BusDriverActions.GlobalMap };
                case InputContext.Cinematic:
                    return new[] { BusDriverActions.GlobalMap };
                default:
                    return new string[0];
            }
        }

        public void SetContext(InputContext context) {
            InputContext before = EffectiveContext;
            Context = context;
            ApplyContext();
            RaiseIfChanged(before);
        }

        // PauseService shows Screen while paused, whatever the game wanted (§4.11)
        public void SetOverride(InputContext context) {
            InputContext before = EffectiveContext;
            overrideContext = context;
            hasOverride = true;
            ApplyContext();
            RaiseIfChanged(before);
        }

        public void ClearOverride() {
            if (!hasOverride) {
                return;
            }
            InputContext before = EffectiveContext;
            hasOverride = false;
            ApplyContext();
            RaiseIfChanged(before);
        }

        // Called every frame by GameRoot. Other code can enable actions behind our back: the
        // Input System enables project-wide actions on entering play, and every
        // InputSystemUIInputModule enables its UI actions in OnEnable. This puts the context back.
        // No allocations: it only compares enabled flags.
        public void Tick() {
            if (!IsApplied()) {
                ApplyContext();
            }
        }

        void RaiseIfChanged(InputContext before) {
            InputContext now = EffectiveContext;
            if (now != before) {
                Log.Info(LogCat.Input, $"input context {before} -> {now}");
                if (OnContextChanged != null) {
                    OnContextChanged(now);
                }
            }
        }

        bool WantsMap(InputActionMap map, InputContext context) {
            if (IsRebinding) {
                return false;
            }
            switch (context) {
                case InputContext.Menu:
                case InputContext.Screen:
                    return map == Actions.UI || map == Actions.Global;
                case InputContext.Driving:
                    return map == Actions.Driving || map == Actions.Global;
                case InputContext.OnFoot:
                    return map == Actions.OnFoot || map == Actions.Global;
                default:
                    return false;
            }
        }

        bool IsApplied() {
            InputContext context = EffectiveContext;
            if (!MapApplied(Actions.Driving, context) || !MapApplied(Actions.OnFoot, context) || !MapApplied(Actions.UI, context)) {
                return false;
            }
            if (context == InputContext.Cinematic && !IsRebinding) {
                return Actions.Pause.enabled && !Actions.DebugOverlay.enabled && !Actions.ToggleArt.enabled;
            }
            return MapApplied(Actions.Global, context);
        }

        bool MapApplied(InputActionMap map, InputContext context) {
            bool want = WantsMap(map, context);
            if (!want) {
                // A map counts as enabled when any one of its actions is
                return !map.enabled;
            }
            for (int i = 0; i < map.actions.Count; i++) {
                if (!map.actions[i].enabled) {
                    return false;
                }
            }
            return true;
        }

        void ApplyContext() {
            InputContext context = EffectiveContext;
            SetMap(Actions.Driving, WantsMap(Actions.Driving, context));
            SetMap(Actions.OnFoot, WantsMap(Actions.OnFoot, context));
            SetMap(Actions.UI, WantsMap(Actions.UI, context));
            if (context == InputContext.Cinematic && !IsRebinding) {
                Actions.Global.Disable();
                Actions.Pause.Enable();
            }else {
                SetMap(Actions.Global, WantsMap(Actions.Global, context));
            }
        }

        static void SetMap(InputActionMap map, bool enabled) {
            if (enabled) {
                map.Enable();
            }else {
                map.Disable();
            }
        }

        // ------------------------------------------------------------------ display strings

        // What a prompt shows for an action on keyboard and mouse (controller-ready rule 3,
        // §4.10). M4b switches the group with the active device.
        public string GetDisplayString(string actionId) {
            InputAction action = Actions.Find(actionId);
            if (action == null) {
                Log.Warn(LogCat.Input, "no input action " + actionId);
                return "?";
            }
            return GetDisplayString(action);
        }

        public string GetDisplayString(InputAction action) {
            return action.GetBindingDisplayString(InputBinding.MaskByGroup(KeyboardMouseGroup));
        }

        public string GetBindingDisplayString(InputAction action, int bindingIndex) {
            return action.GetBindingDisplayString(bindingIndex);
        }

        // ------------------------------------------------------------------ rebinding

        // Every keyboard/mouse binding of Driving, OnFoot and Global except Look (§4.10), composite
        // parts one row each
        public List<RebindableBinding> RebindableBindings() {
            List<RebindableBinding> rows = new List<RebindableBinding>();
            for (int m = 0; m < RebindableMaps.Length; m++) {
                InputActionMap map = Asset.FindActionMap(RebindableMaps[m], true);
                for (int a = 0; a < map.actions.Count; a++) {
                    InputAction action = map.actions[a];
                    if (action.name == "Look") {
                        continue;
                    }
                    for (int b = 0; b < action.bindings.Count; b++) {
                        InputBinding binding = action.bindings[b];
                        if (binding.isComposite || !InGroup(binding, KeyboardMouseGroup)) {
                            continue;
                        }
                        rows.Add(new RebindableBinding {
                            Action = action,
                            BindingIndex = b,
                            ActionId = map.name + "/" + action.name,
                            Label = binding.isPartOfComposite ? $"{action.name} ({binding.name})" : action.name,
                        });
                    }
                }
            }
            return rows;
        }

        static bool InGroup(InputBinding binding, string group) {
            return !string.IsNullOrEmpty(binding.groups) && Array.IndexOf(binding.groups.Split(InputBinding.Separator), group) >= 0;
        }

        // Validates and applies one keyboard/mouse binding. Refused: mouse movement, anything that
        // isn't a key or mouse button, and a control already used in the same map or in Global
        // (every map when the action itself is in Global, since Global is live in every context).
        public bool TryApplyBinding(string actionId, int bindingIndex, string path, out string error) {
            InputAction action = Actions.Find(actionId);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) {
                error = "Unknown binding";
                return false;
            }
            return TryApplyBinding(action, bindingIndex, path, out error);
        }

        public bool TryApplyBinding(InputAction action, int bindingIndex, string path, out string error) {
            if (!IsBindablePath(path, out error)) {
                return false;
            }
            InputAction clash = FindClash(action, bindingIndex, path);
            if (clash != null) {
                error = "Already used by " + clash.name;
                return false;
            }
            action.ApplyBindingOverride(bindingIndex, path);
            error = null;
            Persist();
            return true;
        }

        public static bool IsBindablePath(string path, out string error) {
            error = null;
            if (string.IsNullOrEmpty(path)) {
                error = "Nothing pressed";
                return false;
            }
            for (int i = 0; i < UnbindablePaths.Length; i++) {
                if (path.StartsWith(UnbindablePaths[i], StringComparison.OrdinalIgnoreCase)) {
                    error = "Mouse movement can't be bound";
                    return false;
                }
            }
            if (!path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase)) {
                error = "Only keys and mouse buttons can be bound here";
                return false;
            }
            return true;
        }

        InputAction FindClash(InputAction action, int bindingIndex, string path) {
            bool isGlobal = action.actionMap == Actions.Global;
            for (int m = 0; m < RebindableMaps.Length; m++) {
                InputActionMap map = Asset.FindActionMap(RebindableMaps[m], true);
                if (!isGlobal && map != action.actionMap && map != Actions.Global) {
                    continue;
                }
                for (int a = 0; a < map.actions.Count; a++) {
                    InputAction other = map.actions[a];
                    for (int b = 0; b < other.bindings.Count; b++) {
                        if (other == action && b == bindingIndex) {
                            continue;
                        }
                        InputBinding binding = other.bindings[b];
                        if (binding.isComposite || !InGroup(binding, KeyboardMouseGroup)) {
                            continue;
                        }
                        if (string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase)) {
                            return other;
                        }
                    }
                }
            }
            return null;
        }

        // Waits for a key or mouse button, then validates it like TryApplyBinding. Escape cancels.
        // Every map is off while it runs, so the key that's pressed does nothing else (Escape
        // would otherwise also pop the Controls screen).
        public void StartRebind(string actionId, int bindingIndex, Action<RebindResult> done) {
            InputAction action = Actions.Find(actionId);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count || action.bindings[bindingIndex].isComposite) {
                Finish(done, new RebindResult { ActionId = actionId, BindingIndex = bindingIndex, Error = "Unknown binding" });
                return;
            }
            CancelRebind();
            string previousOverride = action.bindings[bindingIndex].overridePath;
            rebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithExpectedControlType("Button")
                .OnMatchWaitForAnother(0.1f)
                .OnCancel(op => {
                    EndRebind();
                    Finish(done, new RebindResult { Cancelled = true, ActionId = actionId, BindingIndex = bindingIndex });
                })
                .OnComplete(op => {
                    // The operation has already applied its override; put the old one back and
                    // re-apply through the checks
                    string chosen = action.bindings[bindingIndex].overridePath;
                    RestoreOverride(action, bindingIndex, previousOverride);
                    EndRebind();
                    string error;
                    bool ok = TryApplyBinding(action, bindingIndex, chosen, out error);
                    Finish(done, new RebindResult { Success = ok, Error = error, ActionId = actionId, BindingIndex = bindingIndex });
                });
            // Every map is off while waiting (see above)
            ApplyContext();
            rebind.Start();
        }

        public void CancelRebind() {
            if (rebind != null) {
                rebind.Cancel();
            }
        }

        void EndRebind() {
            if (rebind != null) {
                rebind.Dispose();
                rebind = null;
            }
            ApplyContext();
        }

        static void RestoreOverride(InputAction action, int bindingIndex, string previousOverride) {
            if (string.IsNullOrEmpty(previousOverride)) {
                action.RemoveBindingOverride(bindingIndex);
            }else {
                action.ApplyBindingOverride(bindingIndex, previousOverride);
            }
        }

        void Finish(Action<RebindResult> done, RebindResult result) {
            if (result.Success) {
                Log.Info(LogCat.Input, $"rebound {result.ActionId}[{result.BindingIndex}]");
            }else if (!result.Cancelled) {
                Log.Info(LogCat.Input, $"rebind of {result.ActionId}[{result.BindingIndex}] refused: {result.Error}");
            }
            if (done != null) {
                done(result);
            }
        }

        public void ResetBinding(string actionId) {
            InputAction action = Actions.Find(actionId);
            if (action == null) {
                return;
            }
            for (int b = 0; b < action.bindings.Count; b++) {
                if (InGroup(action.bindings[b], KeyboardMouseGroup)) {
                    action.RemoveBindingOverride(b);
                }
            }
            Persist();
        }

        public void ResetAll() {
            Asset.RemoveAllBindingOverrides();
            Persist();
        }

        // ------------------------------------------------------------------ persistence

        public string SaveOverrides() {
            return Asset.SaveBindingOverridesAsJson();
        }

        public void LoadOverrides(string json) {
            Asset.RemoveAllBindingOverrides();
            if (!string.IsNullOrEmpty(json)) {
                try {
                    Asset.LoadBindingOverridesFromJson(json);
                }catch (Exception e) {
                    Log.Warn(LogCat.Input, "binding overrides unreadable, using the defaults: " + e.Message);
                    Asset.RemoveAllBindingOverrides();
                }
            }
            appliedOverridesJson = json ?? "";
            RaiseBindingsChanged();
        }

        // Rebinds are rare, so each one is written to settings.json straight away
        void Persist() {
            string json = SaveOverrides();
            appliedOverridesJson = json;
            if (settings != null) {
                settings.Current.bindingOverridesJson = json;
                settings.Save();
            }
            RaiseBindingsChanged();
        }

        void HandleSettingsChanged() {
            string json = settings.Current.bindingOverridesJson ?? "";
            if (json != appliedOverridesJson) {
                LoadOverrides(json);
            }
        }

        void RaiseBindingsChanged() {
            if (OnBindingsChanged != null) {
                OnBindingsChanged();
            }
        }
    }
}
