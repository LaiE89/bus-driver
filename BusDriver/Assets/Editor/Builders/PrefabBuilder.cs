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
    // kinds (EnvironmentPrefabBuilder, T-M2-06), the Passenger logic prefab and its greybox view
    // (T-M3-01) and a monster variant per MonsterDefinition (T-M4-03, T-M6-05). The 12 m bus
    // geometry, camera placements and seat layout are the MVP's, unchanged (Appendix A.2). The
    // logic root owns every collider, camera, light and anchor; the view owns only renderers (§4.14).
    public static class PrefabBuilder {
        public const string Folder = GeneratedRoot + "/Prefabs";
        public const string BusPath = Folder + "/Bus.prefab";
        public const string OnFootRigPath = Folder + "/OnFootRig.prefab";
        public const string FallCameraPath = Folder + "/FallCamera.prefab";
        public const string PassengerPath = Folder + "/Passenger.prefab";
        public const string ViewsFolder = Folder + "/Views";
        public const string GreyboxPassengerViewPath = ViewsFolder + "/View_GreyboxPassenger.prefab";
        public const string MonstersFolder = Folder + "/Monsters";
        public const string TuningPath = "Assets/Settings/BusTuning.asset";

        // Names the scene builders and tests look things up by
        public const string DriverHeadName = "Anchor_DriverHead";
        public const string DriverPivotName = "DriverCamPivot";
        public const string DriverCameraName = "DriverCamera";
        public const string EarsName = "Driver Ears";
        public const string DriverAvatarName = "DriverAvatar";
        public const string OnFootCameraName = "OnFootCamera";

        // Bus body; the root origin is at ground level under the middle of the bus. Every number
        // is the artist's model's (BusLayout, Appendix A.2).
        const float BusWidth = BusLayout.Width;
        const float BusLength = BusLayout.Length;
        const float BusHeight = BusLayout.Height;
        const float HullBottom = BusLayout.HullBottom;
        const float FloorTop = BusLayout.FloorTop;
        const float Panel = 0.08f;
        public const float FrontAxleZ = BusLayout.FrontAxleZ;
        public const float RearAxleZ = BusLayout.RearAxleZ;
        public const float DoorBack = BusLayout.DoorBack;
        public const float DoorFront = BusLayout.DoorFront;
        const float DoorZ = BusLayout.DoorZ;
        public static readonly Vector3 DriverHeadPos = BusLayout.DriverHeadPos;

        public struct CctvSpec {
            public string Anchor;
            public string CameraName;
            public string Label;
            public Vector3 Position;
            public Vector3 Euler;
        }

        // Appendix A.2; every CCTV camera has FOV 95. MVP labels, with MID added between them.
        public static readonly CctvSpec[] Cctv = {
            new CctvSpec { Anchor = "Anchor_CCTV_Front", CameraName = "CAM 1 Front", Label = "CAM 1  FRONT", Position = BusLayout.CctvFront, Euler = new Vector3(BusLayout.CctvFrontPitch, 180f, 0f) },
            new CctvSpec { Anchor = "Anchor_CCTV_Mid", CameraName = "CAM 2 Mid", Label = "CAM 2  MID", Position = BusLayout.CctvMid, Euler = new Vector3(BusLayout.CctvMidPitch, 180f, 0f) },
            new CctvSpec { Anchor = "Anchor_CCTV_Rear", CameraName = "CAM 3 Rear", Label = "CAM 3  REAR", Position = BusLayout.CctvRear, Euler = new Vector3(BusLayout.CctvRearPitch, 0f, 0f) },
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
            EnsureFolder(ViewsFolder);
            ScareFxBuilder.Build();
            BuildGreyboxPassengerView();
            BuildRider<Passenger>(PassengerPath, "Passenger");
            BuildMonsterVariants();
            UIPrefabBuilder.Build();
            EnvironmentPrefabBuilder.Build();
            AssetDatabase.SaveAssets();
        }

        static Material M(string name) {
            return MaterialLibraryBuilder.Get(name);
        }

        public static string SeatName(int row, int column) {
            return BusLayout.SeatName(row, column);
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
            hull.center = new Vector3(0f, HullBottom + BusHeight * 0.5f, BusLayout.CenterZ);
            hull.size = new Vector3(BusWidth, BusHeight, BusLength);
            hull.sharedMaterial = MaterialLibraryBuilder.HullPhysics();

            BusController controller = bus.AddComponent<BusController>();
            bus.AddComponent<CrashDetector>();
            BusInput input = bus.AddComponent<BusInput>();
            BusEngineSound engineSound = bus.AddComponent<BusEngineSound>();
            BusDoors doors = bus.AddComponent<BusDoors>();
            BusCabin cabin = bus.AddComponent<BusCabin>();

            // The artist's model when it's in Assets/Art, the greybox otherwise (§4.14)
            Transform[] dash = BuildDashAnchors(root);
            BusViewBase view = BusArtViewBuilder.ArtAvailable()
                ? BusArtViewBuilder.Build(root, dash)
                : BuildBusView(root, tuning, dash);

            Transform com = Group("COM", root).transform;
            com.localPosition = new Vector3(0f, 0.9f, 0.2f);
            WheelCollider[] front;
            WheelCollider[] rear;
            BuildWheelColliders(root, tuning, out front, out rear);

            List<Object> seats = BuildSeats(root);
            Transform aisleAtDoor = Node(root, "Anchor_AisleAtDoor", new Vector3(0f, FloorTop, DoorZ));
            // In the door well, which is lower than the cabin floor
            Transform doorStep = Node(root, "Anchor_DoorStep", new Vector3(1.05f, BusLayout.DoorWellTop, DoorZ));
            // Height is found with a raycast at run time: road or kerb
            Transform doorOutside = Node(root, "DoorOutside", new Vector3(BusWidth * 0.5f + 0.55f, 0f, DoorZ));
            Transform standPoint = Node(root, "StandPoint", new Vector3(0.15f, FloorTop, 4.5f));
            DriverSeat driverSeat;
            GameObject interior = BuildInteriorColliders(root, out driverSeat);
            Transform passengerRoot = Group("Passengers", root).transform;

            Camera driverCamera;
            DriverLook look;
            BuildDriverHead(root, out driverCamera, out look);
            CCTVSystem cctv = BuildCctv(root, driverCamera);
            SetRef(look, "cctv", cctv);

            BuildLights(root, view);
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
            // The rear bench's platform: its riders step up from the aisle's end, not along it
            SetBool(cabin, "aisleEnds", true);
            SetFloat(cabin, "aisleEndZ", BusLayout.AisleEndZ);

            SaveOrOverwritePrefab(bus, BusPath);
            Object.DestroyImmediate(bus);
        }

        // 33 seats on their cushions (BusLayout), +Z the way a seated passenger faces. Created rear
        // to front, as the MVP did, so seat choice by index doesn't change.
        static List<Object> BuildSeats(Transform root) {
            Transform seatRoot = Group("Seats", root).transform;
            List<Object> seats = new List<Object>();
            foreach (BusLayout.Seat spec in BusLayout.Seats) {
                Transform seat = Node(seatRoot, SeatName(spec.Row, spec.Column), spec.Position, new Vector3(0f, spec.Yaw, 0f));
                BusSeat busSeat = seat.gameObject.AddComponent<BusSeat>();
                SetInt(busSeat, "row", spec.Row);
                SetInt(busSeat, "column", spec.Column);
                seats.Add(busSeat);
            }
            return seats;
        }

        // Saved inactive and only switched on while the bus is frozen for walking, so these
        // never join the hull's compound collider or change the driving inertia tensor.
        // Unscaled empties with sized BoxColliders, cabin floor top at 0.8.
        static GameObject BuildInteriorColliders(Transform root, out DriverSeat driverSeat) {
            Transform interior = Group("InteriorColliders", root).transform;
            float inner = BusLayout.InnerHalfWidth;
            float wallY = (FloorTop + BusLayout.Ceiling) * 0.5f;
            float wallH = BusLayout.Ceiling - FloorTop;
            float rearZ = BusLayout.InnerRearZ;
            float frontZ = BusLayout.DashZ;
            // The cabin floor, and the door well beside it on the kerb side
            InteriorSpan(interior, "Floor", -inner, BusLayout.DoorWellInnerX, FloorTop - 0.4f, FloorTop, rearZ, frontZ);
            InteriorSpan(interior, "Floor R Rear", BusLayout.DoorWellInnerX, inner, FloorTop - 0.4f, FloorTop, rearZ, DoorBack);
            InteriorSpan(interior, "Floor R Front", BusLayout.DoorWellInnerX, inner, FloorTop - 0.4f, FloorTop, DoorFront, frontZ);
            InteriorSpan(interior, "Door Well", BusLayout.DoorWellInnerX, inner, BusLayout.DoorWellTop - 0.4f, BusLayout.DoorWellTop, DoorBack, DoorFront);
            InteriorSpan(interior, "Wall L", -inner - 0.2f, -inner, FloorTop, BusLayout.Ceiling, rearZ, frontZ);
            InteriorSpan(interior, "Wall R Rear", inner, inner + 0.2f, FloorTop, BusLayout.Ceiling, rearZ, DoorBack);
            InteriorSpan(interior, "Wall R Front", inner, inner + 0.2f, FloorTop, BusLayout.Ceiling, DoorFront, frontZ);
            // Separate so a later step can let the player off the bus by disabling it. It stands at
            // the edge of the door well, so the player can't drop into it and get stuck.
            InteriorSpan(interior, "Doorway Blocker", BusLayout.DoorWellInnerX - 0.1f, BusLayout.DoorWellInnerX + 0.1f,
                BusLayout.DoorWellTop, BusLayout.Ceiling, DoorBack, DoorFront);
            InteriorSpan(interior, "Rear", -inner, inner, FloorTop, BusLayout.Ceiling, rearZ - 0.1f, rearZ);
            InteriorSpan(interior, "Front Dash", -inner, inner, FloorTop, BusLayout.Ceiling, frontZ, frontZ + 0.4f);
            // Three boxes per side over the seats, no gaps between rows to snag on: the front arch
            // seats, the forward rows, the rear arch seats (BusLayout says why these widths). The
            // rear bench spans the width on its platform.
            foreach (float side in new[] { -1f, 1f }) {
                string name = side < 0f ? "L" : "R";
                BenchSpan(interior, "Bench " + name + " Front Arch", side, BusLayout.ArchBenchInnerX, BusLayout.FrontArchRearZ, BusLayout.BenchFrontZ);
                BenchSpan(interior, "Bench " + name, side, BusLayout.BenchInnerX, BusLayout.RearArchFrontZ, BusLayout.FrontArchRearZ);
                BenchSpan(interior, "Bench " + name + " Rear Arch", side, BusLayout.ArchBenchInnerX, BusLayout.BenchRearZ, BusLayout.RearArchFrontZ);
            }
            InteriorSpan(interior, "Rear Bench", -inner, inner, FloorTop, BusLayout.BenchTop, rearZ, BusLayout.RearBenchFrontZ);
            InteriorSpan(interior, "Driver Partition", -inner, BusLayout.DriverPartitionInnerX, FloorTop, BusLayout.DriverPartitionTop,
                BusLayout.DriverPartitionZ - 0.05f, BusLayout.DriverPartitionZ + 0.05f);
            driverSeat = InteriorBox(interior, "Driver Seat", BusLayout.DriverSeatCenter, BusLayout.DriverSeatSize).AddComponent<DriverSeat>();
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

        // From the aisle-side edge |innerX| out to the wall, on the given side (−1 left, +1 right)
        static void BenchSpan(Transform parent, string name, float side, float innerX, float z0, float z1) {
            float a = side * innerX;
            float b = side * BusLayout.InnerHalfWidth;
            InteriorSpan(parent, name, Mathf.Min(a, b), Mathf.Max(a, b), FloorTop, BusLayout.BenchTop, z0, z1);
        }

        // A box given by its extents, which is how the model's measurements read
        static GameObject InteriorSpan(Transform parent, string name, float x0, float x1, float y0, float y1, float z0, float z1) {
            return InteriorBox(parent, name, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
                new Vector3(x1 - x0, y1 - y0, z1 - z0));
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
                float sign = i == 0 ? -1f : 1f;
                string side = i == 0 ? "L" : "R";
                front[i] = CreateWheelCollider(wheels, "WC_F" + side, new Vector3(sign * BusLayout.FrontTrackHalf, colliderY, FrontAxleZ), tuning);
                rear[i] = CreateWheelCollider(wheels, "WC_R" + side, new Vector3(sign * BusLayout.RearTrackHalf, colliderY, RearAxleZ), tuning);
            }
        }

        // FL, FR, RL, RR wheel centres at rest: where the colliders put the wheels
        public static Vector3[] WheelRestCentres(BusTuning tuning) {
            float y = tuning.wheelRadius;
            return new[] {
                new Vector3(-BusLayout.FrontTrackHalf, y, FrontAxleZ),
                new Vector3(BusLayout.FrontTrackHalf, y, FrontAxleZ),
                new Vector3(-BusLayout.RearTrackHalf, y, RearAxleZ),
                new Vector3(BusLayout.RearTrackHalf, y, RearAxleZ),
            };
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
            // The rumble strip and the scares shake the view, never the look pivot
            driverCamera.gameObject.AddComponent<CameraShake>();
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

        static void BuildLights(Transform root, BusViewBase view) {
            Vector3 lamp = BusLayout.HeadlightLeft;
            for (int i = 0; i < 2; i++) {
                Transform anchor = Node(root, i == 0 ? "Anchor_Headlight_L" : "Anchor_Headlight_R",
                    new Vector3(i == 0 ? lamp.x : -lamp.x, lamp.y, lamp.z), new Vector3(6f, 0f, 0f));
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
            // Under the ceiling, where the model's own preview lights hang
            foreach (float z in BusLayout.CabinLightZ) {
                Light cabin = Group("Cabin Light", cabinLights).AddComponent<Light>();
                cabin.transform.localPosition = new Vector3(0f, BusLayout.CabinLightY, z);
                cabin.type = LightType.Point;
                cabin.range = 5f;
                cabin.intensity = 2.2f;
                cabin.color = new Color(0.75f, 0.9f, 0.7f);
                cabin.shadows = LightShadows.None;
            }
            Light dash = Group("Dash Light", cabinLights).AddComponent<Light>();
            dash.transform.localPosition = BusLayout.DashLight;
            dash.type = LightType.Point;
            dash.range = 1.2f;
            dash.intensity = 0.12f;
            dash.color = new Color(1f, 0.6f, 0.25f);
            dash.shadows = LightShadows.None;
            // Scares and telegraphs flicker or cut the cabin lights, not the dash (§2.14, §2.17)
            CabinLights group = cabinLights.gameObject.AddComponent<CabinLights>();
            List<Object> overhead = new List<Object>();
            foreach (Light light in cabinLights.GetComponentsInChildren<Light>()) {
                if (light != dash) {
                    overhead.Add(light);
                }
            }
            SetRefArray(group, "lights", overhead.ToArray());
            SetRef(group, "view", view);
        }

        // §4.8: DriverShoulder 0.35 m right of and 0.25 m behind the driver's head; DriverWindow
        // 0.6 m outside the driver's window; CctvLens<n> 0.35 m in front of camera n, facing it;
        // CabinCenter mid-aisle at head height. +Z of each faces what it frames.
        static void BuildScareAnchors(Transform root) {
            Transform group = Group("ScareAnchors", root).transform;
            Transform shoulder = Node(group, "Anchor_Scare_DriverShoulder", DriverHeadPos + new Vector3(0.35f, 0f, -0.25f), new Vector3(0f, 180f, 0f));
            Transform window = Node(group, "Anchor_Scare_DriverWindow", new Vector3(-BusWidth * 0.5f - 0.6f, DriverHeadPos.y, DriverHeadPos.z), new Vector3(0f, 90f, 0f));
            Object[] lenses = new Object[Cctv.Length];
            for (int i = 0; i < Cctv.Length; i++) {
                Quaternion rotation = Quaternion.Euler(Cctv[i].Euler);
                Transform lens = Node(group, "Anchor_Scare_CctvLens" + (i + 1), Cctv[i].Position + rotation * new Vector3(0f, 0f, 0.35f));
                lens.localRotation = Quaternion.LookRotation(rotation * Vector3.back, Vector3.up);
                lenses[i] = lens;
            }
            // 1.05 m above the cabin floor, as it was over the MVP's floor
            Transform center = Node(group, "Anchor_Scare_CabinCenter", new Vector3(0f, FloorTop + 1.05f, 0f));
            ScareAnchors anchors = group.gameObject.AddComponent<ScareAnchors>();
            SetRef(anchors, "driverShoulder", shoulder);
            SetRef(anchors, "driverWindow", window);
            SetRef(anchors, "cabinCenter", center);
            SetRefArray(anchors, "cctvLens", lenses);
        }

        // Trigger-only boxes on the Occluder layer for the observation linecasts (§2.8): the hull
        // shell and the driver partition. They collide with nothing (§4.16) and, being triggers,
        // add nothing to the body's mass or inertia.
        static void BuildOccluderShell(Transform root) {
            Transform shell = Group("OccluderShell", root).transform;
            float halfW = BusWidth * 0.5f;
            float midY = HullBottom + BusHeight * 0.5f;
            float z = BusLayout.CenterZ;
            OccluderBox(shell, "Left", new Vector3(-halfW + 0.05f, midY, z), new Vector3(0.1f, BusHeight, BusLength));
            OccluderBox(shell, "Right", new Vector3(halfW - 0.05f, midY, z), new Vector3(0.1f, BusHeight, BusLength));
            OccluderBox(shell, "Roof", new Vector3(0f, HullBottom + BusHeight - 0.05f, z), new Vector3(BusWidth, 0.1f, BusLength));
            OccluderBox(shell, "Floor", new Vector3(0f, HullBottom + 0.05f, z), new Vector3(BusWidth, 0.1f, BusLength));
            OccluderBox(shell, "Front", new Vector3(0f, midY, BusLayout.FrontZ - 0.05f), new Vector3(BusWidth, BusHeight, 0.1f));
            OccluderBox(shell, "Rear", new Vector3(0f, midY, BusLayout.RearZ + 0.05f), new Vector3(BusWidth, BusHeight, 0.1f));
            // The rail and divider behind the driver's seat: seated heads (y 2.3) stay visible over it
            float partitionX0 = -BusLayout.InnerHalfWidth;
            float partitionX1 = BusLayout.DriverPartitionInnerX;
            OccluderBox(shell, "Driver Partition", new Vector3((partitionX0 + partitionX1) * 0.5f, (FloorTop + BusLayout.DriverPartitionTop) * 0.5f, BusLayout.DriverPartitionZ),
                new Vector3(partitionX1 - partitionX0, BusLayout.DriverPartitionTop - FloorTop, 0.05f));
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

        static GreyboxBusView BuildBusView(Transform root, BusTuning tuning, Transform[] dash) {
            GameObject viewObject = Group("View", root);
            GreyboxBusView view = viewObject.AddComponent<GreyboxBusView>();
            BuildShell(viewObject.transform);
            Transform steeringWheel = BuildInterior(viewObject.transform);
            Transform[] wheels = BuildWheelVisuals(viewObject.transform, tuning);

            Vector3 closed = new Vector3(BusLayout.DoorPanelX - 0.1f, 1.6f, DoorZ);
            GameObject doorPanel = Box("Door_Panel", viewObject.transform, closed, new Vector3(0.06f, 2.1f, DoorFront - DoorBack), M("Door"));

            // Emissive ceiling panels over the cabin lights; SetInteriorLights dims them
            List<Object> lights = new List<Object>();
            string[] lightNames = { "Light_Cabin_Rear", "Light_Cabin_Mid", "Light_Cabin_Front" };
            float[] lightZ = BusLayout.CabinLightZ;
            for (int i = 0; i < lightNames.Length; i++) {
                GameObject panel = Box(lightNames[i], viewObject.transform, new Vector3(0f, HullBottom + BusHeight - 0.12f, lightZ[i]),
                    new Vector3(0.5f, 0.03f, 0.9f), M("LampWhite"));
                lights.Add(panel.GetComponent<Renderer>());
            }

            SetRefArray(view, "wheels", wheels);
            SetRef(view, "steeringWheel", steeringWheel);
            SetRef(view, "doorPanel", doorPanel.transform);
            SetVector(view, "doorClosedLocalPos", closed);
            // Slides back along the outside of the body
            SetVector(view, "doorOpenLocalPos", new Vector3(BusLayout.DoorPanelX, 1.6f, DoorZ - (DoorFront - DoorBack)));
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

            // Driver station, left hand side, on the cabin floor
            float driverX = DriverHeadPos.x;
            float seatTop = DriverHeadPos.y - 0.85f;
            Box("Dashboard", interior, new Vector3(0f, FloorTop + 0.5f, BusLayout.DashZ), new Vector3(BusWidth - 0.2f, 1f, 0.5f), M("Dash"));
            Box("Binnacle", interior, new Vector3(driverX, 1.72f, 5.45f), new Vector3(0.5f, 0.12f, 0.25f), M("Dash"));
            GameObject wheel = Prim(PrimitiveType.Cylinder, "SteeringWheel", interior, new Vector3(driverX, 1.71f, 5.17f), new Vector3(0.5f, 0.015f, 0.5f), M("Dash"));
            wheel.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            Box("Driver Seat Base", interior, new Vector3(driverX, (FloorTop + seatTop) * 0.5f, 4.45f), new Vector3(0.4f, seatTop - FloorTop, 0.4f), M("Dash"));
            Box("Driver Seat", interior, new Vector3(driverX, seatTop, 4.45f), new Vector3(0.55f, 0.12f, 0.55f), M("Seat"));
            Box("Driver Seat Back", interior, new Vector3(driverX, seatTop + 0.45f, 4.1f), new Vector3(0.55f, 0.9f, 0.1f), M("Seat"));

            // One cushion and back per seat anchor, facing the way its rider does
            Transform benches = Group("Seats", interior).transform;
            foreach (BusLayout.Seat spec in BusLayout.Seats) {
                Quaternion facing = Quaternion.Euler(0f, spec.Yaw, 0f);
                Vector3 p = spec.Position;
                Transform seat = Box("Seat", benches, p - new Vector3(0f, 0.06f, 0f), Vector3.one, M("Seat")).transform;
                seat.localRotation = facing;
                seat.localScale = new Vector3(0.5f, 0.12f, 0.5f);
                Transform back = Box("Seat Back", benches, p + facing * new Vector3(0f, 0.4f, -0.29f), Vector3.one, M("Seat")).transform;
                back.localRotation = facing;
                back.localScale = new Vector3(0.5f, 0.75f, 0.08f);
            }

            // The platforms over the wheels, under the side-facing seats, and under the rear bench
            float arch = BusLayout.FrontArchPlatformTop;
            foreach (float z in new[] { FrontAxleZ, RearAxleZ }) {
                foreach (float side in new[] { -1f, 1f }) {
                    Box("Wheel Arch", interior, new Vector3(side * 1.025f, (FloorTop + arch) * 0.5f, z), new Vector3(0.65f, arch - FloorTop, 1.2f), M("BusInterior"));
                }
            }
            float platformZ = (BusLayout.InnerRearZ + BusLayout.RearPlatformFrontZ) * 0.5f;
            Box("Rear Platform", interior, new Vector3(0f, (FloorTop + BusLayout.RearPlatformTop) * 0.5f, platformZ),
                new Vector3(BusLayout.InnerHalfWidth * 2f, BusLayout.RearPlatformTop - FloorTop, BusLayout.RearPlatformFrontZ - BusLayout.InnerRearZ), M("BusInterior"));
            return wheel.transform;
        }

        // FL, FR, RL, RR; BusController poses them every frame
        static Transform[] BuildWheelVisuals(Transform view, BusTuning tuning) {
            Transform group = Group("Wheels", view).transform;
            string[] names = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            Vector3[] centres = WheelRestCentres(tuning);
            Transform[] wheels = new Transform[4];
            float diameter = tuning.wheelRadius * 2f;
            for (int i = 0; i < 4; i++) {
                Transform wheel = Node(group, names[i], centres[i]);
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
        // the screen size in metres. On the model's monitor, mirror and panels (BusLayout).
        static Transform[] BuildDashAnchors(Transform root) {
            Transform[] anchors = new Transform[4];
            anchors[(int)DashScreen.Clock] = DashAnchor(root, "Anchor_Dash_Clock", BusLayout.DashClock);
            anchors[(int)DashScreen.FareBox] = DashAnchor(root, "Anchor_Dash_FareBox", BusLayout.DashFareBox);
            anchors[(int)DashScreen.Gps] = DashAnchor(root, "Anchor_Dash_Gps", BusLayout.DashGps);
            anchors[(int)DashScreen.Mirror] = DashAnchor(root, "Anchor_Dash_Mirror", BusLayout.DashMirror);
            return anchors;
        }

        static Transform DashAnchor(Transform root, string name, BusLayout.DashSpot spot) {
            Transform anchor = Node(root, name, spot.Position);
            anchor.localRotation = spot.Rotation;
            anchor.localScale = new Vector3(spot.Size.x, spot.Size.y, 1f);
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
            cam.gameObject.AddComponent<CameraShake>();
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

        // ============================================================== riders

        // A rider's logic root (§4.14), root at the feet: Anchor_Head and the interact collider on
        // the Passenger layer. No renderers: ViewFactory creates the view from the rider's look at
        // spawn. The legacy angel is built the same way and looks like everyone else on purpose.
        static void BuildRider<T>(string path, string name) where T : Passenger {
            GameObject root = new GameObject(name);
            T passenger = root.AddComponent<T>();
            // Only there for the player's interaction ray, and only enabled while the bus is walkable
            CapsuleCollider reach = root.AddComponent<CapsuleCollider>();
            reach.isTrigger = true;
            reach.center = new Vector3(0f, 0.65f, 0f);
            reach.radius = 0.32f;
            reach.height = 1.4f;
            reach.enabled = false;
            Transform head = Node(root.transform, "Anchor_Head", new Vector3(0f, Passenger.StandingHeadHeight, 0f));
            SetRef(passenger, "anchorHead", head);
            SetRef(passenger, "interactCollider", reach);
            SetLayerRecursively(root, Layers.Passenger);
            SaveOrOverwritePrefab(root, path);
            Object.DestroyImmediate(root);
        }

        public static string MonsterPath(string monsterId) {
            return MonstersFolder + "/Monster_" + monsterId + ".prefab";
        }

        // One prefab variant of Passenger.prefab per MonsterDefinition (§4.15 step 7, T-M4-03): the
        // rider plus a ThreatMeter and a MonsterBrain pointing at its definition, a KillSequence
        // when its escape isn't None (T-M4-07), and the component for its ability config (the Starer's
        // advance, T-M4-08; the others arrive with M5 and M6). The definition's logicPrefab is the
        // one Data field the builder owns (D98).
        static void BuildMonsterVariants() {
            EnsureFolder(MonstersFolder);
            GameObject rider = AssetDatabase.LoadAssetAtPath<GameObject>(PassengerPath);
            GameRootConfig config = DataSeeder.EnsureGameRootConfig();
            foreach (MonsterDefinition monster in config.monsters ?? new MonsterDefinition[0]) {
                if (monster == null || string.IsNullOrEmpty(monster.id)) {
                    continue;
                }
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(rider);
                instance.name = "Monster_" + monster.id;
                instance.AddComponent<ThreatMeter>();
                MonsterBrain brain = instance.AddComponent<MonsterBrain>();
                SetRef(brain, "definition", monster);
                if (monster.escape.kind != EscapeKind.None) {
                    instance.AddComponent<KillSequence>();
                }
                if (monster.ability is StarerAdvanceConfig) {
                    instance.AddComponent<StarerAdvance>();
                }
                if (monster.ability is AngelStalkConfig) {
                    instance.AddComponent<AngelStalk>();
                }
                GameObject saved = SaveOrOverwritePrefab(instance, MonsterPath(monster.id));
                Object.DestroyImmediate(instance);
                if (monster.logicPrefab != saved) {
                    monster.logicPrefab = saved;
                    EditorUtility.SetDirty(monster);
                }
            }
        }

        // The greybox passenger view (§4.14): the MVP capsule and sphere with a face bar, plus a
        // primitive for every tell and accessory, hidden until used. ViewFactory colours it per look.
        static void BuildGreyboxPassengerView() {
            GameObject root = new GameObject("View_GreyboxPassenger");
            GreyboxPassengerView view = root.AddComponent<GreyboxPassengerView>();
            Transform rig = Group("Rig", root.transform).transform;
            Transform bodyPivot = Group("BodyPivot", rig).transform;
            GameObject body = Prim(PrimitiveType.Capsule, "Body", bodyPivot, new Vector3(0f, 0.75f, 0f), new Vector3(0.42f, 0.75f, 0.42f), M("Passenger"));
            GameObject coat = Prim(PrimitiveType.Capsule, "Acc_LongCoat", body.transform, new Vector3(0f, -0.12f, 0f), new Vector3(1.14f, 0.95f, 1.14f), M("Accessory"));
            GameObject backpack = Box("Acc_Backpack", body.transform, new Vector3(0f, 0.15f, -0.62f), new Vector3(0.8f, 0.42f, 0.45f), M("Accessory"));

            Transform headPivot = Node(bodyPivot, "HeadPivot", new Vector3(0f, Passenger.StandingHeadHeight, 0f));
            GameObject head = Prim(PrimitiveType.Sphere, "HeadVisual", headPivot, Vector3.zero, Vector3.one * 0.26f, M("Passenger"));
            // Without a face nobody could tell which way a sphere is looking
            Box("Face", head.transform, new Vector3(0f, 0.08f, 0.46f), new Vector3(0.6f, 0.2f, 0.15f), M("Face"));
            Transform face = Node(headPivot, "Anchor_Face", new Vector3(0f, 0f, 0.23f));
            Transform eyes = Group("Eyes", headPivot).transform;
            eyes.localPosition = new Vector3(0f, 0.035f, 0.115f);
            foreach (float x in new[] { -0.05f, 0.05f }) {
                Prim(PrimitiveType.Sphere, x < 0f ? "EyeL" : "EyeR", eyes, new Vector3(x, 0f, 0f), Vector3.one * 0.06f, M("Eye"));
            }
            eyes.gameObject.SetActive(false);
            GameObject mouth = Box("Mouth", headPivot, new Vector3(0f, -0.055f, 0.122f), new Vector3(0.07f, 0.018f, 0.012f), M("Face"));
            GameObject hood = Group("Hood", headPivot);
            hood.transform.localPosition = new Vector3(0f, -0.03f, -0.01f);
            hood.AddComponent<MeshFilter>().sharedMesh = MeshBuilder.Get(MeshBuilder.HoodMeshName);
            hood.AddComponent<MeshRenderer>().sharedMaterial = M("Hood");
            hood.SetActive(false);
            GameObject phone = Prim(PrimitiveType.Quad, "Phone", headPivot, new Vector3(0.02f, -0.12f, 0.28f), new Vector3(0.07f, 0.13f, 1f), M("PhoneGlow"));
            // Its lit face toward the camera in front of the rider, tilted up at the eyes
            phone.transform.localRotation = Quaternion.Euler(-30f, 180f, 0f);
            phone.SetActive(false);

            GameObject cap = Group("Acc_Cap", headPivot);
            Prim(PrimitiveType.Cylinder, "Crown", cap.transform, new Vector3(0f, 0.1f, 0f), new Vector3(0.27f, 0.05f, 0.27f), M("Accessory"));
            Box("Brim", cap.transform, new Vector3(0f, 0.085f, 0.13f), new Vector3(0.22f, 0.015f, 0.12f), M("Accessory"));
            GameObject scarf = Prim(PrimitiveType.Cylinder, "Acc_Scarf", headPivot, new Vector3(0f, -0.17f, 0f), new Vector3(0.26f, 0.05f, 0.26f), M("Accessory"));
            GameObject glasses = Box("Acc_Glasses", headPivot, new Vector3(0f, 0.035f, 0.125f), new Vector3(0.2f, 0.035f, 0.02f), M("Accessory"));

            Transform shoulders = Node(bodyPivot, "Shoulders", new Vector3(0f, 1.38f, 0f));
            Transform[] arms = new Transform[2];
            Renderer[] armRenderers = new Renderer[2];
            for (int i = 0; i < 2; i++) {
                float x = i == 0 ? -0.22f : 0.22f;
                arms[i] = Node(shoulders, i == 0 ? "ArmL" : "ArmR", new Vector3(x, 0f, 0f));
                armRenderers[i] = Box("Arm", arms[i], new Vector3(0f, -0.225f, 0f), new Vector3(0.09f, 0.45f, 0.09f), M("Passenger")).GetComponent<Renderer>();
                arms[i].gameObject.SetActive(false);
            }

            GameObject[] accessories = { cap, scarf, backpack, glasses, coat };
            foreach (GameObject accessory in accessories) {
                accessory.SetActive(false);
            }
            SetRef(view, "rig", rig);
            SetRef(view, "bodyPivot", bodyPivot);
            SetRef(view, "body", body.transform);
            SetRef(view, "headPivot", headPivot);
            SetRef(view, "face", face);
            SetRef(view, "eyes", eyes);
            SetRef(view, "mouth", mouth.transform);
            SetRef(view, "hood", hood);
            SetRef(view, "phone", phone.GetComponent<Renderer>());
            SetRef(view, "shoulders", shoulders);
            SetRef(view, "armLeft", arms[0]);
            SetRef(view, "armRight", arms[1]);
            SetRefArray(view, "bodyRenderers", new Object[] { body.GetComponent<Renderer>(), armRenderers[0], armRenderers[1] });
            SetRefArray(view, "headRenderers", new Object[] { head.GetComponent<Renderer>() });
            SetRefArray(view, "accessories", accessories);
            SaveOrOverwritePrefab(root, GreyboxPassengerViewPath);
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
            // The landing on the valley floor shakes it (T-M4-09)
            cam.gameObject.AddComponent<CameraShake>();
            // A pale spot along the view, or the dark bus is lost against the dark valley. Off
            // until the fall; FallDeathPresenter switches it on with the camera.
            Light spot = Group("Fall Light", root.transform).AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 140f;
            spot.spotAngle = 28f;
            spot.innerSpotAngle = 12f;
            spot.intensity = 6000f;
            spot.color = new Color(0.75f, 0.82f, 1f);
            spot.shadows = LightShadows.None;
            spot.enabled = false;
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
