using System;
using UnityEngine;

namespace Sabrevois.Gameplay.Tree
{
    public class FellableTree : MonoBehaviour
    {
        public float Strength;
        
        public void Fell(Vector3 dir)
        {
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.AddForce(dir * Strength, ForceMode.Impulse);
        }

        public void OnCollisionEnter(Collision other)
        {
            if (other.gameObject.TryGetComponent(out FellableTree tree))
            {
                Vector3 rayDir = (tree.transform.position - transform.position).normalized;
                tree.Fell(rayDir);
            }
        }
    }
}