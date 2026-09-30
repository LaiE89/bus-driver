using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Editor.Builders;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BusDriver.Tests.EditMode.Builders {
    // The generated bus keeps the MVP's geometry exactly (T-M1-14, Appendix A.2). Numbers are
    // spelled out here from the appendix, not taken from the builder.
    public class BusPrefabTests {
        const float Eps = 0.001f;
        GameObject bus;
        GameObject rig;

        [OneTimeSetUp]
        public void Load() {
            bus = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Generated/Prefabs/Bus.prefab");
            rig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Generated/Prefabs/OnFootRig.prefab");
            Assert.IsNotNull(bus, "no Bus.prefab; run Build All");
            Assert.IsNotNull(rig, "no OnFootRig.prefab; run Build All");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Generated/Prefabs/FallCamera.prefab"), "no FallCamera.prefab");
        }

        static Vector3 Local(GameObject root, Transform t) {
            return root.transform.InverseTransformPoint(t.position);
        }

        static void AssertNear(Vector3 expected, Vector3 actual, string what) {
            Assert.Less(Vector3.Distance(expected, actual), Eps, $"{what}: expected {expected}, got {actual}");
        }

        static Transform Find(GameObject root, string name) {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) {
                if (t.name == name) {
                    return t;
                }
            }
            Assert.Fail("no " + name + " under " + root.name);
            return null;
        }

        [Test]
        public void ThirtySixSeatsAtTheLegacyPositions() {
            BusSeat[] seats = bus.GetComponentsInChildren<BusSeat>(true);
            Assert.AreEqual(36, seats.Length);
            string[] columns = { "L2", "L1", "R1", "R2" };
            float[] xs = { -1f, -0.6f, 0.6f, 1f };
            for (int row = 1; row <= 9; row++) {
                for (int c = 0; c < 4; c++) {
                    Transform seat = Find(bus, $"Anchor_Seat_R{row}_{columns[c]}");
                    Assert.IsNotNull(seat.GetComponent<BusSeat>());
                    // R1 is the front row at z 2.8; rows are 1 m apart back to R9 at −5.2
                    AssertNear(new Vector3(xs[c], 1.06f, 2.8f - (row - 1)), Local(bus, seat), seat.name);
                    Assert.Greater(Vector3.Dot(seat.forward, bus.transform.forward), 0.999f, seat.name + " faces forward");
                }
            }
            // The cabin lists them rear to front, as the MVP did
            Object[] listed = SerializedSeats();
            Assert.AreEqual(36, listed.Length);
            Assert.AreEqual("Anchor_Seat_R9_L2", ((Component)listed[0]).name);
            Assert.AreEqual("Anchor_Seat_R1_R2", ((Component)listed[35]).name);
        }

        Object[] SerializedSeats() {
            SerializedProperty property = new SerializedObject(bus.GetComponent<BusCabin>()).FindProperty("seats");
            List<Object> seats = new List<Object>();
            for (int i = 0; i < property.arraySize; i++) {
                seats.Add(property.GetArrayElementAtIndex(i).objectReferenceValue);
            }
            return seats.ToArray();
        }

        [Test]
        public void CameraPoses() {
            Transform head = Find(bus, "Anchor_DriverHead");
            AssertNear(new Vector3(-0.7f, 1.9f, 4.6f), Local(bus, head), "Anchor_DriverHead");
            Camera driver = Find(bus, "DriverCamera").GetComponent<Camera>();
            AssertNear(new Vector3(-0.7f, 1.9f, 4.6f), Local(bus, driver.transform), "driver camera");
            Assert.AreEqual(70f, driver.fieldOfView, Eps);
            Assert.IsNotNull(head.GetComponentInChildren<AudioListener>(true), "the ears ride on the driver head");

            string[] anchors = { "Anchor_CCTV_Front", "Anchor_CCTV_Mid", "Anchor_CCTV_Rear" };
            Vector3[] positions = { new Vector3(0.3f, 3.2f, 5.3f), new Vector3(0f, 3.2f, 0.5f), new Vector3(0f, 3.2f, -5.7f) };
            Vector3[] eulers = { new Vector3(22f, 180f, 0f), new Vector3(28f, 180f, 0f), new Vector3(22f, 0f, 0f) };
            CctvCamera[] cameras = bus.GetComponentsInChildren<CctvCamera>(true);
            Assert.AreEqual(3, cameras.Length);
            for (int i = 0; i < 3; i++) {
                Transform anchor = Find(bus, anchors[i]);
                CctvCamera cctv = anchor.GetComponentInChildren<CctvCamera>(true);
                Assert.IsNotNull(cctv, anchors[i] + " has no CctvCamera");
                AssertNear(positions[i], Local(bus, cctv.transform), anchors[i]);
                Assert.Less(Quaternion.Angle(Quaternion.Euler(eulers[i]), Quaternion.Inverse(bus.transform.rotation) * cctv.transform.rotation), 0.01f, anchors[i] + " rotation");
                Camera cam = cctv.GetComponent<Camera>();
                Assert.AreEqual(95f, cam.fieldOfView, Eps);
                Assert.IsFalse(cam.enabled, "CCTV cameras start off");
                Assert.AreEqual(7f, cctv.ObserveRange, Eps);
                Assert.AreEqual(i + 1, cctv.HideLayerIndex);
                Assert.AreEqual(0, cam.cullingMask & Layers.Mask(15 + i + 1), anchors[i] + " must not render its MimicHideCam layer");
            }
        }

        [Test]
        public void AxlesAndTheDoorOpening() {
            WheelCollider[] wheels = bus.GetComponentsInChildren<WheelCollider>(true);
            Assert.AreEqual(4, wheels.Length);
            foreach (WheelCollider wheel in wheels) {
                Vector3 local = Local(bus, wheel.transform);
                bool front = wheel.name.StartsWith("WC_F");
                Assert.AreEqual(front ? 3.3f : -2.7f, local.z, Eps, wheel.name);
                Assert.AreEqual(1.05f, Mathf.Abs(local.x), Eps, wheel.name);
            }
            // The door gap in the kerb-side body panels (unit cubes, scaled)
            Transform rear = Find(bus, "Right Lower Rear");
            Transform front2 = Find(bus, "Right Lower Front");
            Assert.AreEqual(4.1f, Local(bus, rear).z + rear.lossyScale.z * 0.5f, Eps);
            Assert.AreEqual(5.5f, Local(bus, front2).z - front2.lossyScale.z * 0.5f, Eps);
            BoxCollider blocker = Find(bus, "Doorway Blocker").GetComponent<BoxCollider>();
            Vector3 center = Local(bus, blocker.transform);
            Assert.AreEqual(4.1f, center.z - blocker.size.z * 0.5f, Eps);
            Assert.AreEqual(5.5f, center.z + blocker.size.z * 0.5f, Eps);
            AssertNear(new Vector3(1f, 0.55f, 4.8f), Local(bus, Find(bus, "Anchor_DoorStep")), "Anchor_DoorStep");
            AssertNear(new Vector3(0f, 0.55f, 4.8f), Local(bus, Find(bus, "Anchor_AisleAtDoor")), "Anchor_AisleAtDoor");
            AssertNear(new Vector3(-0.9f, 1.2f, 6.1f), Local(bus, Find(bus, "Anchor_Headlight_L")), "Anchor_Headlight_L");
            AssertNear(new Vector3(0.9f, 1.2f, 6.1f), Local(bus, Find(bus, "Anchor_Headlight_R")), "Anchor_Headlight_R");
        }

        [Test]
        public void TheAvatarIsSeenByCctvOnly() {
            Transform seated = Find(bus, "DriverAvatar");
            Transform standing = Find(rig, "Avatar");
            foreach (Transform avatar in new[] { seated, standing }) {
                Assert.IsNotNull(avatar.GetComponent<PlayerAvatarViewBase>(), avatar.name + " has no avatar view");
                Renderer[] renderers = avatar.GetComponentsInChildren<Renderer>(true);
                Assert.Greater(renderers.Length, 0);
                foreach (Renderer renderer in renderers) {
                    Assert.AreEqual(19, renderer.gameObject.layer, renderer.name + " is not on PlayerAvatar (19)");
                }
            }
            int avatarBit = 1 << 19;
            Assert.AreEqual(0, Find(bus, "DriverCamera").GetComponent<Camera>().cullingMask & avatarBit, "the driver camera must cull the avatar");
            Assert.AreEqual(0, Find(rig, "OnFootCamera").GetComponent<Camera>().cullingMask & avatarBit, "the on-foot camera must cull the avatar");
            foreach (CctvCamera cctv in bus.GetComponentsInChildren<CctvCamera>(true)) {
                Assert.AreNotEqual(0, cctv.GetComponent<Camera>().cullingMask & avatarBit, cctv.name + " must render the avatar");
            }
        }

        [Test]
        public void LogicAndViewSplit() {
            Assert.AreEqual(Layers.Bus, bus.layer);
            Assert.IsNotNull(bus.GetComponent<Rigidbody>());
            BoxCollider hull = bus.GetComponent<BoxCollider>();
            Assert.IsFalse(hull.isTrigger);
            // The view child holds renderers only (§4.14)
            BusViewBase view = bus.GetComponentInChildren<BusViewBase>(true);
            Assert.IsNotNull(view, "no BusViewBase");
            Assert.AreEqual("View", view.name);
            CollectionAssert.IsEmpty(view.GetComponentsInChildren<Collider>(true), "the view has colliders");
            foreach (DashScreen screen in System.Enum.GetValues(typeof(DashScreen))) {
                Transform anchor = view.DashAnchor(screen);
                Assert.IsNotNull(anchor, screen + " dash anchor");
                Assert.AreEqual("Anchor_Dash_" + screen, anchor.name);
            }
            foreach (Collider collider in Find(bus, "InteriorColliders").GetComponentsInChildren<Collider>(true)) {
                Assert.AreEqual(Layers.BusInterior, collider.gameObject.layer, collider.name);
            }
            Assert.IsFalse(Find(bus, "InteriorColliders").gameObject.activeSelf, "interior colliders start off");
            Collider[] occluders = Find(bus, "OccluderShell").GetComponentsInChildren<Collider>(true);
            Assert.Greater(occluders.Length, 0);
            foreach (Collider occluder in occluders) {
                Assert.IsTrue(occluder.isTrigger, occluder.name);
                Assert.AreEqual(Layers.Occluder, occluder.gameObject.layer, occluder.name);
            }
            foreach (string name in new[] { "Anchor_Scare_DriverShoulder", "Anchor_Scare_DriverWindow", "Anchor_Scare_CctvLens1",
                "Anchor_Scare_CctvLens2", "Anchor_Scare_CctvLens3", "Anchor_Scare_CabinCenter" }) {
                Find(bus, name);
            }
            AssertNear(new Vector3(-0.35f, 1.9f, 4.35f), Local(bus, Find(bus, "Anchor_Scare_DriverShoulder")), "DriverShoulder");
            SerializedProperty locks = new SerializedObject(bus.GetComponent<BusController>()).FindProperty("initialLocks");
            Assert.AreEqual((int)DriveLock.Scripted, locks.intValue, "the bus prefab starts with DriveLock.Scripted");
        }
    }
}
