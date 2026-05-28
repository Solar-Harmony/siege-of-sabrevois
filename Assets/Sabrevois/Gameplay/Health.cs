using System;
using System.Collections.Generic;
using Sabrevois.Utils;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    [Serializable]
    public struct LayerRule
    {
        public float PenetrationRequired;
        [Range(0f, 100f)]
        public float InstantDeathChancePercent;
        [Range(0f, 100f)]
        public float PenetrationResistancePercent;
    }

    public class Health : MonoBehaviour
    {
        public List<LayerRule> LayerRules = new List<LayerRule>() 
        {
            new LayerRule { PenetrationRequired = 0.5f, InstantDeathChancePercent = 0f, PenetrationResistancePercent = 10f },
            new LayerRule { PenetrationRequired = 1.0f, InstantDeathChancePercent = 25f, PenetrationResistancePercent = 20f },
            new LayerRule { PenetrationRequired = 1.5f, InstantDeathChancePercent = 100f, PenetrationResistancePercent = 50f }
        };
        
        public bool IsDead => _isDead;
        private bool _isDead = false;
        private float _maxPenetration = 0f;

        public float HealthPercent 
        {
            get
            {
                if (_isDead) return 0f;
                float deathProb = 0f;
                foreach (var rule in LayerRules)
                {
                    if (_maxPenetration >= rule.PenetrationRequired)
                    {
                        if (rule.InstantDeathChancePercent > deathProb)
                        {
                            deathProb = rule.InstantDeathChancePercent;
                        }
                    }
                }
                return 1f - Mathf.Clamp01(deathProb / 100f);
            }
        }

        public event Action<float> OnDamageTaken;
        public event Action OnDeathComplete;
        
        public void TakeDamage(float localPenetration, Vector3? hitDirection = null, bool isEssential = true)
        {
            if (_isDead) return;
            
            if (localPenetration > _maxPenetration)
                _maxPenetration = localPenetration;

            OnDamageTaken?.Invoke(localPenetration);

            bool shouldDie = false;
            if (isEssential)
            {
                foreach (var rule in LayerRules)
                {
                    if (localPenetration >= rule.PenetrationRequired)
                    {
                        if (UnityEngine.Random.Range(0f, 100f) <= rule.InstantDeathChancePercent)
                        {
                            shouldDie = true;
                        }
                    }
                }
            }

            if (shouldDie)
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

                StartCoroutine(DieCoroutine(hitDirection, targetY));
            }
        }
        
        private System.Collections.IEnumerator DieCoroutine(Vector3? hitDirection, float targetY)
        {
            Vector3 cameraPos = Camera.main != null ? Camera.main.transform.position : gameObject.transform.position + Vector3.forward;
            
            Vector3 hitDirXZ;
            if (hitDirection.HasValue && hitDirection.Value.sqrMagnitude > 0.001f)
            {
                hitDirXZ = hitDirection.Value;
                hitDirXZ.y = 0;
                hitDirXZ.Normalize();
            }
            else
            {
                hitDirXZ = gameObject.transform.position - cameraPos;
                hitDirXZ.y = 0;
                if (hitDirXZ.sqrMagnitude > 0.001f) hitDirXZ.Normalize(); else hitDirXZ = gameObject.transform.forward;
            }

            // Align NPC: +Z points directly away from the attacker (so front face -Z looks at attacker)
            Quaternion startRot = Quaternion.LookRotation(hitDirXZ, Vector3.up);
            
            // Final Pose: +Z points DOWN into the dirt (so front face -Z points up at the sky)
            // and +Y (the head vector) points away from the initial impact!
            Quaternion endRot = Quaternion.LookRotation(Vector3.down, hitDirXZ);

            Vector3 startPos = gameObject.transform.position;
            Vector3 endPos = new Vector3(startPos.x, targetY + 0.05f, startPos.z);

            float elapsed = 0f;
            float duration = 0.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                float easeT = t * t * (3f - 2f * t);

                gameObject.transform.rotation = Quaternion.Slerp(startRot, endRot, easeT);
                gameObject.transform.position = Vector3.Lerp(startPos, endPos, easeT);
                
                yield return null;
            }

            gameObject.transform.rotation = endRot;
            gameObject.transform.position = endPos;
            
            OnDeathComplete?.Invoke();
        }
    }
}