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
    }

    public class Health : MonoBehaviour
    {
        public List<LayerRule> LayerRules = new List<LayerRule>() 
        {
            new LayerRule { PenetrationRequired = 0.5f, InstantDeathChancePercent = 0f },
            new LayerRule { PenetrationRequired = 1.0f, InstantDeathChancePercent = 25f },
            new LayerRule { PenetrationRequired = 1.5f, InstantDeathChancePercent = 100f }
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
        
        public void TakeDamage(float localPenetration, Vector3? hitDirection = null)
        {
            if (_isDead) return;
            
            if (localPenetration > _maxPenetration)
                _maxPenetration = localPenetration;

            OnDamageTaken?.Invoke(localPenetration);

            bool shouldDie = false;
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
            Vector3 startDir = cameraPos - gameObject.transform.position;
            startDir.y = 0;
            if (startDir.sqrMagnitude < 0.001f) startDir = gameObject.transform.forward;
            startDir.Normalize();

            Vector3 endDir = startDir;
            if (hitDirection.HasValue)
            {
                Vector3 dirXZ = hitDirection.Value;
                dirXZ.y = 0;
                if (dirXZ.sqrMagnitude > 0.001f)
                {
                    endDir = -dirXZ.normalized;
                }
            }

            // Extract strictly the Yaw angles so we can explicitly enforce horizontally bounded interpolation
            float startYaw = Mathf.Atan2(startDir.x, startDir.z) * Mathf.Rad2Deg;
            float endYaw = Mathf.Atan2(endDir.x, endDir.z) * Mathf.Rad2Deg;

            Vector3 startPos = gameObject.transform.position;
            Vector3 endPos = new Vector3(startPos.x, targetY + 0.05f, startPos.z);

            float elapsed = 0f;
            float duration = 0.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Add a slight easing curve to feeling heavy
                float easeT = t * t * (3f - 2f * t);

                float currentYaw = Mathf.LerpAngle(startYaw, endYaw, easeT);
                Quaternion yawRot = Quaternion.Euler(0, currentYaw, 0);
                
                // Pitch purely around the local X axis to simulate hinging perfectly backwards over legs
                Quaternion pitchRot = Quaternion.AngleAxis(-90f * easeT, Vector3.right);

                gameObject.transform.rotation = yawRot * pitchRot;
                gameObject.transform.position = Vector3.Lerp(startPos, endPos, easeT);
                
                yield return null;
            }

            Quaternion finalYawRot = Quaternion.Euler(0, endYaw, 0);
            Quaternion finalPitchRot = Quaternion.AngleAxis(-90f, Vector3.right);
            gameObject.transform.rotation = finalYawRot * finalPitchRot;
            gameObject.transform.position = endPos;
        }
    }
}