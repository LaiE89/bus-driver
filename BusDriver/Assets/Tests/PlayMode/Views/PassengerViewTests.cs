using System;
using System.Collections;
using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BusDriver.Tests.PlayMode.Views {
    // T-M3-01: every TellId changes something visible on the greybox view (a transform's position,
    // rotation, scale or active state, or the renderers blinking), or is a documented no-op (None).
    public class PassengerViewTests {
        const string ViewPath = "Assets/Generated/Prefabs/Views/View_GreyboxPassenger.prefab";
        const float Tolerance = 1e-4f;

        GreyboxPassengerView view;
        RendererFlicker flicker;
        GameObject target;

        [SetUp]
        public void SetUp() {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath);
            Assert.IsNotNull(prefab, ViewPath + " is missing; run Build All");
            view = UnityEngine.Object.Instantiate(prefab).GetComponent<GreyboxPassengerView>();
#endif
            Assert.IsNotNull(view, "the greybox view prefab has no GreyboxPassengerView");
            view.Configure(null);
            flicker = ViewFactory.AttachFlicker(view, 7);
            // A still baseline, so only the tell under test moves anything
            view.SetTell(TellId.Stillness, 1f);
            target = new GameObject("Look Target");
            target.transform.position = new Vector3(3f, 1.2f, 2f);
        }

        [TearDown]
        public void TearDown() {
            if (view != null) {
                UnityEngine.Object.Destroy(view.gameObject);
            }
            if (target != null) {
                UnityEngine.Object.Destroy(target);
            }
        }

        struct Pose {
            public bool Active;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        List<Pose> Snapshot() {
            List<Pose> poses = new List<Pose>();
            foreach (Transform node in view.GetComponentsInChildren<Transform>(true)) {
                poses.Add(new Pose {
                    Active = node.gameObject.activeInHierarchy,
                    Position = node.localPosition,
                    Rotation = node.localRotation,
                    Scale = node.localScale,
                });
            }
            return poses;
        }

        static bool Differs(List<Pose> a, List<Pose> b) {
            for (int i = 0; i < a.Count; i++) {
                if (a[i].Active != b[i].Active
                    || (a[i].Position - b[i].Position).sqrMagnitude > Tolerance * Tolerance
                    || Quaternion.Angle(a[i].Rotation, b[i].Rotation) > 0.05f
                    || (a[i].Scale - b[i].Scale).sqrMagnitude > Tolerance * Tolerance) {
                    return true;
                }
            }
            return false;
        }

        static IEnumerator Frames(float seconds) {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator EveryTell_ChangesSomethingVisible() {
            List<string> unchanged = new List<string>();
            foreach (TellId tell in (TellId[])Enum.GetValues(typeof(TellId))) {
                switch (tell) {
                    case TellId.None:
                        // Documented no-op
                        continue;
                    case TellId.Stillness:
                        yield return StillnessStopsIdleMotion(unchanged);
                        continue;
                    case TellId.MimicFlicker:
                    case TellId.MimicReveal:
                        yield return RenderersBlink(tell, unchanged);
                        continue;
                }
                // Tells that aim somewhere need a target
                view.SetLookAt(target.transform, 0f);
                yield return Frames(0.1f);
                List<Pose> before = Snapshot();
                view.SetTell(tell, 1f);
                yield return Frames(tell == TellId.HeadTrack ? 1f : 0.3f);
                if (!Differs(before, Snapshot())) {
                    unchanged.Add(tell.ToString());
                }
                view.SetTell(tell, 0f);
                view.SetLookAt(null, 0f);
                yield return Frames(tell == TellId.HeadTrack ? 5f : 0.1f);
            }
            Assert.IsEmpty(unchanged, "tells with no visible change: " + string.Join(", ", unchanged));
        }

        IEnumerator StillnessStopsIdleMotion(List<string> unchanged) {
            view.SetTell(TellId.Stillness, 0f);
            List<Pose> start = Snapshot();
            bool movedWhileIdle = false;
            for (float t = 0f; t < 1.5f && !movedWhileIdle; t += Time.unscaledDeltaTime) {
                yield return null;
                movedWhileIdle = Differs(start, Snapshot());
            }
            view.SetTell(TellId.Stillness, 1f);
            yield return null;
            List<Pose> still = Snapshot();
            yield return Frames(0.5f);
            bool movedWhileStill = Differs(still, Snapshot());
            if (!movedWhileIdle || movedWhileStill) {
                unchanged.Add($"Stillness (idle moved {movedWhileIdle}, still moved {movedWhileStill})");
            }
        }

        IEnumerator RenderersBlink(TellId tell, List<string> unchanged) {
            view.SetTell(tell, 1f);
            bool blinked = false;
            // MimicFlicker at full intensity blinks every 2 s; the reveal strobes at 12 Hz
            float until = Time.realtimeSinceStartup + 3f;
            while (!blinked && Time.realtimeSinceStartup < until) {
                yield return null;
                foreach (Renderer part in view.GetComponentsInChildren<Renderer>()) {
                    if (!part.enabled) {
                        blinked = true;
                        break;
                    }
                }
            }
            view.SetTell(tell, 0f);
            yield return Frames(0.2f);
            if (!blinked) {
                unchanged.Add(tell.ToString());
            }
            Assert.IsFalse(flicker.IsBlinkedOff, "the renderers stayed off after the tell ended");
        }

        [UnityTest]
        public IEnumerator Pose_MovesTheHead() {
            Transform head = view.Head;
            view.SetPose(PassengerPose.Standing);
            yield return null;
            float standing = head.localPosition.y;
            view.SetPose(PassengerPose.Seated);
            yield return null;
            Assert.AreEqual(1.62f, standing, 0.001f);
            Assert.AreEqual(0.94f, head.localPosition.y, 0.001f);
        }

        [UnityTest]
        public IEnumerator SetVisible_HidesEveryRenderer_AndLayersApply() {
            view.SetVisible(false);
            yield return null;
            foreach (Renderer part in view.GetComponentsInChildren<Renderer>(true)) {
                Assert.IsFalse(part.enabled, part.name + " is still drawn");
            }
            view.SetVisible(true);
            view.SetRenderLayer(17);
            yield return null;
            foreach (Transform node in view.GetComponentsInChildren<Transform>(true)) {
                Assert.AreEqual(17, node.gameObject.layer, node.name + " kept its layer");
            }
        }
    }
}
