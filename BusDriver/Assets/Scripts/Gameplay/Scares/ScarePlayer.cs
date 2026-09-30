using System;
using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Audio;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Shift;
using UnityEngine;

namespace BusDriver.Gameplay.Scares {
    // Plays scares step by step (§2.17, §4.6, D36): every ScareStepKind, several scares at once
    // (hallucinations overlap monster scares), and each one puts back what it changed when it ends
    // or is interrupted: the camera view, the lights, the input, the shake, the overlays, the
    // riders' heads, the driver's look. A Kill-tier scare ends in a death, so it leaves the screen
    // black, the view and the input as they are for the death presenter. Scare intensity Reduced
    // (§2.17) turns flashes into 0.15 s fades at no more than 50 % opacity, drops camera shake and
    // plays every scare sound 6 dB down; timing is unchanged. Scaled time throughout: pausing
    // holds a scare mid-step.
    public sealed class ScarePlayer : MonoBehaviour {
        [Tooltip("The greybox scare head (Generated/Prefabs/Scares)")]
        [SerializeField] GameObject scareHeadPrefab;
        [Tooltip("ShowOverlay's texture when a step has none")]
        [SerializeField] Texture2D defaultOverlay;
        [Tooltip("Reduced intensity: a flash fades in and out over this long, seconds")]
        [SerializeField] float reducedFadeSeconds = 0.15f;
        [Tooltip("Reduced intensity: the most any overlay may cover")]
        [SerializeField] float reducedMaxAlpha = 0.5f;
        [Tooltip("Reduced intensity: scare sounds play at this volume (−6 dB)")]
        [SerializeField] float reducedVolume = 0.5f;

        sealed class Instance {
            public int Id;
            public ScareDefinition Definition;
            public ScareContext Context;
            public Coroutine Routine;
            public readonly List<Coroutine> Steps = new List<Coroutine>();
            public int ActiveSteps;
            public readonly List<SoundHandle> Sounds = new List<SoundHandle>();
            public readonly List<GameObject> Heads = new List<GameObject>();
            public readonly List<Passenger> Turned = new List<Passenger>();
            public bool ChangedView;
            public int ViewBefore;
            public int ViewSet;
            public bool LockedInput;
            public InputContext InputBefore;
            public bool Looked;
            public float YawBefore;
            public float PitchBefore;
            public bool Blackout;
            public float FadeBefore;
            public bool Overlay;
            public bool Static;
            public bool Hands;
        }

        readonly List<Instance> playing = new List<Instance>();
        ShiftServices shift;
        int nextId;

        public bool IsPlaying { get { return playing.Count > 0; } }
        public int PlayingCount { get { return playing.Count; } }
        public ScareOverlayState Overlay { get { return shift != null ? shift.ScareOverlay : null; } }
        public bool Reduced {
            get { return shift != null && shift.Game.Settings.Current.scareIntensity == ScareIntensity.Reduced; }
        }

        // ShiftContext, step 8 of the Init order (§4.5), before ScareDirector
        public void Init(ShiftServices services) {
            shift = services;
        }

        // Starts a scare on this object (its lifetime is the night's). Several may run at once.
        public ScareHandle Begin(ScareDefinition definition, ScareContext context) {
            if (definition == null) {
                return default(ScareHandle);
            }
            Instance instance = new Instance { Id = ++nextId, Definition = definition, Context = context };
            playing.Add(instance);
            Log.Verbose(LogCat.Scare, "play " + definition.id);
            instance.Routine = StartCoroutine(Run(instance));
            return new ScareHandle(instance.Id);
        }

        public bool IsRunning(ScareHandle handle) {
            return Find(handle.Id) != null;
        }

        // Runs the scare to its end: `yield return player.Play(...)` from the caller's coroutine
        public IEnumerator Play(ScareDefinition definition, ScareContext context) {
            ScareHandle handle = Begin(definition, context);
            while (IsRunning(handle)) {
                yield return null;
            }
        }

        // Stops every running scare and puts back what each changed (a Kill preempting, §2.17)
        public void Interrupt() {
            while (playing.Count > 0) {
                Stop(playing[playing.Count - 1]);
            }
        }

        public void Interrupt(ScareHandle handle) {
            Instance instance = Find(handle.Id);
            if (instance != null) {
                Stop(instance);
            }
        }

        void OnDisable() {
            Interrupt();
        }

