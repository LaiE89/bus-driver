using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Core.Util;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Monsters;
using BusDriver.Gameplay.Passengers;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static BusDriver.Editor.Builders.BuilderUtil;

namespace BusDriver.Editor.Builders {
    // BuildAll step 7 (§4.15): the logic prefabs and their greybox views. For now: Bus, OnFootRig
    // and FallCamera (T-M1-14), the HUD and Screens (UIPrefabBuilder, T-M1-16), the environment
    // kinds (EnvironmentPrefabBuilder, T-M2-06) and the MVP's two
    // pooled riders (T-M1-16, until the manifest and the view split replace them in M2–M3). The 12 m bus geometry, camera placements and seat layout are the
    // MVP's, unchanged (Appendix A.2). The logic root owns every collider, camera, light and
    // anchor; the View child owns only renderers (§4.14).
    public static class PrefabBuilder {
        public const string Folder = GeneratedRoot + "/Prefabs";
        public const string BusPath = Folder + "/Bus.prefab";
        public const string OnFootRigPath = Folder + "/OnFootRig.prefab";
        public const string FallCameraPath = Folder + "/FallCamera.prefab";
        public const string LegacyPassengerPath = Folder + "/Passenger.prefab";
        public const string LegacyWeepingAngelPath = Folder + "/WeepingAngel.prefab";
        public const string TuningPath = "Assets/Settings/BusTuning.asset";

        // Names the scene builders and tests look things up by
        public const string DriverHeadName = "Anchor_DriverHead";
        public const string DriverPivotName = "DriverCamPivot";
        public const string DriverCameraName = "DriverCamera";
        public const string EarsName = "Driver Ears";
        public const string DriverAvatarName = "DriverAvatar";
        public const string OnFootCameraName = "OnFootCamera";

        // Bus body; the root origin is at ground level under the middle of the bus (Appendix A.2)
        const float BusWidth = 2.55f;
        const float BusLength = 12f;
        const float BusHeight = 3f;
        const float HullBottom = 0.45f;
        const float FloorTop = HullBottom + 0.1f;
        const float Panel = 0.08f;
        public const float FrontAxleZ = 3.3f;
        public const float RearAxleZ = -2.7f;
        public const float TrackHalf = 1.05f;
        public const float DoorBack = 4.1f;
        public const float DoorFront = 5.5f;
        const float DoorZ = 4.8f;
        public const float SeatTop = 1.06f;
        // Row R1 is the front row (§2.6); rows are 1 m apart back to R9
        public const float FrontRowZ = 2.8f;
        public const int Rows = 9;
        public static readonly string[] SeatColumns = { "L2", "L1", "R1", "R2" };
        public static readonly float[] SeatColumnX = { -1f, -0.6f, 0.6f, 1f };
        public static readonly Vector3 DriverHeadPos = new Vector3(-0.7f, 1.9f, 4.6f);

        public struct CctvSpec {
            public string Anchor;
            public string CameraName;
            public string Label;
            public Vector3 Position;
            public Vector3 Euler;
        }

        // Appendix A.2; every CCTV camera has FOV 95. MVP labels, with MID added between them.
        public static readonly CctvSpec[] Cctv = {
            new CctvSpec { Anchor = "Anchor_CCTV_Front", CameraName = "CAM 1 Front", Label = "CAM 1  FRONT", Position = new Vector3(0.3f, 3.2f, 5.3f), Euler = new Vector3(22f, 180f, 0f) },
            new CctvSpec { Anchor = "Anchor_CCTV_Mid", CameraName = "CAM 2 Mid", Label = "CAM 2  MID", Position = new Vector3(0f, 3.2f, 0.5f), Euler = new Vector3(28f, 180f, 0f) },
            new CctvSpec { Anchor = "Anchor_CCTV_Rear", CameraName = "CAM 3 Rear", Label = "CAM 3  REAR", Position = new Vector3(0f, 3.2f, -5.7f), Euler = new Vector3(22f, 0f, 0f) },
        };
        public const float CctvFov = 95f;
        public const float CctvObserveRange = 7f;
        public const float DriverFov = 70f;

        static readonly Color FogColor = new Color(0.02f, 0.025f, 0.04f);

        [MenuItem("Tools/Bus Driver/Builders/Prefabs")]
        public static void Build() {
            EnsureFolder(Folder);
            BuildBus();
            BuildOnFootRig();
            BuildFallCamera();
            BuildLegacyRider<Passenger>(LegacyPassengerPath, "Passenger");
            BuildLegacyRider<WeepingAngel>(LegacyWeepingAngelPath, "WeepingAngel");
            UIPrefabBuilder.Build();
            EnvironmentPrefabBuilder.Build();
            AssetDatabase.SaveAssets();
        }

