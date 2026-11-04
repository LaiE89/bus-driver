using System.Collections.Generic;
using BusDriver.Core.Data;
using BusDriver.Gameplay.Bus;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusDriver.Gameplay.Views {
    // SKM_Bus_00 in place of the greybox shell. Colliders, seats, cameras and the door logic stay
    // on the bus root; this only poses the art and moves the existing dash anchors onto it.
    sealed class ArtBus {
        const float DoorSwing = 90f;
        const string ModelResource = "SKM_Bus_00";
        static readonly Vector3 DriverEye = new Vector3(-0.7f, 1.9f, 4.6f);

        Transform bus;
        Transform root;
        Transform doorA;
        Transform doorB;
        Quaternion doorAClosed;
        Quaternion doorBClosed;
        float doorASign = 1f;
        float doorBSign = 1f;
        Vector3 doorAAxis = Vector3.up;
        Vector3 doorBAxis = Vector3.up;
        Transform steering;
        Quaternion steeringClosed;
        Vector3 steeringAxis = Vector3.forward;
        readonly Transform[] wheelBones = new Transform[4];
        readonly Quaternion[] wheelFix = new Quaternion[4];
        readonly bool[] wheelSeen = new bool[4];
        static Material[] dressed;

        public bool HasDoors { get { return doorA != null || doorB != null; } }

        public static ArtBus TryAttach(Transform bus) {
            GameObject prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null || bus == null) {
                return null;
            }
            GameObject art = Object.Instantiate(prefab, bus);
            art.name = "ArtBus";
            StripPhysics(art);
            ArtBus rig = new ArtBus();
            rig.bus = bus;
            rig.root = art.transform;
            rig.root.localRotation = Quaternion.identity;
            rig.root.localPosition = Vector3.zero;
            rig.root.localScale = Vector3.one;
            rig.Fit();
            rig.Dress();
            rig.Bind();
            rig.RaiseSeat();
            rig.PlaceSeats();
            return rig;
        }

        public void HideShell(Transform view) {
            if (view == null) {
                return;
            }
            Renderer[] renderers = view.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                if (renderers[i] != null && !renderers[i].transform.IsChildOf(root)) {
                    renderers[i].enabled = false;
                }
            }
        }

        public void SetDoorOpen(float open01) {
            float swing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(open01)) * DoorSwing;
            Swing(doorA, doorAClosed, doorASign, doorAAxis, swing);
            Swing(doorB, doorBClosed, doorBSign, doorBAxis, swing);
        }

        public void SetSteering(float wheelAngleDeg, float ratio) {
            if (steering == null) {
                return;
            }
            steering.localRotation = steeringClosed * Quaternion.AngleAxis(wheelAngleDeg * ratio, steeringAxis);
        }

        // False when this wheel has no art bone, so the greybox wheel can still pose
        public bool SetWheelPose(int wheelIndex, Quaternion busLocalRot) {
            if (wheelIndex < 0 || wheelIndex >= wheelBones.Length || wheelBones[wheelIndex] == null) {
                return false;
            }
            if (!wheelSeen[wheelIndex]) {
                wheelFix[wheelIndex] = Quaternion.Inverse(bus.rotation * busLocalRot) * wheelBones[wheelIndex].rotation;
                wheelSeen[wheelIndex] = true;
                return true;
            }
            wheelBones[wheelIndex].rotation = bus.rotation * busLocalRot * wheelFix[wheelIndex];
            return true;
        }

        public void PlaceScreens(Transform[] anchors) {
            if (anchors == null || anchors.Length < 4) {
                return;
            }
            Vector3 eye = EyePoint();
            Transform monitor = Find(root, "SM_Monitor_Screen");
            Renderer screen = monitor != null ? monitor.GetComponent<Renderer>() : null;
            Vector3 center = screen != null ? screen.bounds.center : monitor != null ? monitor.position : bus.TransformPoint(new Vector3(-0.15f, 1.64f, 5.3f));
            Vector2 gpsSize = new Vector2(0.42f, 0.26f);
            Vector3 gps = center + bus.TransformVector(new Vector3(0f, 0.18f, -0.04f));
            Face(anchors[(int)DashScreen.Gps], gps, eye, gpsSize);
            Transform fareAnchor = anchors[(int)DashScreen.FareBox];
            float fareWidth = fareAnchor != null ? Mathf.Abs(fareAnchor.localScale.x) : 0.18f;
            if (fareWidth < 0.02f) {
                fareWidth = 0.18f;
            }
            float side = gpsSize.x * 0.5f + 0.14f + fareWidth * 0.5f;
            Vector3 fare = gps + bus.TransformVector(new Vector3(-side, 0f, -0.01f));
            Face(fareAnchor, fare, eye);
            Transform speedo = Find(root, "SM_Speedo_00");
            Vector3 clock = speedo != null
                ? (speedo.GetComponent<Renderer>() != null ? speedo.GetComponent<Renderer>().bounds.center : speedo.position)
                : center + bus.TransformVector(new Vector3(0.02f, 0.16f, -0.02f));
            Face(anchors[(int)DashScreen.Clock], clock, eye);
            Transform mirror = DriverMirror();
            if (mirror != null) {
                Renderer glass = mirror.GetComponentInChildren<Renderer>();
                Face(anchors[(int)DashScreen.Mirror], glass != null ? glass.bounds.center : mirror.position, eye);
            }
        }

        static void StripPhysics(GameObject art) {
            Collider[] colliders = art.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) {
                Object.Destroy(colliders[i]);
            }
            Rigidbody[] bodies = art.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++) {
                Object.Destroy(bodies[i]);
            }
            Camera[] cameras = art.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++) {
                cameras[i].enabled = false;
                if (cameras[i].CompareTag("MainCamera")) {
                    cameras[i].tag = "Untagged";
                }
            }
            AudioListener[] listeners = art.GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++) {
                listeners[i].enabled = false;
            }
            Light[] lights = art.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++) {
                lights[i].enabled = false;
            }
        }

        void Fit() {
            float longest = Longest();
            if (longest > 30f) {
                root.localScale = Vector3.one * 0.01f;
            }else if (longest > 0.01f && longest < 3f) {
                root.localScale = Vector3.one * (12f / longest);
            }
            Transform driver = Find(root, "DriverCamera");
            if (driver != null && bus.InverseTransformPoint(driver.position).x > 0.15f) {
                Vector3 scale = root.localScale;
                scale.x = -Mathf.Abs(scale.x);
                root.localScale = scale;
            }
            // The tyre contact is y = 0 on this bus. Renderer bounds hang below the tread, and
            // lining the bus up to those put the wheels in the air.
            float minY = LowestWheel();
            if (float.IsPositiveInfinity(minY)) {
                minY = 0f;
            }
            Vector3 driverLocal = driver != null ? bus.InverseTransformPoint(driver.position) : new Vector3(0.8f, 2.1f, 4.6f);
            root.localPosition += new Vector3(DriverEye.x - driverLocal.x, -minY, DriverEye.z - driverLocal.z);
        }

        // The greybox eye sits on the dash of this model. The art camera is the clear view over it.
        void RaiseSeat() {
            Transform artCam = Find(root, "DriverCamera");
            Transform head = Find(bus, "Anchor_DriverHead");
            if (artCam == null || head == null || head.IsChildOf(root)) {
                return;
            }
            float eyeY = bus.InverseTransformPoint(artCam.position).y + 0.12f;
            Vector3 local = head.localPosition;
            local.y = eyeY;
            head.localPosition = local;
            // The kill scare hangs on this anchor. It was left at the old seat height, under the
            // raised eye, so the face sat below the right shoulder.
            Transform shoulder = Find(bus, "Anchor_Scare_DriverShoulder");
            if (shoulder != null && !shoulder.IsChildOf(root)) {
                Vector3 at = shoulder.localPosition;
                at.y = eyeY + 0.2f;
                shoulder.localPosition = at;
            }
        }

        Vector3 EyePoint() {
            Transform head = Find(bus, "Anchor_DriverHead");
            if (head != null && !head.IsChildOf(root)) {
                return head.position;
            }
            Transform artCam = Find(root, "DriverCamera");
            return artCam != null ? artCam.position : bus.TransformPoint(DriverEye);
        }

        // Sit the existing seat anchors on the cushion pads. The back panels are separate meshes
        // beside the pad; using those put the passenger on the backrest. Row numbers stay put, so
        // the front rows are still the ones nearest the driver.
        void PlaceSeats() {
            List<Vector3> cushions = new List<Vector3>();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                Renderer renderer = renderers[i];
                if (!IsCushion(renderer.transform)) {
                    continue;
                }
                Bounds box = renderer.bounds;
                if (box.size.x > 1.2f || box.size.z > 1.2f || box.size.y > 0.45f || box.size.sqrMagnitude < 0.01f) {
                    continue;
                }
                Vector3 world = new Vector3(box.center.x, box.max.y, box.center.z);
                world = ClearOfBack(renderer.transform, world, box);
                cushions.Add(bus.InverseTransformPoint(world));
            }
            if (cushions.Count < 4) {
                return;
            }
            cushions.Sort((a, b) => b.z.CompareTo(a.z));
            List<List<Vector3>> rows = new List<List<Vector3>>();
            for (int i = 0; i < cushions.Count; i++) {
                if (rows.Count == 0 || Mathf.Abs(cushions[i].z - rows[rows.Count - 1][0].z) > 0.45f) {
                    rows.Add(new List<Vector3>());
                }
                rows[rows.Count - 1].Add(cushions[i]);
            }
            if (rows.Count == 0) {
                return;
            }
            BusSeat[] seats = bus.GetComponentsInChildren<BusSeat>(true);
            int artRows = Mathf.Max(1, rows.Count - 1);
            for (int i = 0; i < seats.Length; i++) {
                BusSeat seat = seats[i];
                if (seat.transform.IsChildOf(root)) {
                    continue;
                }
                int row = Mathf.Clamp(Mathf.RoundToInt((seat.Row - 1) * artRows / 8f), 0, rows.Count - 1);
                rows[row].Sort((a, b) => a.x.CompareTo(b.x));
                int count = rows[row].Count;
                int column = Mathf.Clamp(seat.Column, 0, 3);
                int index = count >= 4 ? Mathf.Min(column, count - 1) : Mathf.Clamp(Mathf.RoundToInt(column * (count - 1) / 3f), 0, count - 1);
                Vector3 cushion = rows[row][index];
                seat.transform.position = bus.TransformPoint(cushion);
            }
        }

        static bool IsCushion(Transform node) {
            string name = node.name;
            if (!name.Contains("SeatBottom") && !name.Contains("Seat_Cushion")) {
                return false;
            }
            if (name.Contains("Back") || name.Contains("Babk") || name.Contains("Driver")) {
                return false;
            }
            Transform walk = node;
            while (walk != null) {
                if (walk.name.Contains("Driver") || walk.name.Contains("Cockpit")) {
                    return false;
                }
                walk = walk.parent;
            }
            return true;
        }

        // The back panel sits on one edge of the pad. A point in the middle of that panel reads
        // as the passenger sitting on the backrest, so the hips move the other way, still on the pad.
        static Vector3 ClearOfBack(Transform cushion, Vector3 world, Bounds pad) {
            Transform seat = cushion.parent;
            if (seat == null) {
                return world;
            }
            Renderer[] parts = seat.GetComponentsInChildren<Renderer>(true);
            Vector3 push = Vector3.zero;
            for (int i = 0; i < parts.Length; i++) {
                string name = parts[i].name;
                if (parts[i].transform == cushion || (!name.Contains("Back") && !name.Contains("Babk"))) {
                    continue;
                }
                Vector3 away = world - parts[i].bounds.center;
                away.y = 0f;
                if (away.sqrMagnitude < 0.0001f || away.sqrMagnitude > 0.36f) {
                    continue;
                }
                push += away.normalized;
            }
            if (push.sqrMagnitude < 0.0001f) {
                return world;
            }
            world += push.normalized * 0.12f;
            world.x = Mathf.Clamp(world.x, pad.min.x + 0.04f, pad.max.x - 0.04f);
            world.z = Mathf.Clamp(world.z, pad.min.z + 0.04f, pad.max.z - 0.04f);
            world.y = pad.max.y;
            return world;
        }

        void Bind() {
            doorA = Find(root, "door1");
            doorB = Find(root, "door2");
            ClaimDoor(doorA, "Door_01", "DoorFront_01", "DoorGlass_01");
            ClaimDoor(doorB, "Door_02", "DoorFront_02", "DoorGlass_02");
            if (doorA != null) {
                doorAClosed = doorA.localRotation;
                ChooseSwing(doorA, out doorASign, out doorAAxis);
            }
            if (doorB != null) {
                doorBClosed = doorB.localRotation;
                ChooseSwing(doorB, out doorBSign, out doorBAxis);
            }
            steering = Find(root, "steercolumn");
            if (steering != null) {
                steeringClosed = steering.localRotation;
                Transform along = Find(steering, "steercolumn_null");
                if (along != null && along.localPosition.sqrMagnitude > 0.0001f) {
                    steeringAxis = along.localPosition.normalized;
                }
                if (!UsedBySkin(steering)) {
                    Transform group = Find(root, "Steeringwheel_GRP");
                    if (group != null && !group.IsChildOf(steering) && !steering.IsChildOf(group)) {
                        group.SetParent(steering, true);
                    }
                }
            }
            string[] bones = { "FL_rot", "FR_rot", "RL_rot", "RR_rot" };
            string[] meshes = { "SM_Wheel_FL", "SM_Wheel_FR", "SM_Wheel_RL", "SM_Wheel_RR" };
            string[] duals = { null, null, "SM_Wheel_RL1", "SM_Wheel_RR1" };
            for (int i = 0; i < bones.Length; i++) {
                wheelBones[i] = Find(root, bones[i]);
                if (wheelBones[i] != null && !UsedBySkin(wheelBones[i])) {
                    Claim(wheelBones[i], meshes[i]);
                    Claim(wheelBones[i], duals[i]);
                }
            }
        }

        void ClaimDoor(Transform bone, params string[] tokens) {
            if (bone == null || UsedBySkin(bone)) {
                return;
            }
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) {
                Transform part = all[i];
                if (part == bone || part.IsChildOf(bone) || !NameHas(part.name, tokens)) {
                    continue;
                }
                if (part.parent != null && NameHas(part.parent.name, tokens)) {
                    continue;
                }
                if (bone.IsChildOf(part)) {
                    continue;
                }
                part.SetParent(bone, true);
            }
        }

        void Claim(Transform bone, string meshName) {
            if (bone == null || string.IsNullOrEmpty(meshName)) {
                return;
            }
            Transform mesh = Find(root, meshName);
            if (mesh != null && mesh != bone && !mesh.IsChildOf(bone) && !bone.IsChildOf(mesh)) {
                mesh.SetParent(bone, true);
            }
        }

        // The joint's null points along the bottom of the leaf, which flips the door up. The hinge
        // is the vertical pin through that joint, on the side of the door. The sign is chosen from
        // the free edge of this leaf only, so one door cannot inherit the other's swing.
        void ChooseSwing(Transform bone, out float sign, out Vector3 axis) {
            axis = HingeAxis(bone);
            Quaternion closed = bone.localRotation;
            float plus = EdgeReach(bone, closed, axis, 75f);
            float minus = EdgeReach(bone, closed, axis, -75f);
            bone.localRotation = closed;
            if (Mathf.Abs(plus - minus) < 0.03f) {
                plus = NullReach(bone, closed, axis, 75f);
                minus = NullReach(bone, closed, axis, -75f);
                bone.localRotation = closed;
            }
            if (Mathf.Min(plus, minus) > NullReach(bone, closed, axis, 0f) - 0.02f && Mathf.Abs(plus - minus) < 0.03f) {
                axis = -axis;
                plus = NullReach(bone, closed, axis, 75f);
                minus = NullReach(bone, closed, axis, -75f);
                bone.localRotation = closed;
            }
            // Toward the aisle. The larger reach is the swing out through the side of the bus.
            sign = plus <= minus ? 1f : -1f;
        }

        float NullReach(Transform bone, Quaternion closed, Vector3 axis, float degrees) {
            bone.localRotation = closed * Quaternion.AngleAxis(degrees, axis);
            Transform nub = Find(bone, bone.name + "_null");
            Vector3 point = nub != null ? nub.position : bone.position + bone.TransformVector(new Vector3(0f, -0.6f, 0f));
            return Mathf.Abs(bus.InverseTransformPoint(point).x);
        }

        Vector3 HingeAxis(Transform bone) {
            Vector3 up = bone.InverseTransformDirection(bus.up);
            if (up.sqrMagnitude < 0.0001f) {
                return Vector3.up;
            }
            return up.normalized;
        }

        float EdgeReach(Transform bone, Quaternion closed, Vector3 axis, float degrees) {
            bone.localRotation = closed * Quaternion.AngleAxis(degrees, axis);
            return Mathf.Abs(bus.InverseTransformPoint(LeafPoint(bone)).x);
        }

        // A point on this leaf, away from the pin. Child meshes win, because a search by name
        // also hit the other door and left one leaf swinging out. The null is the leaf's own
        // bottom edge when the mesh is not parented yet.
        Vector3 LeafPoint(Transform bone) {
            Renderer[] renderers = bone.GetComponentsInChildren<Renderer>(true);
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < renderers.Length; i++) {
                sum += renderers[i].bounds.center;
                count++;
            }
            if (count > 0) {
                return sum / count;
            }
            Transform nub = Find(bone, bone.name + "_null");
            if (nub != null) {
                return nub.position;
            }
            return bone.position + bone.TransformVector(new Vector3(0f, -0.6f, 0f));
        }

        bool UsedBySkin(Transform bone) {
            SkinnedMeshRenderer[] skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skins.Length; i++) {
                Transform[] bones = skins[i].bones;
                if (bones == null) {
                    continue;
                }
                for (int b = 0; b < bones.Length; b++) {
                    if (bones[b] == bone) {
                        return true;
                    }
                }
            }
            return false;
        }

        static void Swing(Transform bone, Quaternion closed, float sign, Vector3 axis, float degrees) {
            if (bone == null) {
                return;
            }
            bone.localRotation = closed * Quaternion.AngleAxis(sign * degrees, axis);
        }

        void Face(Transform anchor, Vector3 worldPos, Vector3 eye) {
            if (anchor == null) {
                return;
            }
            Vector2 size = new Vector2(Mathf.Abs(anchor.localScale.x), Mathf.Abs(anchor.localScale.y));
            Face(anchor, worldPos, eye, size);
        }

        void Face(Transform anchor, Vector3 worldPos, Vector3 eye, Vector2 size) {
            if (anchor == null) {
                return;
            }
            if (size.x < 0.02f) {
                size.x = 0.2f;
            }
            if (size.y < 0.02f) {
                size.y = 0.1f;
            }
            Vector3 toEye = eye - worldPos;
            if (toEye.sqrMagnitude < 0.0001f) {
                toEye = -bus.forward;
            }
            worldPos += toEye.normalized * 0.012f;
            toEye = eye - worldPos;
            anchor.SetPositionAndRotation(worldPos, Quaternion.LookRotation(toEye, bus.up));
            anchor.localScale = new Vector3(size.x, size.y, 1f);
        }

        Transform DriverMirror() {
            Transform left = Find(root, "SM_Mirror_L_00");
            Transform right = Find(root, "SM_Mirror_R_00");
            if (left == null) {
                return right;
            }
            if (right == null) {
                return left;
            }
            float leftX = bus.InverseTransformPoint(left.position).x;
            float rightX = bus.InverseTransformPoint(right.position).x;
            return leftX <= rightX ? left : right;
        }

        // Bottom of the tyre meshes, in bus space. Wheel bounds are the tread, not the loose
        // skinned-mesh box around the whole body.
        float LowestWheel() {
            float min = float.PositiveInfinity;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                string name = renderers[i].name;
                if (!name.Contains("Wheel") || name.Contains("Steering") || name.Contains("Nut")) {
                    continue;
                }
                float bottom = MeshBottom(renderers[i]);
                if (bottom < min) {
                    min = bottom;
                }
            }
            if (!float.IsPositiveInfinity(min)) {
                return min;
            }
            for (int i = 0; i < renderers.Length; i++) {
                float bottom = MeshBottom(renderers[i]);
                if (bottom < min) {
                    min = bottom;
                }
            }
            return min;
        }

        float MeshBottom(Renderer renderer) {
            Mesh mesh = null;
            SkinnedMeshRenderer skin = renderer as SkinnedMeshRenderer;
            if (skin != null) {
                mesh = skin.sharedMesh;
            }else {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null) {
                    mesh = filter.sharedMesh;
                }
            }
            if (mesh == null) {
                return float.PositiveInfinity;
            }
            Bounds box = mesh.bounds;
            if (box.extents.sqrMagnitude < 0.000001f) {
                return float.PositiveInfinity;
            }
            Transform space = renderer.transform;
            Vector3 center = space.TransformPoint(box.center);
            Vector3 ext = box.extents;
            float reach =
                Mathf.Abs(Vector3.Dot(space.TransformVector(new Vector3(ext.x, 0f, 0f)), bus.up)) +
                Mathf.Abs(Vector3.Dot(space.TransformVector(new Vector3(0f, ext.y, 0f)), bus.up)) +
                Mathf.Abs(Vector3.Dot(space.TransformVector(new Vector3(0f, 0f, ext.z)), bus.up));
            float upScale = bus.TransformVector(Vector3.up).magnitude;
            return bus.InverseTransformPoint(center).y - reach / Mathf.Max(upScale, 0.0001f);
        }

        float Longest() {
            float longest = 0f;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                Vector3 size = renderers[i].bounds.size;
                longest = Mathf.Max(longest, size.x, size.y, size.z);
            }
            return longest;
        }

        void Dress() {
            if (dressed == null) {
                dressed = BuildMaterials();
            }
            if (dressed == null) {
                return;
            }
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                Material[] slots = renderers[i].sharedMaterials;
                bool changed = false;
                for (int s = 0; s < slots.Length; s++) {
                    Material replacement = Match(slots[s]);
                    if (replacement != null) {
                        slots[s] = replacement;
                        changed = true;
                    }
                }
                if (changed) {
                    renderers[i].sharedMaterials = slots;
                }
            }
        }

        static Material Match(Material source) {
            if (source == null || dressed == null) {
                return null;
            }
            string name = source.name;
            if (name.Contains("DoorGlass") || name.Contains("Window")) {
                return FindDressed("Window");
            }
            if (name.Contains("Monitor")) {
                return FindDressed("Monitor");
            }
            for (int i = 0; i < dressed.Length; i++) {
                if (dressed[i] != null && name.Contains(dressed[i].name)) {
                    return dressed[i];
                }
            }
            return null;
        }

        static Material FindDressed(string token) {
            for (int i = 0; i < dressed.Length; i++) {
                if (dressed[i] != null && dressed[i].name.Contains(token)) {
                    return dressed[i];
                }
            }
            return null;
        }

        static Material[] BuildMaterials() {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) {
                return null;
            }
            string[] parts = { "Exterior", "Interior", "Trim", "Wheel", "Seats", "Control", "Accessory", "Window", "Monitor" };
            Material[] materials = new Material[parts.Length];
            for (int i = 0; i < parts.Length; i++) {
                materials[i] = Make(lit, parts[i]);
            }
            return materials;
        }

        static Material Make(Shader lit, string part) {
            Material material = new Material(lit);
            material.name = part == "Monitor" ? "M_Monitor_00" : "M_Bus_" + part + "_00";
            if (part == "Monitor") {
                material.SetColor("_BaseColor", new Color(0.01f, 0.012f, 0.014f, 1f));
                material.SetFloat("_Smoothness", 0.4f);
                return material;
            }
            string prefix = part == "Interior" ? "T_Bus_Interior" : "T_Bus_" + part + "_00";
            Texture2D color = Resources.Load<Texture2D>("Textures/" + prefix + "_BC");
            Texture2D normal = Resources.Load<Texture2D>("Textures/" + prefix + "_N");
            Texture2D opacity = Resources.Load<Texture2D>("Textures/" + prefix + "_O");
            Texture2D punched = Punch(color, opacity);
            if (punched != null) {
                color = punched;
            }
            if (color != null) {
                material.SetTexture("_BaseMap", color);
            }
            if (normal != null) {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            bool glass = part == "Window" || punched != null;
            if (glass) {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetShaderPassEnabled("ShadowCaster", false);
                material.SetFloat("_Cull", (float)CullMode.Off);
                if (punched == null) {
                    Color tint = material.GetColor("_BaseColor");
                    tint.a = 0.45f;
                    material.SetColor("_BaseColor", tint);
                }
                material.SetFloat("_Smoothness", 0.85f);
            }else if (part == "Trim" || part == "Wheel") {
                material.SetFloat("_Smoothness", 0.55f);
                material.SetFloat("_Metallic", 0.6f);
            }else {
                material.SetFloat("_Smoothness", part == "Seats" ? 0.15f : 0.35f);
            }
            return material;
        }

        // Null when there is no mask, or the mask is solid white and would not punch a hole
        static Texture2D Punch(Texture2D color, Texture2D opacity) {
            if (color == null || opacity == null || !color.isReadable || !opacity.isReadable) {
                return null;
            }
            if (color.width != opacity.width || color.height != opacity.height || color.width * color.height > 16777216) {
                return null;
            }
            Color[] pixels = color.GetPixels();
            Color[] mask = opacity.GetPixels();
            bool hole = false;
            for (int i = 0; i < pixels.Length; i++) {
                pixels[i].a = mask[i].r;
                if (mask[i].r < 0.98f) {
                    hole = true;
                }
            }
            if (!hole) {
                return null;
            }
            Texture2D dest = new Texture2D(color.width, color.height, TextureFormat.RGBA32, true);
            dest.SetPixels(pixels);
            dest.Apply(true, true);
            dest.name = color.name + "_A";
            return dest;
        }

        static bool NameHas(string name, string[] tokens) {
            for (int i = 0; i < tokens.Length; i++) {
                if (name.Contains(tokens[i])) {
                    return true;
                }
            }
            return false;
        }

        static Transform Find(Transform transform, string name) {
            if (transform == null) {
                return null;
            }
            if (transform.name == name) {
                return transform;
            }
            for (int i = 0; i < transform.childCount; i++) {
                Transform found = Find(transform.GetChild(i), name);
                if (found != null) {
                    return found;
                }
            }
            return null;
        }
    }
}
