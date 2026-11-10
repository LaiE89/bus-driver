using BusDriver.Core.Data;
using BusDriver.Gameplay.Bus;
using BusDriver.Gameplay.Death;
using BusDriver.Gameplay.Flow;
using BusDriver.Gameplay.Player;
using BusDriver.Gameplay.Shift;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusDriver.Gameplay.Sanity {
    // A dark figure down the middle of the aisle. It only shows while the driver is seated and
    // sanity is under the threshold. Standing up hides it where it is. Recovering past the
    // threshold hides it and steps it back toward the rear.
    public sealed class CabinShadow : MonoBehaviour {
        [Tooltip("Metres per second along the aisle, from the back seat toward the driver")]
        [SerializeField] float metresPerSecond = 0.09f;
        [Tooltip("How close along the aisle, in metres, counts as a catch")]
        [SerializeField] float reachMetres = 1.1f;
        [Tooltip("Metres it retreats toward the back of the cabin when sanity rises past the threshold")]
        [SerializeField] float retreatMetres = 2.5f;
        [Tooltip("Alpha of the silhouette. Lower is more see-through")]
        [SerializeField] float figureAlpha = 0.38f;

        ShiftServices shift;
        SanitySystem sanity;
        float below;
        float progress;
        bool pathReady;
        bool sanityTracked;
        bool wasUnder;
        Vector3 aisleAxis;
        float rearAlong;
        GameObject figure;
        bool caught;

        public float Progress { get { return progress; } }

        public void Init(ShiftServices services, SanitySystem system, float showBelow) {
            shift = services;
            sanity = system;
            below = showBelow;
        }

        void Update() {
            if (caught || shift == null || sanity == null || shift.Cabin == null || shift.Mode == null) {
                return;
            }
            bool under = sanity.Value < below;
            if (sanityTracked && wasUnder && !under) {
                Retreat();
            }
            wasUnder = under;
            sanityTracked = true;
            if (!ShouldShow()) {
                if (figure != null) {
                    figure.SetActive(false);
                }
                return;
            }
            EnsurePath();
            EnsureFigure();
            figure.SetActive(true);
            Advance();
        }

        bool ShouldShow() {
            if (shift.Director == null || shift.Director.State != ShiftState.Driving) {
                return false;
            }
            if (shift.Death != null && shift.Death.IsDying) {
                return false;
            }
            if (shift.Mode.Mode != PlayerMode.Driving) {
                return false;
            }
            return sanity.Value < below;
        }

        Transform Space {
            get {
                BusCabin cabin = shift.Cabin;
                return cabin.PassengerRoot != null ? cabin.PassengerRoot : cabin.transform;
            }
        }

        void EnsurePath() {
            if (pathReady) {
                return;
            }
            BusCabin cabin = shift.Cabin;
            BusSeat[] seats = cabin.GetComponentsInChildren<BusSeat>(true);
            BusSeat rear = null;
            for (int i = 0; i < seats.Length; i++) {
                if (seats[i] == null) {
                    continue;
                }
                if (rear == null || seats[i].Row > rear.Row) {
                    rear = seats[i];
                }
            }
            Vector3 door = cabin.AisleAtDoorLocal;
            Vector3 axis = cabin.AisleInwardLocal;
            axis.y = 0f;
            if (axis.sqrMagnitude < 0.0001f) {
                axis = Vector3.back;
            }
            aisleAxis = axis.normalized;
            if (rear != null) {
                rearAlong = Vector3.Dot(cabin.SeatAisleLocal(rear) - door, aisleAxis);
            } else {
                rearAlong = 8f;
            }
            pathReady = true;
        }

        // The driver's spot projected onto the aisle, so the figure never leaves the centre line.
        float FrontAlong() {
            BusCabin cabin = shift.Cabin;
            Vector3 door = cabin.AisleAtDoorLocal;
            Vector3 driver = cabin.StandPointLocal;
            Camera cam = shift.Mode.DriverCamera;
            if (cam != null) {
                driver = Space.InverseTransformPoint(cam.transform.position);
            }
            return Vector3.Dot(driver - door, aisleAxis);
        }

        Vector3 PointOnAisle(float along) {
            Vector3 door = shift.Cabin.AisleAtDoorLocal;
            Vector3 point = new Vector3(door.x, 0f, door.z) + aisleAxis * along;
            point.y = door.y + 1.05f + Mathf.Sin(Time.time * 1.4f) * 0.05f;
            return point;
        }

        void Retreat() {
            EnsurePath();
            float span = Mathf.Max(0.01f, Mathf.Abs(FrontAlong() - rearAlong));
            progress = Mathf.Clamp01(progress - retreatMetres / span);
        }

        void Advance() {
            float front = FrontAlong();
            float span = Mathf.Abs(front - rearAlong);
            if (span < 0.01f) {
                span = 0.01f;
            }
            progress = Mathf.Clamp01(progress + (metresPerSecond * Time.deltaTime) / span);
            float along = Mathf.Lerp(rearAlong, front, progress);
            Transform space = Space;
            figure.transform.localPosition = PointOnAisle(along);
            Vector3 look = space.TransformDirection(aisleAxis * Mathf.Sign(front - rearAlong));
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f) {
                figure.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
            }
            if (Mathf.Abs(front - along) <= reachMetres) {
                Catch();
            }
        }

        void Catch() {
            if (shift.Death == null || shift.Director == null || shift.Director.State != ShiftState.Driving || shift.Death.IsDying) {
                return;
            }
            caught = true;
            if (figure != null) {
                figure.SetActive(false);
            }
            shift.Death.Die(DeathCause.MonsterKill, "cabin.shadow");
        }

        void EnsureFigure() {
            if (figure != null) {
                return;
            }
            figure = new GameObject("CabinShadow");
            figure.transform.SetParent(Space, false);
            Material mat = DarkMaterial();
            AddPart(PrimitiveType.Capsule, Vector3.zero, new Vector3(0.5f, 0.7f, 0.34f), mat);
            AddPart(PrimitiveType.Sphere, new Vector3(0f, 0.9f, 0f), new Vector3(0.34f, 0.38f, 0.34f), mat);
            AddPart(PrimitiveType.Capsule, new Vector3(-0.34f, 0.1f, 0f), new Vector3(0.1f, 0.48f, 0.1f), mat);
            AddPart(PrimitiveType.Capsule, new Vector3(0.34f, 0.1f, 0f), new Vector3(0.1f, 0.48f, 0.1f), mat);
        }

        void AddPart(PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat) {
            GameObject part = new GameObject(type.ToString());
            part.transform.SetParent(figure.transform, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;
            MeshFilter filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = PrimitiveMesh(type);
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Mesh PrimitiveMesh(PrimitiveType type) {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(temp);
            return mesh;
        }

        // Clone the shipped unlit glass material. A shader found only at runtime is left out of a
        // player build, and the figure then draws with a shader this pipeline never renders.
        Material DarkMaterial() {
            Material kept = Resources.Load<Material>("Materials/CabinShadow");
            Material mat = kept != null ? new Material(kept) : FallbackMaterial();
            Color ink = new Color(0.02f, 0.02f, 0.03f, figureAlpha);
            if (mat.HasProperty("_BaseColor")) {
                mat.SetColor("_BaseColor", ink);
            }
            if (mat.HasProperty("_Color")) {
                mat.SetColor("_Color", ink);
            }
            return mat;
        }

        static Material FallbackMaterial() {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }
    }
}