        static Material M(string name) {
            return MaterialLibraryBuilder.Get(name);
        }

        public static string SeatName(int row, int column) {
            return $"Anchor_Seat_R{row}_{SeatColumns[column]}";
        }

        public static float RowZ(int row) {
            return FrontRowZ - (row - 1);
        }

        // ================================================================== bus

        static void BuildBus() {
            BusTuning tuning = AssetDatabase.LoadAssetAtPath<BusTuning>(TuningPath);
            if (tuning == null) {
                tuning = ScriptableObject.CreateInstance<BusTuning>();
                AssetDatabase.CreateAsset(tuning, TuningPath);
            }

            GameObject bus = new GameObject("Bus");
            bus.layer = Layers.Bus;
            Transform root = bus.transform;

            Rigidbody rb = bus.AddComponent<Rigidbody>();
            rb.mass = tuning.mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // One hull collider. Interior and visual parts carry no colliders, they would
            // join the compound collider and distort the inertia tensor.
            BoxCollider hull = bus.AddComponent<BoxCollider>();
            hull.center = new Vector3(0f, HullBottom + BusHeight * 0.5f, 0f);
            hull.size = new Vector3(BusWidth, BusHeight, BusLength);
            hull.sharedMaterial = MaterialLibraryBuilder.HullPhysics();

            BusController controller = bus.AddComponent<BusController>();
            bus.AddComponent<CrashDetector>();
            BusInput input = bus.AddComponent<BusInput>();
            BusEngineSound engineSound = bus.AddComponent<BusEngineSound>();
            BusDoors doors = bus.AddComponent<BusDoors>();
            BusCabin cabin = bus.AddComponent<BusCabin>();

            GreyboxBusView view = BuildBusView(root, tuning);

            Transform com = Group("COM", root).transform;
            com.localPosition = new Vector3(0f, 0.9f, 0.2f);
            WheelCollider[] front;
            WheelCollider[] rear;
            BuildWheelColliders(root, tuning, out front, out rear);

            List<Object> seats = BuildSeats(root);
            Transform aisleAtDoor = Node(root, "Anchor_AisleAtDoor", new Vector3(0f, FloorTop, DoorZ));
            Transform doorStep = Node(root, "Anchor_DoorStep", new Vector3(1f, FloorTop, DoorZ));
            // Height is found with a raycast at run time: road or kerb
            Transform doorOutside = Node(root, "DoorOutside", new Vector3(1.9f, 0f, DoorZ));
            Transform standPoint = Node(root, "StandPoint", new Vector3(0.15f, FloorTop, 4.5f));
            DriverSeat driverSeat;
            GameObject interior = BuildInteriorColliders(root, out driverSeat);
            Transform passengerRoot = Group("Passengers", root).transform;

            Camera driverCamera;
            DriverLook look;
            BuildDriverHead(root, out driverCamera, out look);
            CCTVSystem cctv = BuildCctv(root, driverCamera);
            SetRef(look, "cctv", cctv);

            BuildLights(root);
            BuildScareAnchors(root);
            BuildOccluderShell(root);

            SetRef(controller, "tuning", tuning);
            SetRefArray(controller, "frontWheels", front);
            SetRefArray(controller, "rearWheels", rear);
            SetRef(controller, "centerOfMass", com);
            SetRef(controller, "view", view);
            SetInt(controller, "initialLocks", (int)DriveLock.Scripted);
            SetRef(input, "bus", controller);
            SetRef(engineSound, "bus", controller);
            SetRef(doors, "bus", controller);
            SetRef(doors, "view", view);
            SetRef(cabin, "bus", controller);
            SetRef(cabin, "doors", doors);
            SetRefArray(cabin, "seats", seats.ToArray());
            SetRef(cabin, "passengerRoot", passengerRoot);
            SetRef(cabin, "interiorColliders", interior);
            // Stops belong to the route scene: BusCabin.Init takes them from RouteSceneRoot
            SetRef(cabin, "driverSeat", driverSeat);
            SetRefArray(cabin, "initialPassengers", new Object[0]);
            SetRef(cabin, "aisleAtDoor", aisleAtDoor);
            SetRef(cabin, "doorStep", doorStep);
            SetRef(cabin, "doorOutside", doorOutside);
            SetRef(cabin, "standPoint", standPoint);

            SaveOrOverwritePrefab(bus, BusPath);
            Object.DestroyImmediate(bus);
        }

        // 36 seats on the seat surface, +Z the way a seated passenger faces. Created rear to front,
        // as the MVP did, so seat choice by index doesn't change.
        static List<Object> BuildSeats(Transform root) {
            Transform seatRoot = Group("Seats", root).transform;
            List<Object> seats = new List<Object>();
            for (int row = Rows; row >= 1; row--) {
                for (int column = 0; column < SeatColumns.Length; column++) {
                    Transform seat = Node(seatRoot, SeatName(row, column), new Vector3(SeatColumnX[column], SeatTop, RowZ(row)));
                    seats.Add(seat.gameObject.AddComponent<BusSeat>());
                }
            }
            return seats;
        }

