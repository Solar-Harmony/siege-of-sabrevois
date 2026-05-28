using System;
using Sabrevois.Utils;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    public class Health : MonoBehaviour
    {
        [Min(1.0f)]
        public float MaxHealth;
        
        public float CurrentHealth { get; private set; }
        public float CurrentHealth01 => CurrentHealth / MaxHealth;
        
        private bool _isDead = false;

        private void Awake()
        {
            CurrentHealth = MaxHealth;
        }
        
        public void TakeDamage(float damage, Vector3? hitDirection = null)
        {
            if (_isDead) return;

            CurrentHealth = Mathf.Max(CurrentHealth - damage, 0);
            OnDamageTaken?.Invoke(damage);

            if (CurrentHealth <= 0)
            {
                _isDead = true;

                var billboard = GetComponent<Billboard>();
                if (billboard != null) billboard.enabled = false;
                
                var wounds = GetComponent<WoundsComponent>();
                if (wounds != null) wounds.SetBillboardEnabled(false);
                
                var navMeshAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navMeshAgent != null) navMeshAgent.enabled = false;

                var agent = GetComponent<Sabrevois.AI.Agent>();
                if (agent != null) agent.enabled = false;
                
                var rb  = GetComponent<Rigidbody>();
                if (rb != null) 
                {
                    rb.isKinematic = true; // freeze physics so the rotated capsule doesn't fall through the floor
                    rb.useGravity = false;
                }
                
                var cap  = GetComponent<CapsuleCollider>();
                if (cap != null) 
                {
                    cap.radius = 0.06f; // keep raycastable but thin
                }
                
                // Do a raycast downwards from slightly above the object to snap firmly to the ground exactly under the center of the entity
                float targetY = gameObject.transform.position.y;
                if (Physics.Raycast(gameObject.transform.position + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    targetY = hit.point.y;
                }
                else if (Terrain.activeTerrain)
                {
                    targetY = Terrain.activeTerrain.SampleHeight(gameObject.transform.position) + Terrain.activeTerrain.transform.position.y;
                }

                if (hitDirection.HasValue)
                {
                    Vector3 dirXZ = hitDirection.Value;
                    dirXZ.y = 0;
                    if (dirXZ.sqrMagnitude > 0.001f)
                    {
                        dirXZ.Normalize();
                        // Align the sprite to lie on its back by making forward point up and top of sprite point along hit direction
                        gameObject.transform.rotation = Quaternion.LookRotation(Vector3.up, dirXZ);
                    }
                    else
                    {
                        gameObject.transform.Rotate(90f, 0f, 0f);    
                    }
                }
                else
                {
                    gameObject.transform.Rotate(90f, 0f, 0f);
                }
                
                Vector3 finalPos = gameObject.transform.position;
                finalPos.y = targetY + 0.05f;
                gameObject.transform.position = finalPos;
            }
        }
        
        public event Action<float> OnDamageTaken;
    }
}