using UnityEngine;

namespace BusDriver.Gameplay.Scares {
    // The builtin sphere does not wrap this picture as a globe, so turning that mesh never
    // puts the grin on the lens. This sphere is built so +Z is the smile and +Y is the top
    // of the picture. ShowScareHead aims that +Z straight at the CCTV camera.
    [RequireComponent(typeof(MeshFilter))]
    public sealed class ScareHeadFace : MonoBehaviour {
        // Brightness-weighted center of the grin on the 1024×512 wrap.
        const float FaceU = 0.7514634f;
        const float FaceV = 0.4531967f;

        static Mesh sphere;

        void Awake() {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null) {
                filter.sharedMesh = Sphere();
            }
            transform.localRotation = Quaternion.identity;
        }

        public static Mesh Sphere() {
            if (sphere != null) {
                return sphere;
            }
            const int segments = 64;
            const int rings = 32;
            int columns = segments + 1;
            Vector3[] vertices = new Vector3[(rings + 1) * columns];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];
            // The grin sits a little below the equator of the wrap. This pitch brings it onto +Z.
            Quaternion align = Quaternion.Euler((FaceV - 0.5f) * 180f, 0f, 0f);
            int index = 0;
            for (int ring = 0; ring <= rings; ring++) {
                float v = ring / (float)rings;
                float latitude = (v - 0.5f) * Mathf.PI;
                float cosLat = Mathf.Cos(latitude);
                float sinLat = Mathf.Sin(latitude);
                for (int seg = 0; seg <= segments; seg++) {
                    float u = seg / (float)segments;
                    float longitude = (u - FaceU) * Mathf.PI * 2f;
                    // Image-right goes to -X, which is screen-right once +Z points at the camera.
                    Vector3 direction = new Vector3(
                        -Mathf.Sin(longitude) * cosLat,
                        sinLat,
                        Mathf.Cos(longitude) * cosLat);
                    direction = align * direction;
                    vertices[index] = direction * 0.5f;
                    normals[index] = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
                    uvs[index] = new Vector2(u, v);
                    index++;
                }
            }
            int[] triangles = new int[rings * segments * 6];
            int tri = 0;
            for (int ring = 0; ring < rings; ring++) {
                int row = ring * columns;
                int next = row + columns;
                for (int seg = 0; seg < segments; seg++) {
                    int a = row + seg;
                    int b = a + 1;
                    int c = next + seg;
                    int d = c + 1;
                    triangles[tri++] = a;
                    triangles[tri++] = c;
                    triangles[tri++] = b;
                    triangles[tri++] = b;
                    triangles[tri++] = c;
                    triangles[tri++] = d;
                }
            }
            sphere = new Mesh();
            sphere.name = "ScareHeadSphere";
            sphere.vertices = vertices;
            sphere.normals = normals;
            sphere.uv = uvs;
            sphere.triangles = triangles;
            sphere.RecalculateBounds();
            sphere.hideFlags = HideFlags.DontSave;
            return sphere;
        }
    }
}
