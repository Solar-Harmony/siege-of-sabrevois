using UnityEngine;
using SolarHarmony.DynamicWounds2D;

namespace Sabrevois.Gameplay
{
    public class SeveredPart3DFactory : MonoBehaviour, ISeveredPartFactory
    {
        [SerializeField] private LayerMask _groundLayers = -1;

        public void FinalizeSeveredPart(GameObject severedPart, MeshRenderer sourceRenderer, Vector3 hitDirection)
        {
            var rb = severedPart.AddComponent<Rigidbody>();
            rb.mass = 5f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var mf = severedPart.GetComponent<MeshFilter>();
            BoxCollider col = null;
            if (mf != null && mf.sharedMesh != null)
            {
                var meshBounds = mf.sharedMesh.bounds;
                col = severedPart.AddComponent<BoxCollider>();
                col.size = new Vector3(meshBounds.size.x + 0.02f, meshBounds.size.y + 0.02f, 0.15f);
                col.center = meshBounds.center;
            }
            else
            {
                col = severedPart.AddComponent<BoxCollider>();
            }

            if (col != null)
            {
                var rootColliders = sourceRenderer.transform.root.GetComponentsInChildren<Collider>();
                foreach (var rc in rootColliders)
                    Physics.IgnoreCollision(col, rc);
            }

            rb.excludeLayers = ~_groundLayers;

            rb.AddForce(Vector3.up * 2f + Random.insideUnitSphere * 1.5f, ForceMode.VelocityChange);
            rb.angularVelocity = new Vector3(Random.Range(-8f, 8f), Random.Range(-8f, 8f), Random.Range(-8f, 8f));

            Destroy(severedPart, 10f);
        }
    }
}
