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
    // The generated bus matches the artist's model (Appendix A.2, D108). Numbers are spelled out
    // here from the appendix, not taken from the builder.
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

        struct SeatSpec {
            public string Name;
            public Vector3 Position;
            public float Yaw;
            public SeatSpec(string name, float x, float y, float z, float yaw) {
                Name = name;
                Position = new Vector3(x, y, z);
                Yaw = yaw;
            }
        }

        // Appendix A.2, in the order the cabin lists them: rear row first
        static readonly SeatSpec[] Seats = {
            new SeatSpec("R8_L2", -1.069f, 1.382f, -4.713f, 0f),
            new SeatSpec("R8_L1", -0.533f, 1.382f, -4.713f, 0f),
            new SeatSpec("R8_C", 0f, 1.382f, -4.713f, 0f),
            new SeatSpec("R8_R1", 0.533f, 1.382f, -4.713f, 0f),
            new SeatSpec("R8_R2", 1.069f, 1.382f, -4.713f, 0f),
            new SeatSpec("R7_L2", -0.913f, 1.36f, -3.406f, 90f),
            new SeatSpec("R7_L1", -0.913f, 1.36f, -2.806f, 90f),
            new SeatSpec("R7_R1", 0.913f, 1.36f, -2.8f, -90f),
            new SeatSpec("R7_R2", 0.913f, 1.36f, -3.4f, -90f),
            new SeatSpec("R6_L2", -1.062f, 1.382f, -2.063f, 0f),
            new SeatSpec("R6_L1", -0.516f, 1.382f, -2.063f, 0f),
            new SeatSpec("R6_R1", 0.516f, 1.382f, -2.063f, 0f),
            new SeatSpec("R6_R2", 1.062f, 1.382f, -2.063f, 0f),
            new SeatSpec("R5_L2", -1.062f, 1.382f, -1.113f, 0f),
            new SeatSpec("R5_L1", -0.516f, 1.382f, -1.113f, 0f),
            new SeatSpec("R5_R1", 0.516f, 1.382f, -1.113f, 0f),
            new SeatSpec("R5_R2", 1.062f, 1.382f, -1.113f, 0f),
            new SeatSpec("R4_L2", -1.062f, 1.382f, -0.113f, 0f),
            new SeatSpec("R4_L1", -0.516f, 1.382f, -0.113f, 0f),
            new SeatSpec("R4_R1", 0.516f, 1.382f, -0.113f, 0f),
            new SeatSpec("R4_R2", 1.062f, 1.382f, -0.113f, 0f),
            new SeatSpec("R3_L2", -1.062f, 1.382f, 0.887f, 0f),
            new SeatSpec("R3_L1", -0.516f, 1.382f, 0.887f, 0f),
            new SeatSpec("R3_R1", 0.516f, 1.382f, 0.887f, 0f),
            new SeatSpec("R3_R2", 1.062f, 1.382f, 0.887f, 0f),
            new SeatSpec("R2_L2", -1.062f, 1.382f, 1.887f, 0f),
            new SeatSpec("R2_L1", -0.516f, 1.382f, 1.887f, 0f),
            new SeatSpec("R2_R1", 0.516f, 1.382f, 1.887f, 0f),
            new SeatSpec("R2_R2", 1.062f, 1.382f, 1.887f, 0f),
            new SeatSpec("R1_L2", -0.913f, 1.352f, 2.997f, 90f),
            new SeatSpec("R1_L1", -0.913f, 1.352f, 3.597f, 90f),
            new SeatSpec("R1_R1", 0.913f, 1.352f, 3.603f, -90f),
            new SeatSpec("R1_R2", 0.913f, 1.352f, 3.003f, -90f),
        };

        [Test]
        public void ThirtyThreeSeatsOnTheModelsCushions() {
            BusSeat[] seats = bus.GetComponentsInChildren<BusSeat>(true);
            Assert.AreEqual(33, seats.Length);
            Object[] listed = SerializedSeats();
            Assert.AreEqual(33, listed.Length);
            for (int i = 0; i < Seats.Length; i++) {
                SeatSpec spec = Seats[i];
                Transform seat = Find(bus, "Anchor_Seat_" + spec.Name);
                BusSeat busSeat = seat.GetComponent<BusSeat>();
                Assert.IsNotNull(busSeat);
                AssertNear(spec.Position, Local(bus, seat), seat.name);
                Vector3 facing = Quaternion.Euler(0f, spec.Yaw, 0f) * Vector3.forward;
                Assert.Greater(Vector3.Dot(bus.transform.InverseTransformDirection(seat.forward), facing), 0.999f, seat.name + " faces the right way");
                Assert.AreEqual(int.Parse(spec.Name.Substring(1, 1)), busSeat.Row, seat.name + " row");
                // The cabin lists them rear to front, as the MVP did
                Assert.AreSame(busSeat, listed[i], "seat " + i + " in the cabin's list");
            }
            Assert.AreEqual(4, Find(bus, "Anchor_Seat_R8_C").GetComponent<BusSeat>().Column, "C is column 4");
            Assert.AreEqual(2, Find(bus, "Anchor_Seat_R8_R1").GetComponent<BusSeat>().Column, "R1 keeps column 2");
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
            AssertNear(new Vector3(-0.8f, 2.1f, 4.6f), Local(bus, head), "Anchor_DriverHead");
            Camera driver = Find(bus, "DriverCamera").GetComponent<Camera>();
            AssertNear(new Vector3(-0.8f, 2.1f, 4.6f), Local(bus, driver.transform), "driver camera");
            Assert.AreEqual(70f, driver.fieldOfView, Eps);
            Assert.IsNotNull(head.GetComponentInChildren<AudioListener>(true), "the ears ride on the driver head");

            string[] anchors = { "Anchor_CCTV_Front", "Anchor_CCTV_Mid", "Anchor_CCTV_Rear" };
            Vector3[] positions = { new Vector3(0.25f, 3.06f, 5.24f), new Vector3(0f, 3.08f, 0.5f), new Vector3(0f, 3.08f, -5.08f) };
            Vector3[] eulers = { new Vector3(30f, 180f, 0f), new Vector3(34f, 180f, 0f), new Vector3(30f, 0f, 0f) };
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
                Assert.AreEqual(front ? 3f : -2.9f, local.z, Eps, wheel.name);
                Assert.AreEqual(front ? 1.3f : 1.13f, Mathf.Abs(local.x), Eps, wheel.name);
            }
            BoxCollider blocker = Find(bus, "Doorway Blocker").GetComponent<BoxCollider>();
            Vector3 center = Local(bus, blocker.transform);
            Assert.AreEqual(4.2f, center.z - blocker.size.z * 0.5f, Eps);
            Assert.AreEqual(5.4f, center.z + blocker.size.z * 0.5f, Eps);
            AssertNear(new Vector3(1.05f, 0.5f, 4.8f), Local(bus, Find(bus, "Anchor_DoorStep")), "Anchor_DoorStep");
            AssertNear(new Vector3(0f, 0.8f, 4.8f), Local(bus, Find(bus, "Anchor_AisleAtDoor")), "Anchor_AisleAtDoor");
            AssertNear(new Vector3(-1f, 1.1f, 6f), Local(bus, Find(bus, "Anchor_Headlight_L")), "Anchor_Headlight_L");
            AssertNear(new Vector3(1f, 1.1f, 6f), Local(bus, Find(bus, "Anchor_Headlight_R")), "Anchor_Headlight_R");
            BoxCollider hull = bus.GetComponent<BoxCollider>();
            AssertNear(new Vector3(2.9f, 3.1f, 11.9f), hull.size, "hull size");
            AssertNear(new Vector3(0f, 1.95f, 0.06f), hull.center, "hull centre");
        }

        // With the artist's FBX in Assets/Art, the View is the rigged model (D108)
        [Test]
        public void TheArtViewDrivesTheModelsJoints() {
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Vehicles/Bus/SK_Bus_00.fbx") == null) {
                Assert.Ignore("no bus art; the greybox view is in use");
            }
            Transform view = bus.transform.Find("View");
            AnimatedBusView art = view.GetComponent<AnimatedBusView>();
            Assert.IsNotNull(art, "the View is an AnimatedBusView");
            SerializedObject so = new SerializedObject(art);
            string[] wheels = { "FL_rot", "FR_rot", "RL_rot", "RR_rot" };
            SerializedProperty bones = so.FindProperty("wheelBones");
            Assert.AreEqual(4, bones.arraySize);
            for (int i = 0; i < 4; i++) {
                Transform bone = (Transform)bones.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.IsNotNull(bone, wheels[i]);
                Assert.AreEqual(wheels[i], bone.name);
                Assert.IsTrue(bone.IsChildOf(view), wheels[i] + " belongs to the view");
            }
            // FL is on the driver's side
            Assert.Less(Local(bus, (Transform)bones.GetArrayElementAtIndex(0).objectReferenceValue).x, 0f, "FL_rot is on the left");
            Assert.AreEqual("steercolumn", ((Transform)so.FindProperty("steeringColumn").objectReferenceValue).name);
            SerializedProperty doors = so.FindProperty("doorLeaves");
            Assert.AreEqual(2, doors.arraySize);
            Assert.AreEqual(4.2f, Local(bus, (Transform)doors.GetArrayElementAtIndex(0).objectReferenceValue).z, 0.01f, "door1 hinges at z 4.2");
            Assert.AreEqual(5.4f, Local(bus, (Transform)doors.GetArrayElementAtIndex(1).objectReferenceValue).z, 0.01f, "door2 hinges at z 5.4");
            for (int i = 0; i < 4; i++) {
                Assert.IsNotNull(art.DashAnchor((DashScreen)i), (DashScreen)i + " anchor");
            }
            // Every skinned part that's left still has its bones; the chassis parts are baked
            foreach (SkinnedMeshRenderer skinned in view.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                foreach (Transform bone in skinned.bones) {
                    Assert.IsNotNull(bone, skinned.name + " lost a bone");
                }
            }
            Assert.Less(view.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, 60, "the static parts are baked");
            Assert.Greater(view.GetComponentsInChildren<MeshRenderer>(true).Length, 0, "the baked static meshes");
            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>(true)) {
                foreach (Material material in renderer.sharedMaterials) {
                    Assert.IsNotNull(material, renderer.name + " is missing a material");
                }
            }
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
