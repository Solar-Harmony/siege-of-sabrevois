using UnityEngine;
using SolarHarmony.DynamicWounds2D;

namespace Sabrevois.Gameplay
{
    public class SeveredPart3DFactory : MonoBehaviour, ISeveredPartFactory
    {
        public void FinalizeSeveredPart(GameObject severedPart, MeshRenderer sourceRenderer, Vector3 hitDirection)
        {
            var rb = severedPart.AddComponent<Rigidbody>();
            rb.mass = 5f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.constraints = RigidbodyConstraints.FreezeRotationZ;

            var mf = severedPart.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var meshBounds = mf.sharedMesh.bounds;
                var col = severedPart.AddComponent<BoxCollider>();
                col.size = new Vector3(meshBounds.size.x + 0.02f, meshBounds.size.y + 0.02f, 0.15f);
                col.center = meshBounds.center;

                var rootColliders = sourceRenderer.transform.root.GetComponentsInChildren<Collider>();
                foreach (var rc in rootColliders)
                    Physics.IgnoreCollision(col, rc);
            }
            else
            {
                severedPart.AddComponent<BoxCollider>();
            }

            rb.AddForce(Vector3.up * 1.5f, ForceMode.VelocityChange);
            rb.AddTorque(new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)), ForceMode.VelocityChange);

            Destroy(severedPart, 10f);
        }
    }
}