        Instance Find(int id) {
            for (int i = 0; i < playing.Count; i++) {
                if (playing[i].Id == id) {
                    return playing[i];
                }
            }
            return null;
        }

        void Stop(Instance instance) {
            if (instance.Routine != null) {
                StopCoroutine(instance.Routine);
            }
            for (int i = 0; i < instance.Steps.Count; i++) {
                if (instance.Steps[i] != null) {
                    StopCoroutine(instance.Steps[i]);
                }
            }
            for (int i = 0; i < instance.Sounds.Count; i++) {
                shift.Game.Audio.Stop(instance.Sounds[i]);
            }
            Finish(instance);
        }

        // The steps in start order. An insertion sort: stable, so same-time steps keep the
        // definition's order, and a scare has a handful of steps.
        IEnumerator Run(Instance instance) {
            ScareStep[] steps = (ScareStep[])instance.Definition.steps.Clone();
            for (int i = 1; i < steps.Length; i++) {
                ScareStep step = steps[i];
                int j = i - 1;
                while (j >= 0 && steps[j].at > step.at) {
                    steps[j + 1] = steps[j];
                    j--;
                }
                steps[j + 1] = step;
            }
            float time = 0f;
            int next = 0;
            while (true) {
                while (next < steps.Length && steps[next].at <= time) {
                    instance.Steps.Add(StartCoroutine(RunStep(instance, steps[next])));
                    next++;
                }
                if (next >= steps.Length && instance.ActiveSteps == 0) {
                    break;
                }
                yield return null;
                time += Time.deltaTime;
            }
            Finish(instance);
        }

        IEnumerator RunStep(Instance instance, ScareStep step) {
            instance.ActiveSteps++;
            switch (step.kind) {
                case ScareStepKind.PlaySound: yield return PlaySound(instance, step); break;
                case ScareStepKind.ShowOverlay: yield return ShowOverlay(instance, step); break;
                case ScareStepKind.CameraShake: yield return CameraShake(instance, step); break;
                case ScareStepKind.FlickerCabinLights: FlickerLights(instance, step); break;
                case ScareStepKind.CctvStatic: yield return CctvStatic(instance, step); break;
                case ScareStepKind.LockInput: yield return LockInput(instance, step); break;
                case ScareStepKind.ForceHomeView: SetView(instance, -1); break;
                case ScareStepKind.CutToCctv: SetView(instance, CameraFor(instance, step)); break;
                case ScareStepKind.ShowScareHead: yield return ShowScareHead(instance, step); break;
                case ScareStepKind.AllPassengersReact: yield return AllPassengersReact(instance, step); break;
                case ScareStepKind.CabinLightsOff: LightsOff(instance, step); break;
                case ScareStepKind.Blackout: yield return Blackout(instance, step); break;
                case ScareStepKind.Wait: yield return Wait(step.duration); break;
                case ScareStepKind.HandsOverCamera: yield return HandsOverCamera(instance, step); break;
                default:
                    Log.Warn(LogCat.Scare, $"{instance.Definition.id}: unknown step kind {step.kind}");
                    break;
            }
            instance.ActiveSteps--;
        }

        // ------------------------------------------------------------------ steps

        static IEnumerator Wait(float seconds) {
            if (seconds > 0f) {
                yield return new WaitForSeconds(seconds);
            }
        }

        IEnumerator PlaySound(Instance instance, ScareStep step) {
            if (string.IsNullOrEmpty(step.soundId)) {
                yield break;
            }
            IAudioService audio = shift.Game.Audio;
            Transform at = Anchor(instance, step);
            SoundHandle handle = at != null ? audio.PlayAt(step.soundId, at.position) : audio.Play(step.soundId);
            if (handle.IsNone) {
                yield break;
            }
            instance.Sounds.Add(handle);
            if (Reduced) {
                audio.SetVolume(handle, reducedVolume);
            }
            if (step.duration > 0f) {
                yield return Wait(step.duration);
                audio.Stop(handle);
            }
        }

        IEnumerator ShowOverlay(Instance instance, ScareStep step) {
            ScareOverlayState state = shift.ScareOverlay;
            instance.Overlay = true;
            state.Overlay = step.overlay != null ? (Texture)step.overlay : defaultOverlay;
            yield return Flash(step, value => state.OverlayAlpha = value);
            state.OverlayAlpha = 0f;
        }

        IEnumerator CctvStatic(Instance instance, ScareStep step) {
            instance.Static = true;
            ScareOverlayState state = shift.ScareOverlay;
            yield return Flash(step, value => state.StaticAlpha = value);
            state.StaticAlpha = 0f;
        }