        // Saved inactive and only switched on while the bus is frozen for walking, so these
        // never join the hull's compound collider or change the driving inertia tensor.
        // Unscaled empties with sized BoxColliders, floor top at 0.55.
        static GameObject BuildInteriorColliders(Transform root, out DriverSeat driverSeat) {
            Transform interior = Group("InteriorColliders", root).transform;
            InteriorBox(interior, "Floor", new Vector3(0f, 0.35f, 0f), new Vector3(BusWidth, 0.4f, BusLength));
            InteriorBox(interior, "Wall L", new Vector3(-1.29f, 1.95f, 0f), new Vector3(0.2f, 2.8f, BusLength));
            InteriorBox(interior, "Wall R Rear", new Vector3(1.29f, 1.95f, -0.95f), new Vector3(0.2f, 2.8f, 10.1f));
            InteriorBox(interior, "Wall R Front", new Vector3(1.29f, 1.95f, 5.75f), new Vector3(0.2f, 2.8f, 0.5f));
            // Separate so a later step can let the player off the bus by disabling it
            InteriorBox(interior, "Doorway Blocker", new Vector3(1.29f, 1.95f, DoorZ), new Vector3(0.2f, 2.8f, DoorFront - DoorBack));
            InteriorBox(interior, "Rear", new Vector3(0f, 1.95f, -5.95f), new Vector3(BusWidth, 2.8f, 0.1f));
            InteriorBox(interior, "Front Dash", new Vector3(0f, 1.95f, 5.6f), new Vector3(BusWidth, 2.8f, 0.8f));
            // One long box per side, an 0.8 m aisle with no gaps between rows to snag on
            InteriorBox(interior, "Bench L", new Vector3(-0.8175f, 1.175f, -1.425f), new Vector3(0.835f, 1.25f, 8.95f));
            InteriorBox(interior, "Bench R", new Vector3(0.8175f, 1.175f, -1.425f), new Vector3(0.835f, 1.25f, 8.95f));
            InteriorBox(interior, "Arch FL", new Vector3(-TrackHalf, 0.875f, FrontAxleZ), new Vector3(0.45f, 0.65f, 1.4f));
            InteriorBox(interior, "Arch FR", new Vector3(TrackHalf, 0.875f, FrontAxleZ), new Vector3(0.45f, 0.65f, 1.4f));
            driverSeat = InteriorBox(interior, "Driver Seat", new Vector3(-0.7f, 1.25f, 4.675f), new Vector3(0.6f, 1.4f, 1.05f)).AddComponent<DriverSeat>();
            SetLayerRecursively(interior.gameObject, Layers.BusInterior);
            interior.gameObject.SetActive(false);
            return interior.gameObject;
        }

