using UnityEngine;
using SolarHarmony.DynamicWounds2D;

namespace Sabrevois.Gameplay
{
    public class SeveredPart3DFactory : MonoBehaviour, ISeveredPartFactory
    {
        public void FinalizeSeveredPart(GameObject severedPart, MeshRenderer sourceRenderer)
        {
            var rb = severedPart.AddComponent<Rigidbody>();
            rb.mass = 5f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationZ;

            var mf = severedPart.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var meshBounds = mf.sharedMesh.bounds;
                var col = severedPart.AddComponent<BoxCollider>();
                col.size = meshBounds.size + Vector3.one * 0.02f;
                col.center = meshBounds.center;

                var rootColliders = sourceRenderer.transform.root.GetComponentsInChildren<Collider>();
                foreach (var rc in rootColliders)
                    Physics.IgnoreCollision(col, rc);
            }
            else
            {
                severedPart.AddComponent<BoxCollider>();
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

            Destroy(severedPart, 10f);
        }
    }
}