        // Full: on at once at the step's intensity for its duration. Reduced: a fade in and out,
        // never above reducedMaxAlpha.
        IEnumerator Flash(ScareStep step, Action<float> set) {
            float target = Mathf.Clamp01(step.intensity);
            if (!Reduced) {
                set(target);
                yield return Wait(step.duration);
                yield break;
            }
            target = Mathf.Min(target, reducedMaxAlpha);
            float fade = Mathf.Min(reducedFadeSeconds, step.duration * 0.5f);
            float hold = step.duration - fade * 2f;
            yield return Ramp(0f, target, fade, set);
            yield return Wait(hold);
            yield return Ramp(target, 0f, fade, set);
        }

        static IEnumerator Ramp(float from, float to, float seconds, Action<float> set) {
            for (float t = 0f; t < seconds; t += Time.deltaTime) {
                set(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            set(to);
        }

        IEnumerator CameraShake(Instance instance, ScareStep step) {
            if (Reduced || shift.Shake == null) {
                yield break;
            }
            shift.Shake.SetShake(instance, Mathf.Clamp01(step.intensity));
            yield return Wait(step.duration);
            shift.Shake.SetShake(instance, 0f);
        }

        void FlickerLights(Instance instance, ScareStep step) {
            if (shift.CabinLights != null) {
                shift.CabinLights.Flicker(instance, step.duration);
            }
        }

        void LightsOff(Instance instance, ScareStep step) {
            if (shift.CabinLights != null) {
                shift.CabinLights.Off(instance, step.duration);
            }
        }

        IEnumerator LockInput(Instance instance, ScareStep step) {
            if (!instance.LockedInput) {
                instance.LockedInput = true;
                instance.InputBefore = shift.Game.Input.Context;
            }
            shift.Game.Input.SetContext(InputContext.Cinematic);
            yield return Wait(step.duration);
            RestoreInput(instance);
        }

        // −1 = the home view (the driver's, or the on-foot camera)
        void SetView(Instance instance, int index) {
            CCTVSystem cctv = shift.Cctv;
            if (cctv == null || index >= cctv.Cameras.Count) {
                return;
            }
            if (!instance.ChangedView) {
                instance.ChangedView = true;
                instance.ViewBefore = cctv.ActiveIndex;
            }
            instance.ViewSet = index;
            cctv.Show(index);
        }

        // CutToCctv: param "1".."3", else the scare's camera, else the one on screen, else CAM 1
        int CameraFor(Instance instance, ScareStep step) {
            int n;
            if (!string.IsNullOrEmpty(step.param) && int.TryParse(step.param, out n)) {
                return n - 1;
            }
            if (instance.Context.CctvIndex >= 0) {
                return instance.Context.CctvIndex;
            }
            return shift.Cctv != null && shift.Cctv.ActiveIndex >= 0 ? shift.Cctv.ActiveIndex : 0;
        }

        Transform Anchor(Instance instance, ScareStep step) {
            if (string.IsNullOrEmpty(step.anchor) || shift.ScareAnchors == null) {
                return null;
            }
            int camera = instance.Context.CctvIndex >= 0 ? instance.Context.CctvIndex : shift.Cctv.ActiveIndex;
            return shift.ScareAnchors.Resolve(step.anchor, camera);
        }

        // The head faces whichever camera is rendering; "lookAt" turns the driver's head to it
        IEnumerator ShowScareHead(Instance instance, ScareStep step) {
            Transform anchor = Anchor(instance, step);
            if (anchor == null || scareHeadPrefab == null) {
                Log.Warn(LogCat.Scare, $"{instance.Definition.id}: no scare head or anchor '{step.anchor}'");
                yield break;
            }
            // The prefab is already on the ScareFx layer, which collides with nothing (§4.16)
            GameObject head = Instantiate(scareHeadPrefab, anchor.position, anchor.rotation, anchor);
            instance.Heads.Add(head);
            Camera viewer = shift.Cctv != null ? shift.Cctv.ActiveCamera : null;
            if (viewer != null) {
                Vector3 toViewer = viewer.transform.position - head.transform.position;
                if (toViewer.sqrMagnitude > 0.0001f) {
                    head.transform.rotation = Quaternion.LookRotation(toViewer, anchor.up);
                }
            }
            if (step.param == "lookAt") {
                LookAt(instance, head.transform.position);
            }
            yield return Wait(step.duration);
            if (head != null) {
                instance.Heads.Remove(head);
                Destroy(head);
            }
        }

        void LookAt(Instance instance, Vector3 world) {
            DriverLook look = shift.DriverLook;
            if (look == null || look.transform.parent == null) {
                return;
            }
            if (!instance.Looked) {
                instance.Looked = true;
                instance.YawBefore = look.Yaw;
                instance.PitchBefore = look.Pitch;
            }
            Vector3 local = look.transform.parent.InverseTransformDirection(world - look.transform.position);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            look.SetLook(yaw, pitch);
        }

        IEnumerator AllPassengersReact(Instance instance, ScareStep step) {
            ReactionId reaction = ReactionId.TurnToCamera;
            if (!string.IsNullOrEmpty(step.param)) {
                ReactionId parsed;
                if (Enum.TryParse(step.param, out parsed)) {
                    reaction = parsed;
                }
            }
            Camera camera = shift.Cctv != null ? shift.Cctv.ActiveCamera : null;
            IReadOnlyList<RiderRecord> aboard = shift.Riders.Aboard;
            for (int i = 0; i < aboard.Count; i++) {
                Passenger rider = aboard[i].Passenger;
                if (rider == null || rider.View == null) {
                    continue;
                }
                rider.View.PlayReaction(reaction);
                if (reaction == ReactionId.TurnToCamera && camera != null) {
                    rider.View.SetLookAt(camera.transform, 1f);
                    instance.Turned.Add(rider);
                }
            }
            yield return Wait(step.duration);
            RestoreTurned(instance);
        }

        IEnumerator Blackout(Instance instance, ScareStep step) {
            ScreenFade fade = shift.Fade;
            if (!instance.Blackout) {
                instance.Blackout = true;
                instance.FadeBefore = fade.Alpha;
            }
            if (step.duration > 0f) {
                yield return fade.FadeTo(1f, step.duration);
            }else {
                fade.Set(1f);
            }
        }

        IEnumerator HandsOverCamera(Instance instance, ScareStep step) {
            instance.Hands = true;
            ScareOverlayState state = shift.ScareOverlay;
            yield return Ramp(0f, 1f, step.duration, value => state.HandsProgress = value);
        }

        // ---------------------------------------------------------------- restore

        void Finish(Instance instance) {
            if (!playing.Remove(instance)) {
                return;
            }
            bool kill = instance.Definition.tier == ScareTier.Kill;
            for (int i = 0; i < instance.Heads.Count; i++) {
                if (instance.Heads[i] != null) {
                    Destroy(instance.Heads[i]);
                }
            }
            instance.Heads.Clear();
            RestoreTurned(instance);
            if (shift.Shake != null) {
                shift.Shake.SetShake(instance, 0f);
            }
            if (shift.CabinLights != null) {
                shift.CabinLights.Stop(instance);
            }
            ScareOverlayState state = shift.ScareOverlay;
            if (instance.Overlay) {
                state.OverlayAlpha = 0f;
                state.Overlay = null;
            }
            if (instance.Static) {
                state.StaticAlpha = 0f;
            }
            if (instance.Hands) {
                state.HandsProgress = 0f;
            }
            if (instance.Looked && shift.DriverLook != null) {
                shift.DriverLook.SetLook(instance.YawBefore, instance.PitchBefore);
            }
            // A kill scare hands the black screen, the view and the input to the death (§2.14)
            if (kill) {
                return;
            }
            if (instance.Blackout) {
                shift.Fade.Set(instance.FadeBefore);
            }
            RestoreInput(instance);
            if (instance.ChangedView && shift.Cctv != null && shift.Cctv.ActiveIndex == instance.ViewSet) {
                shift.Cctv.Show(instance.ViewBefore);
            }
        }

        // Only while the night still drives: a death has set its own context by now
        void RestoreInput(Instance instance) {
            if (!instance.LockedInput) {
                return;
            }
            instance.LockedInput = false;
            if (shift.Game.Input.Context == InputContext.Cinematic && shift.Director.State == ShiftState.Driving) {
                shift.Game.Input.SetContext(instance.InputBefore);
            }
        }

        static void RestoreTurned(Instance instance) {
            for (int i = 0; i < instance.Turned.Count; i++) {
                Passenger rider = instance.Turned[i];
                if (rider != null && rider.View != null) {
                    rider.View.SetLookAt(null, 0f);
                }
            }
            instance.Turned.Clear();
        }
    }
}