        static GameObject InteriorBox(Transform parent, string name, Vector3 center, Vector3 size) {
            GameObject go = Group(name, parent);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        static void BuildWheelColliders(Transform root, BusTuning tuning, out WheelCollider[] front, out WheelCollider[] rear) {
            Transform wheels = Group("Wheels", root).transform;
            // The collider sits at the top of the suspension travel. Placed so the tyre
            // just touches y = 0 with the suspension resting at its target position.
            float colliderY = tuning.wheelRadius + tuning.suspensionDistance * (1f - tuning.suspensionTarget);
            front = new WheelCollider[2];
            rear = new WheelCollider[2];
            // Created FL, RL, FR, RR, the MVP's order
            for (int i = 0; i < 2; i++) {
                float x = i == 0 ? -TrackHalf : TrackHalf;
                string side = i == 0 ? "L" : "R";
                front[i] = CreateWheelCollider(wheels, "WC_F" + side, new Vector3(x, colliderY, FrontAxleZ), tuning);
                rear[i] = CreateWheelCollider(wheels, "WC_R" + side, new Vector3(x, colliderY, RearAxleZ), tuning);
            }
        }

        static WheelCollider CreateWheelCollider(Transform wheels, string name, Vector3 localPos, BusTuning tuning) {
            GameObject go = Group(name, wheels);
            go.layer = Layers.Bus;
            go.transform.localPosition = localPos;
            WheelCollider wheel = go.AddComponent<WheelCollider>();
            // BusController.ApplyTuning sets everything again at runtime, these keep the editor gizmos honest
            wheel.radius = tuning.wheelRadius;
            wheel.mass = tuning.wheelMass;
            wheel.suspensionDistance = tuning.suspensionDistance;
            JointSpring spring = wheel.suspensionSpring;
            spring.spring = tuning.suspensionSpring;
            spring.damper = tuning.suspensionDamper;
            spring.targetPosition = tuning.suspensionTarget;
            wheel.suspensionSpring = spring;
            return wheel;
        }

        // The seated eye point. The pivot turns and the camera rides on it; the ears carry the
        // one AudioListener (never on a camera, so switching views can't leave zero or two).
        static void BuildDriverHead(Transform root, out Camera driverCamera, out DriverLook look) {
            Transform head = Node(root, DriverHeadName, DriverHeadPos);
            GameObject pivot = Group(DriverPivotName, head);
            look = pivot.AddComponent<DriverLook>();
            driverCamera = CreateCamera(DriverCameraName, pivot.transform, Vector3.zero, Vector3.zero, DriverFov);
            driverCamera.tag = "MainCamera";
            driverCamera.cullingMask &= ~Layers.Mask(Layers.PlayerAvatar);
            Group(EarsName, head).AddComponent<AudioListener>();

            // The seated body, at the seat under the eye point (D49)
            Transform avatarRoot = Node(head, DriverAvatarName, new Vector3(0f, -0.85f, -0.05f));
            BuildAvatarView(avatarRoot, PassengerPose.Seated);
        }

        static CCTVSystem BuildCctv(Transform root, Camera homeCamera) {
            GameObject group = Group("CCTV", root);
            CCTVSystem cctv = group.AddComponent<CCTVSystem>();
            List<Object> cameras = new List<Object>();
            for (int i = 0; i < Cctv.Length; i++) {
                Transform anchor = Node(group.transform, Cctv[i].Anchor, Cctv[i].Position, Cctv[i].Euler);
                Camera cam = CreateCamera(Cctv[i].CameraName, anchor, Vector3.zero, Vector3.zero, CctvFov);
                cam.enabled = false;
                CctvCamera cctvCamera = cam.gameObject.AddComponent<CctvCamera>();
                SetString(cctvCamera, "label", Cctv[i].Label);
                SetFloat(cctvCamera, "observeRange", CctvObserveRange);
                SetInt(cctvCamera, "hideLayerIndex", i + 1);
                // Camera k never renders MimicHideCam k (§4.16); the avatar stays visible
                cam.cullingMask &= ~Layers.Mask(Layers.MimicHideCamBase + i + 1);
                cameras.Add(cctvCamera);
            }
            SetRef(cctv, "homeCamera", homeCamera);
            SetRefArray(cctv, "cameras", cameras.ToArray());
            return cctv;
        }

        static void BuildLights(Transform root) {
            float frontZ = BusLength * 0.5f + 0.1f;
            for (int i = 0; i < 2; i++) {
                Transform anchor = Node(root, i == 0 ? "Anchor_Headlight_L" : "Anchor_Headlight_R",
                    new Vector3(i == 0 ? -0.9f : 0.9f, 1.2f, frontZ), new Vector3(6f, 0f, 0f));
                Light headlight = anchor.gameObject.AddComponent<Light>();
                headlight.type = LightType.Spot;
                headlight.range = 60f;
                headlight.spotAngle = 55f;
                headlight.innerSpotAngle = 30f;
                headlight.intensity = 1500f;
                headlight.color = new Color(1f, 0.93f, 0.8f);
                // One shadow caster is enough to give the road some depth (§4.17)
                headlight.shadows = i == 0 ? LightShadows.Soft : LightShadows.None;
            }

            // Dim and sickly, but bright enough that the desaturated CCTV feed still reads
            Transform cabinLights = Group("CabinLights", root).transform;
            foreach (float z in new[] { -4f, 0f, 3.5f }) {
                Light cabin = Group("Cabin Light", cabinLights).AddComponent<Light>();
                cabin.transform.localPosition = new Vector3(0f, 3.2f, z);
                cabin.type = LightType.Point;
                cabin.range = 5f;
                cabin.intensity = 2.2f;
                cabin.color = new Color(0.75f, 0.9f, 0.7f);
                cabin.shadows = LightShadows.None;
            }
            Light dash = Group("Dash Light", cabinLights).AddComponent<Light>();
            dash.transform.localPosition = new Vector3(-0.7f, 1.7f, 5.35f);
            dash.type = LightType.Point;
            dash.range = 1.2f;
            dash.intensity = 0.12f;
            dash.color = new Color(1f, 0.6f, 0.25f);
            dash.shadows = LightShadows.None;
        }

        // §4.8: DriverShoulder 0.35 m right of and 0.25 m behind the driver's head; DriverWindow
        // 0.6 m outside the driver's window; CctvLens<n> 0.35 m in front of camera n, facing it;
        // CabinCenter mid-aisle at head height. +Z of each faces what it frames.
        static void BuildScareAnchors(Transform root) {
            Transform group = Group("ScareAnchors", root).transform;
            Node(group, "Anchor_Scare_DriverShoulder", DriverHeadPos + new Vector3(0.35f, 0f, -0.25f), new Vector3(0f, 180f, 0f));
            Node(group, "Anchor_Scare_DriverWindow", new Vector3(-BusWidth * 0.5f - 0.6f, DriverHeadPos.y, DriverHeadPos.z), new Vector3(0f, 90f, 0f));
            for (int i = 0; i < Cctv.Length; i++) {
                Quaternion rotation = Quaternion.Euler(Cctv[i].Euler);
                Transform lens = Node(group, "Anchor_Scare_CctvLens" + (i + 1), Cctv[i].Position + rotation * new Vector3(0f, 0f, 0.35f));
                lens.localRotation = Quaternion.LookRotation(rotation * Vector3.back, Vector3.up);
            }
            Node(group, "Anchor_Scare_CabinCenter", new Vector3(0f, 1.6f, 0f));
        }

        // Trigger-only boxes on the Occluder layer for the observation linecasts (§2.8): the hull
        // shell and the driver partition. They collide with nothing (§4.16) and, being triggers,
        // add nothing to the body's mass or inertia.
        static void BuildOccluderShell(Transform root) {
            Transform shell = Group("OccluderShell", root).transform;
            float halfW = BusWidth * 0.5f;
            float midY = HullBottom + BusHeight * 0.5f;
            OccluderBox(shell, "Left", new Vector3(-halfW + 0.05f, midY, 0f), new Vector3(0.1f, BusHeight, BusLength));
            OccluderBox(shell, "Right", new Vector3(halfW - 0.05f, midY, 0f), new Vector3(0.1f, BusHeight, BusLength));
            OccluderBox(shell, "Roof", new Vector3(0f, HullBottom + BusHeight - 0.05f, 0f), new Vector3(BusWidth, 0.1f, BusLength));
            OccluderBox(shell, "Floor", new Vector3(0f, HullBottom + 0.05f, 0f), new Vector3(BusWidth, 0.1f, BusLength));
            OccluderBox(shell, "Front", new Vector3(0f, midY, BusLength * 0.5f - 0.05f), new Vector3(BusWidth, BusHeight, 0.1f));
            OccluderBox(shell, "Rear", new Vector3(0f, midY, -BusLength * 0.5f + 0.05f), new Vector3(BusWidth, BusHeight, 0.1f));
            // Behind the driver's seat, up to the seat back: seated heads (y 2.0) stay visible over it
            OccluderBox(shell, "Driver Partition", new Vector3(-0.7f, 1.0f, 4.05f), new Vector3(1.2f, 0.9f, 0.05f));
        }

        static void OccluderBox(Transform parent, string name, Vector3 center, Vector3 size) {
            GameObject go = Group(name, parent);
            go.layer = Layers.Occluder;
            go.transform.localPosition = center;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;
        }

        // ------------------------------------------------------------- bus view

        static GreyboxBusView BuildBusView(Transform root, BusTuning tuning) {
            GameObject viewObject = Group("View", root);
            GreyboxBusView view = viewObject.AddComponent<GreyboxBusView>();
            BuildShell(viewObject.transform);
            Transform steeringWheel = BuildInterior(viewObject.transform);
            Transform[] wheels = BuildWheelVisuals(viewObject.transform, tuning);

            Vector3 closed = new Vector3(1.3f, 1.6f, DoorZ);
            GameObject doorPanel = Box("Door_Panel", viewObject.transform, closed, new Vector3(0.06f, 2.1f, 1.4f), M("Door"));

            // Emissive ceiling panels over the cabin lights; SetInteriorLights dims them
            List<Object> lights = new List<Object>();
            string[] lightNames = { "Light_Cabin_Rear", "Light_Cabin_Mid", "Light_Cabin_Front" };
            float[] lightZ = { -4f, 0f, 3.5f };
            for (int i = 0; i < lightNames.Length; i++) {
                GameObject panel = Box(lightNames[i], viewObject.transform, new Vector3(0f, HullBottom + BusHeight - 0.12f, lightZ[i]),
                    new Vector3(0.5f, 0.03f, 0.9f), M("LampWhite"));
                lights.Add(panel.GetComponent<Renderer>());
            }

            Transform[] dash = BuildDashAnchors(root);

            SetRefArray(view, "wheels", wheels);
            SetRef(view, "steeringWheel", steeringWheel);
            SetRef(view, "doorPanel", doorPanel.transform);
            SetVector(view, "doorClosedLocalPos", closed);
            // Slides back along the outside of the body
            SetVector(view, "doorOpenLocalPos", new Vector3(1.34f, 1.6f, DoorZ - 1.4f));
            SetRefArray(view, "cabinLights", lights.ToArray());
            SetRefArray(view, "dashAnchors", dash);
            return view;
        }

        static void BuildShell(Transform view) {
            Transform shell = Group("Body_Exterior", view).transform;
            float halfW = BusWidth * 0.5f - Panel * 0.5f;
            float halfL = BusLength * 0.5f;
            float top = HullBottom + BusHeight;

            Box("Floor", shell, new Vector3(0f, HullBottom + 0.05f, 0f), new Vector3(BusWidth, 0.1f, BusLength), M("BusInterior"));
            Box("Roof", shell, new Vector3(0f, top - 0.05f, 0f), new Vector3(BusWidth, 0.1f, BusLength), M("BusBody"));
            Box("Rear Wall", shell, new Vector3(0f, (FloorTop + top) * 0.5f, -halfL + Panel * 0.5f), new Vector3(BusWidth, top - FloorTop, Panel), M("BusBody"));

            // Window band runs from 1.45 to 2.65
            const float lowerTop = 1.45f;
            const float upperBottom = 2.65f;
            float lowerH = lowerTop - FloorTop;
            float upperH = top - 0.1f - upperBottom;
            float lowerY = FloorTop + lowerH * 0.5f;
            float upperY = upperBottom + upperH * 0.5f;

            Box("Left Lower", shell, new Vector3(-halfW, lowerY, 0f), new Vector3(Panel, lowerH, BusLength), M("BusBody"));
            Box("Left Upper", shell, new Vector3(-halfW, upperY, 0f), new Vector3(Panel, upperH, BusLength), M("BusBody"));
            Box("Right Upper", shell, new Vector3(halfW, upperY, 0f), new Vector3(Panel, upperH, BusLength), M("BusBody"));

            // The door opening on the kerb side, z 4.1 to 5.5
            float rearLen = DoorBack + halfL;
            Box("Right Lower Rear", shell, new Vector3(halfW, lowerY, -halfL + rearLen * 0.5f), new Vector3(Panel, lowerH, rearLen), M("BusBody"));
            float frontLen = halfL - DoorFront;
            Box("Right Lower Front", shell, new Vector3(halfW, lowerY, DoorFront + frontLen * 0.5f), new Vector3(Panel, lowerH, frontLen), M("BusBody"));

            float pillarH = upperBottom - lowerTop;
            float pillarY = lowerTop + pillarH * 0.5f;
            float[] pillars = { -halfL + 0.075f, -4f, -2f, 0f, 2f, DoorBack - 0.05f, halfL - 0.075f };
            foreach (float z in pillars) {
                Box("Pillar L", shell, new Vector3(-halfW, pillarY, z), new Vector3(Panel, pillarH, 0.15f), M("BusBody"));
                Box("Pillar R", shell, new Vector3(halfW, pillarY, z), new Vector3(Panel, pillarH, 0.15f), M("BusBody"));
            }
            Box("Pillar Door", shell, new Vector3(halfW, pillarY, DoorFront + 0.05f), new Vector3(Panel, pillarH, 0.15f), M("BusBody"));

            // Windscreen opening from 1.5 to 2.95
            float frontZ = halfL - Panel * 0.5f;
            Box("Front Lower", shell, new Vector3(0f, (FloorTop + 1.5f) * 0.5f, frontZ), new Vector3(BusWidth, 1.5f - FloorTop, Panel), M("BusBody"));
            Box("Front Upper", shell, new Vector3(0f, (2.95f + top - 0.1f) * 0.5f, frontZ), new Vector3(BusWidth, top - 0.1f - 2.95f, Panel), M("BusBody"));

            foreach (float side in new[] { -1f, 1f }) {
                Box("Headlight", shell, new Vector3(side * 0.9f, 1f, halfL + 0.01f), new Vector3(0.35f, 0.18f, 0.02f), M("LampWhite"));
                Box("Tail Light", shell, new Vector3(side * 0.9f, 1f, -halfL - 0.01f), new Vector3(0.3f, 0.15f, 0.02f), M("LampRed"));
            }
        }

        // Returns the steering wheel
        static Transform BuildInterior(Transform view) {
            Transform interior = Group("Body_Interior", view).transform;

            // Driver station, left hand side
            const float driverX = -0.7f;
            Box("Dashboard", interior, new Vector3(0f, 1.3f, 5.55f), new Vector3(BusWidth - 0.2f, 0.4f, 0.7f), M("Dash"));
            Box("Binnacle", interior, new Vector3(driverX, 1.58f, 5.5f), new Vector3(0.6f, 0.16f, 0.3f), M("Dash"));
            GameObject wheel = Prim(PrimitiveType.Cylinder, "SteeringWheel", interior, new Vector3(driverX, 1.42f, 5f), new Vector3(0.5f, 0.015f, 0.5f), M("Dash"));
            wheel.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            Box("Driver Seat Base", interior, new Vector3(driverX, 0.77f, 4.55f), new Vector3(0.4f, 0.45f, 0.4f), M("Dash"));
            Box("Driver Seat", interior, new Vector3(driverX, 1.05f, 4.55f), new Vector3(0.55f, 0.12f, 0.55f), M("Seat"));
            Box("Driver Seat Back", interior, new Vector3(driverX, 1.5f, 4.25f), new Vector3(0.55f, 0.9f, 0.1f), M("Seat"));

            // Double benches either side of the aisle, one per row
            Transform benches = Group("Seats", interior).transform;
            for (int row = Rows; row >= 1; row--) {
                float z = RowZ(row);
                foreach (float x in new[] { -0.8f, 0.8f }) {
                    Box("Seat", benches, new Vector3(x, 1f, z), new Vector3(0.85f, 0.12f, 0.5f), M("Seat"));
                    Box("Seat Back", benches, new Vector3(x, 1.4f, z - 0.27f), new Vector3(0.85f, 0.75f, 0.08f), M("Seat"));
                    Box("Seat Leg", benches, new Vector3(x, 0.75f, z), new Vector3(0.1f, 0.4f, 0.1f), M("Dash"));
                }
            }

            // Boxes over the wheels, which poke up through the floor line
            foreach (float z in new[] { FrontAxleZ, RearAxleZ }) {
                foreach (float side in new[] { -1f, 1f }) {
                    Box("Wheel Arch", interior, new Vector3(side * TrackHalf, 0.875f, z), new Vector3(0.45f, 0.65f, 1.4f), M("BusInterior"));
                }
            }
            return wheel.transform;
        }

        // FL, FR, RL, RR; BusController poses them every frame
        static Transform[] BuildWheelVisuals(Transform view, BusTuning tuning) {
            Transform group = Group("Wheels", view).transform;
            string[] names = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            float[] xs = { -TrackHalf, TrackHalf, -TrackHalf, TrackHalf };
            float[] zs = { FrontAxleZ, FrontAxleZ, RearAxleZ, RearAxleZ };
            Transform[] wheels = new Transform[4];
            float diameter = tuning.wheelRadius * 2f;
            for (int i = 0; i < 4; i++) {
                Transform wheel = Node(group, names[i], new Vector3(xs[i], tuning.wheelRadius, zs[i]));
                // Cylinder axis is Y, lay it along the axle
                GameObject tyre = Prim(PrimitiveType.Cylinder, "Tyre", wheel, Vector3.zero, new Vector3(diameter, 0.175f, diameter), M("Tyre"));
                tyre.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                // Off-centre block so wheel spin is visible
                Box("Hub Mark", wheel, new Vector3(0f, tuning.wheelRadius * 0.5f, 0f), new Vector3(0.37f, 0.2f, 0.12f), M("Kerb"));
                wheels[i] = wheel;
            }
            return wheels;
        }

        // Screens on the dash facing the driver (D38): +Z toward the driver's head, local scale x/y
        // the screen size in metres. Placements are greybox guesses around the MVP dashboard.
        static Transform[] BuildDashAnchors(Transform root) {
            Transform[] anchors = new Transform[4];
            anchors[(int)DashScreen.Clock] = DashAnchor(root, "Anchor_Dash_Clock", new Vector3(-0.7f, 1.69f, 5.42f), -25f, new Vector2(0.16f, 0.06f));
            anchors[(int)DashScreen.FareBox] = DashAnchor(root, "Anchor_Dash_FareBox", new Vector3(-1.1f, 1.6f, 5.3f), -20f, new Vector2(0.18f, 0.1f));
            anchors[(int)DashScreen.Gps] = DashAnchor(root, "Anchor_Dash_Gps", new Vector3(-0.15f, 1.64f, 5.3f), -20f, new Vector2(0.32f, 0.2f));
            anchors[(int)DashScreen.Mirror] = DashAnchor(root, "Anchor_Dash_Mirror", new Vector3(-0.35f, 2.8f, 5.6f), 15f, new Vector2(0.4f, 0.12f));
            return anchors;
        }

        static Transform DashAnchor(Transform root, string name, Vector3 position, float tilt, Vector2 size) {
            Transform anchor = Node(root, name, position, new Vector3(tilt, 180f, 0f));
            anchor.localScale = new Vector3(size.x, size.y, 1f);
            return anchor;
        }

        // ============================================================ on foot

        // Lives at the scene root, not under the bus (see OnFootController)
        static void BuildOnFootRig() {
            GameObject rig = new GameObject("OnFootRig");
            rig.layer = Layers.Player;
            CharacterController body = rig.AddComponent<CharacterController>();
            body.height = 1.7f;
            body.radius = 0.28f;
            body.center = new Vector3(0f, 0.85f, 0f);
            body.stepOffset = 0.2f;
            body.skinWidth = 0.03f;
            body.slopeLimit = 45f;
            body.minMoveDistance = 0f;
            OnFootController onFoot = rig.AddComponent<OnFootController>();
            Transform head = Node(rig.transform, "Head", new Vector3(0f, 1.6f, 0f));
            Camera cam = CreateCamera(OnFootCameraName, head, Vector3.zero, Vector3.zero, DriverFov);
            cam.tag = "MainCamera";
            cam.enabled = false;
            cam.cullingMask &= ~Layers.Mask(Layers.PlayerAvatar);
            Transform avatar = Group("Avatar", rig.transform).transform;
            BuildAvatarView(avatar, PassengerPose.Standing);
            SetRef(onFoot, "head", head);
            SetRef(onFoot, "avatarRoot", avatar);
            // The bus hull is a scene object; the scene builder fills it in
            SetRefArray(onFoot, "ignoredColliders", new Object[0]);
            SaveOrOverwritePrefab(rig, OnFootRigPath);
            Object.DestroyImmediate(rig);
        }

        // The PR #5 capsule body on the PlayerAvatar layer (D49)
        static void BuildAvatarView(Transform parent, PassengerPose pose) {
            GreyboxPlayerAvatarView view = parent.gameObject.AddComponent<GreyboxPlayerAvatarView>();
            Transform body = Prim(PrimitiveType.Capsule, "Body", parent, Vector3.zero, Vector3.one, M("Player")).transform;
            Transform head = Prim(PrimitiveType.Sphere, "HeadVisual", parent, Vector3.zero, Vector3.one * 0.26f, M("Player")).transform;
            Box("Face", head, new Vector3(0f, 0.08f, 0.46f), new Vector3(0.6f, 0.2f, 0.15f), M("Face"));
            SetRef(view, "body", body);
            SetRef(view, "head", head);
            SetInt(view, "pose", (int)pose);
            // Pose the saved prefab too, not only at Awake
            view.SetPose(pose);
            SetLayerRecursively(parent.gameObject, Layers.PlayerAvatar);
        }

        // ======================================================= legacy riders

        // The MVP rider, root at the feet; Passenger.SetPose moves the body and head between
        // standing and seated. The angel looks like everyone else on purpose.
        static void BuildLegacyRider<T>(string path, string name) where T : Passenger {
            GameObject root = new GameObject(name);
            T passenger = root.AddComponent<T>();
            // Only there for the player's interaction ray, and only enabled while the bus is walkable
            CapsuleCollider reach = root.AddComponent<CapsuleCollider>();
            reach.isTrigger = true;
            reach.center = new Vector3(0f, 0.65f, 0f);
            reach.radius = 0.32f;
            reach.height = 1.4f;
            reach.enabled = false;
            GameObject body = Prim(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.42f, 0.75f, 0.42f), M("Passenger"));
            GameObject head = Prim(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.62f, 0f), Vector3.one * 0.26f, M("Passenger"));
            // Without a face nobody could tell which way a sphere is looking
            Box("Face", head.transform, new Vector3(0f, 0.08f, 0.46f), new Vector3(0.6f, 0.2f, 0.15f), M("Face"));
            SetRef(passenger, "head", head.transform);
            SetRef(passenger, "body", body.transform);
            SetRef(passenger, "interactCollider", reach);
            SaveOrOverwritePrefab(root, path);
            Object.DestroyImmediate(root);
        }

        // ========================================================= fall camera

        // The third-person cliff camera (§2.14). Disabled; FallDeathPresenter moves it to the
        // cliff's FallCamAnchor and enables it (T-M4-09). No AudioListener: the ears stay with
        // the player's body (D30).
        static void BuildFallCamera() {
            GameObject root = new GameObject("FallCamera");
            Camera cam = CreateCamera("Camera", root.transform, Vector3.zero, Vector3.zero, 60f);
            cam.enabled = false;
            SaveOrOverwritePrefab(root, FallCameraPath);
            Object.DestroyImmediate(root);
        }

        // ============================================================= helpers

        static Camera CreateCamera(string name, Transform parent, Vector3 localPos, Vector3 localEuler, float fov) {
            Camera cam = Group(name, parent).AddComponent<Camera>();
            cam.transform.localPosition = localPos;
            cam.transform.localRotation = Quaternion.Euler(localEuler);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 300f;
            // No skybox at night, clear to the fog colour so the far plane never shows
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            return cam;
        }
    }
}
