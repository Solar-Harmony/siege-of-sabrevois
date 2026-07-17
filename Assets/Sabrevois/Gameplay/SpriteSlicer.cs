using System.Collections.Generic;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    public class SpriteSlicer
    {
        public static GameObject CreateSlicedPart(MeshRenderer sourceRenderer, List<Vector2Int> disconnectedNodes, int gridResolution, Bounds initialBounds)
        {
            if (sourceRenderer == null || disconnectedNodes.Count == 0) return null;

            GameObject severedPart = new GameObject("SeveredPart");
            severedPart.transform.position = sourceRenderer.transform.position;
            
            if (Camera.main != null)
            {
                Vector3 viewDir = Camera.main.transform.position - sourceRenderer.transform.position;
                viewDir.y = 0;
                if (viewDir.sqrMagnitude > 0.001f) viewDir.Normalize(); else viewDir = new Vector3(0,0,-1);
                // The generated mesh faces +Z (clockwise winding), so +Z must point to the camera (viewDir)
                severedPart.transform.rotation = Quaternion.LookRotation(viewDir, Vector3.up);
            }
            else
            {
                severedPart.transform.rotation = sourceRenderer.transform.rotation;
            }
            
            severedPart.transform.localScale = sourceRenderer.transform.lossyScale;

            MeshFilter mf = severedPart.AddComponent<MeshFilter>();
            MeshRenderer mr = severedPart.AddComponent<MeshRenderer>();
            
            mr.sharedMaterials = sourceRenderer.sharedMaterials;
            
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(mpb);
            mpb.SetFloat("_EnableBillboard", 0f);
            mpb.SetFloat("_WoundSliceIndex", -1f);
            mr.SetPropertyBlock(mpb);
            
            // Build a simple mesh from the disconnected grid nodes
            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            float stepX = initialBounds.size.x / gridResolution;
            float stepY = initialBounds.size.y / gridResolution;
            int vIndex = 0;

            foreach (var node in disconnectedNodes)
            {
                float x = initialBounds.min.x + node.x * stepX;
                float x2 = initialBounds.min.x + (node.x + 1) * stepX;
                float y = initialBounds.min.y + node.y * stepY;
                float y2 = initialBounds.min.y + (node.y + 1) * stepY;

                vertices.Add(new Vector3(x, y, 0));
                vertices.Add(new Vector3(x2, y, 0));
                vertices.Add(new Vector3(x, y2, 0));
                vertices.Add(new Vector3(x2, y2, 0));

                uvs.Add(new Vector2((float)node.x / gridResolution, (float)node.y / gridResolution));
                uvs.Add(new Vector2((node.x + 1f) / gridResolution, (float)node.y / gridResolution));
                uvs.Add(new Vector2((float)node.x / gridResolution, (node.y + 1f) / gridResolution));
                uvs.Add(new Vector2((node.x + 1f) / gridResolution, (node.y + 1f) / gridResolution));

                triangles.Add(vIndex);
                triangles.Add(vIndex + 2);
                triangles.Add(vIndex + 1);

                triangles.Add(vIndex + 1);
                triangles.Add(vIndex + 2);
                triangles.Add(vIndex + 3);

                vIndex += 4;
            }

            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.sharedMesh = mesh;

            var rb = severedPart.AddComponent<Rigidbody>();
            rb.mass = 5f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationZ;

            var col = severedPart.AddComponent<BoxCollider>();
            col.size = mesh.bounds.size + Vector3.one * 0.02f;
            col.center = mesh.bounds.center;

            var rootColliders = sourceRenderer.transform.root.GetComponentsInChildren<Collider>();
            foreach (var rc in rootColliders)
            {
                Physics.IgnoreCollision(col, rc);
            }

            Vector3 pushDir;
            if (Camera.main != null)
            {
                pushDir = (Camera.main.transform.position - sourceRenderer.transform.position).normalized;
                pushDir.y = 0;
                if (pushDir.sqrMagnitude < 0.001f) pushDir = -sourceRenderer.transform.forward;
            }
            else
            {
                pushDir = -sourceRenderer.transform.forward;
            }
            rb.AddForce(pushDir * 3f + Vector3.up * 2f, ForceMode.VelocityChange);

            Object.Destroy(severedPart, 10f);

            return severedPart;
        }
    }
}