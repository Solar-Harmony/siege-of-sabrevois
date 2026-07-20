using System.Collections.Generic;
using UnityEngine;

namespace SolarHarmony.DynamicWounds2D
{
    public class SpriteSlicer
    {
        public static GameObject CreateSlicedPart(
            MeshRenderer sourceRenderer, List<Vector2Int> disconnectedNodes,
            int gridResolution, Bounds initialBounds,
            ISeveredPartFactory severedPartFactory = null,
            Vector3 hitDirection = default,
            GlobalWoundManager woundManager = null,
            LayerMask groundLayers = default,
            CharacterAtlasData atlasData = null)
        {
            if (sourceRenderer == null || disconnectedNodes.Count == 0) return null;

            GameObject severedPart = new GameObject("SeveredPart");
            severedPart.layer = LayerMask.NameToLayer("Ignore Raycast");

            Vector3 viewDir;
            if (Camera.main != null)
            {
                viewDir = sourceRenderer.transform.position - Camera.main.transform.position;
                viewDir.y = 0;
                if (viewDir.sqrMagnitude > 0.001f) viewDir.Normalize();
                else viewDir = new Vector3(0, 0, -1);
                severedPart.transform.rotation = Quaternion.LookRotation(viewDir, Vector3.up);
            }
            else
            {
                viewDir = -sourceRenderer.transform.forward;
                severedPart.transform.rotation = sourceRenderer.transform.rotation;
            }

            Vector3 cameraDir3D = Camera.main != null
                ? (Camera.main.transform.position - sourceRenderer.transform.position).normalized
                : viewDir;
            severedPart.transform.position = sourceRenderer.transform.position
                + cameraDir3D * 0.05f + Vector3.up * 0.15f;

            severedPart.transform.localScale = sourceRenderer.transform.lossyScale;

            if (woundManager == null)
                woundManager = GlobalWoundManager.Instance;

            int severedSlice = -1;
            if (woundManager != null)
                severedSlice = woundManager.RequestSlice();

            MeshFilter mf = severedPart.AddComponent<MeshFilter>();
            MeshRenderer mr = severedPart.AddComponent<MeshRenderer>();

            mr.sharedMaterials = sourceRenderer.sharedMaterials;

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(mpb);
            mpb.SetFloat("_EnableBillboard", 0f);
            mpb.SetFloat("_WoundSliceIndex", severedSlice);
            mr.SetPropertyBlock(mpb);

            if (severedSlice >= 0)
                SeveredPartSliceTracker.Attach(severedPart, severedSlice, woundManager);

            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> atlasUvs = new List<Vector2>();
            List<Vector2> charUvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            float invTexW = 1f;
            float invTexH = 1f;
            float spriteX = 0f;
            float spriteY = 0f;
            float spriteW = 1f;
            float spriteH = 1f;

            if (atlasData != null && atlasData.LayerSprites != null && atlasData.LayerSprites.Count > 0)
            {
                var sprite0 = atlasData.LayerSprites[0];
                if (sprite0 != null)
                {
                    var r = sprite0.rect;
                    float tw = sprite0.texture != null ? sprite0.texture.width : 1024f;
                    float th = sprite0.texture != null ? sprite0.texture.height : 1024f;
                    invTexW = 1f / tw;
                    invTexH = 1f / th;
                    spriteX = r.x * invTexW;
                    spriteY = r.y * invTexH;
                    spriteW = r.width * invTexW;
                    spriteH = r.height * invTexH;
                }
            }

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

                float cu0 = (float)node.x / gridResolution;
                float cv0 = (float)node.y / gridResolution;
                float cu1 = (node.x + 1f) / gridResolution;
                float cv1 = (node.y + 1f) / gridResolution;

                atlasUvs.Add(new Vector2(spriteX + cu0 * spriteW, spriteY + cv0 * spriteH));
                atlasUvs.Add(new Vector2(spriteX + cu1 * spriteW, spriteY + cv0 * spriteH));
                atlasUvs.Add(new Vector2(spriteX + cu0 * spriteW, spriteY + cv1 * spriteH));
                atlasUvs.Add(new Vector2(spriteX + cu1 * spriteW, spriteY + cv1 * spriteH));

                charUvs.Add(new Vector2(cu0, cv0));
                charUvs.Add(new Vector2(cu1, cv0));
                charUvs.Add(new Vector2(cu0, cv1));
                charUvs.Add(new Vector2(cu1, cv1));

                triangles.Add(vIndex);
                triangles.Add(vIndex + 2);
                triangles.Add(vIndex + 1);

                triangles.Add(vIndex + 1);
                triangles.Add(vIndex + 2);
                triangles.Add(vIndex + 3);

                vIndex += 4;
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, atlasUvs);
            mesh.SetUVs(1, charUvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            mf.mesh = mesh;

            if (severedPartFactory != null)
            {
                severedPartFactory.FinalizeSeveredPart(severedPart, sourceRenderer, hitDirection);
            }
            else
            {
                var rb = severedPart.AddComponent<Rigidbody>();
                rb.mass = 5f;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                if (groundLayers != default)
                    rb.excludeLayers = ~groundLayers;
                var mf2 = severedPart.GetComponent<MeshFilter>();
                if (mf2 != null && mf2.sharedMesh != null)
                {
                    var meshBounds = mf2.sharedMesh.bounds;
                    var col = severedPart.AddComponent<BoxCollider>();
                    col.size = new Vector3(meshBounds.size.x + 0.02f, meshBounds.size.y + 0.02f, 0.15f);
                    col.center = meshBounds.center;
                    var rootColliders = sourceRenderer.transform.root.GetComponentsInChildren<Collider>();
                    foreach (var rc in rootColliders)
                        Physics.IgnoreCollision(col, rc);
                }
                rb.AddForce(Vector3.up * 2f + Random.insideUnitSphere * 1.5f, ForceMode.VelocityChange);
                rb.angularVelocity = new Vector3(
                    Random.Range(-8f, 8f), Random.Range(-8f, 8f), Random.Range(-8f, 8f));
                Object.Destroy(severedPart, 10f);
            }

            return severedPart;
        }
    }

    public class SeveredPartSliceTracker : MonoBehaviour
    {
        private int _sliceIndex = -1;
        private GlobalWoundManager _woundManager;

        public static void Attach(GameObject go, int sliceIndex,
            GlobalWoundManager woundManager = null)
        {
            var tracker = go.AddComponent<SeveredPartSliceTracker>();
            tracker._sliceIndex = sliceIndex;
            tracker._woundManager = woundManager ?? GlobalWoundManager.Instance;
        }

        private void OnDestroy()
        {
            if (_woundManager != null && _sliceIndex >= 0)
                _woundManager.ReleaseSlice(_sliceIndex);
        }
    }
}
