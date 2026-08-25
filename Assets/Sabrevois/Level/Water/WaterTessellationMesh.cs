using UnityEngine;

namespace Sabrevois.Level.Water
{
    // Replaces the Water Plane's built-in 1x1 Plane with a subdivided grid so the
    // tessellated water shader has enough base density to work with. Matches the
    // built-in Plane footprint (10x10 local units, UV 0..1) so the object's scale
    // and collider keep working unchanged.
    [RequireComponent(typeof(MeshFilter))]
    [ExecuteAlways]
    public class WaterTessellationMesh : MonoBehaviour
    {
        [Tooltip("Base grid density per side. Tessellation refines this near the camera; keep it low enough to stay cheap far away.")]
        [Range(8, 512)]
        public int gridResolution = 256;

        private const string MeshName = "WaterTessGrid";

        private void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            var existing = filter.sharedMesh;
            if (existing != null && existing.name == MeshName && existing.vertexCount == (gridResolution + 1) * (gridResolution + 1))
                return;
            Generate();
        }

        [ContextMenu("Generate Water Grid")]
        public void Generate()
        {
            var filter = GetComponent<MeshFilter>();
            int n = Mathf.Max(2, gridResolution);
            int vertCount = (n + 1) * (n + 1);

            var mesh = new Mesh { name = MeshName };

            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var normals = new Vector3[vertCount];
            var tangents = new Vector4[vertCount];
            var triangles = new int[n * n * 6];

            Vector4 tangent = new Vector4(1f, 0f, 0f, -1f);

            for (int y = 0; y <= n; y++)
            {
                for (int x = 0; x <= n; x++)
                {
                    int i = y * (n + 1) + x;
                    float u = (float)x / n;
                    float v = (float)y / n;
                    vertices[i] = new Vector3((u - 0.5f) * 10f, 0f, (v - 0.5f) * 10f);
                    uvs[i] = new Vector2(u, v);
                    normals[i] = Vector3.up;
                    tangents[i] = tangent;
                }
            }

            int t = 0;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i0 = y * (n + 1) + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + (n + 1);
                    int i3 = i2 + 1;
                    triangles[t++] = i0;
                    triangles[t++] = i2;
                    triangles[t++] = i1;
                    triangles[t++] = i1;
                    triangles[t++] = i2;
                    triangles[t++] = i3;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
        }
    }
}
