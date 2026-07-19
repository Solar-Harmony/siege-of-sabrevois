using System;
using System.Collections.Generic;
using Sabrevois.Utils;
using SolarHarmony.DynamicWounds2D;
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

    public class Health : MonoBehaviour, IWoundHost
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
                            deathProb = rule.InstantDeathChancePercent;
                    }
                }
                return 1f - Mathf.Clamp01(deathProb / 100f);
            }
        }

        public event Action<float> OnDamageTaken;
        public static event Action<Health, float> OnAnyDamageTaken;
        public event Action OnDeathComplete;

        [Header("Hitbox")]
        [SerializeField] private Collider _woundHitbox;
        [SerializeField] private WoundsComponent _woundsComponent;
        [SerializeField] private GameObject _dismemberVFXPrefab;

        private Camera _mainCamera;
        private Vector3 _lastHitboxToCameraDir;
        private CapsuleCollider _cachedCapsule;
        private float _previousVisibleHeightFraction = 1f;

        public float GetResistanceAtDepth(float depth)
        {
            float resistance = 0f;
            float maxMet = -1f;

            foreach (var rule in LayerRules)
            {
                if (depth >= rule.PenetrationRequired && rule.PenetrationRequired > maxMet)
                {
                    resistance = rule.PenetrationResistancePercent;
                    maxMet = rule.PenetrationRequired;
                }
            }

            var atlas = _woundsComponent?.AtlasData;
            if (atlas != null)
                resistance += atlas.GetBodyPartArmour(_woundsComponent.LastHitBodyPartIndex);

            return Mathf.Min(resistance, 100f);
        }

        public Transform Transform => transform;

        public void ForceKill()
        {
            TakeDamage(999f, null, true);
        }

        public void ApplyMovementImpulse(Vector3 direction, float strength)
        {
            if (_isDead) return;

            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                agent.Move(direction * strength);
            }
            else
            {
                transform.position += direction * strength;
            }
        }

        private void Awake()
        {
            _mainCamera = Camera.main;
            if (_woundsComponent == null)
                _woundsComponent = GetComponentInChildren<WoundsComponent>();
            if (_woundsComponent != null)
                _woundsComponent.OnLimbSevered += HandleLimbSevered;
            if (_woundHitbox == null && _woundsComponent != null)
                _woundHitbox = _woundsComponent.GetComponentInChildren<Collider>();
            if (_woundHitbox == null)
                _woundHitbox = GetComponentInChildren<Collider>();
            _cachedCapsule = GetComponent<CapsuleCollider>();
        }

        private void OnDestroy()
        {
            if (_woundsComponent != null)
                _woundsComponent.OnLimbSevered -= HandleLimbSevered;
        }

        private void HandleLimbSevered(GameObject severedPart, Vector3 hitDirection)
        {
            if (_dismemberVFXPrefab == null || severedPart == null) return;

            var vfx = Instantiate(_dismemberVFXPrefab, severedPart.transform);
            vfx.transform.localPosition = Vector3.zero;
            Destroy(vfx, 10f);
        }

        private void Update()
        {
            if (_isDead) return;
            if (_woundHitbox == null || _mainCamera == null) return;

            Vector3 toCam = _mainCamera.transform.position - _woundHitbox.transform.position;
            toCam.y = 0;

            if (toCam.sqrMagnitude > 0.001f)
            {
                Vector3 dir = toCam.normalized;
                if (Vector3.Dot(dir, _lastHitboxToCameraDir) < 0.9999f)
                {
                    _woundHitbox.transform.rotation = Quaternion.LookRotation(-dir);
                    _lastHitboxToCameraDir = dir;
                }
            }
        }

        public void TakeDamage(float newWoundDepth, Vector3? hitDirection = null, bool isEssential = true)
        {
            if (_isDead) return;

            if (newWoundDepth > _maxPenetration)
                _maxPenetration = newWoundDepth;

            OnDamageTaken?.Invoke(newWoundDepth);
            OnAnyDamageTaken?.Invoke(this, newWoundDepth);

            AdjustColliderFromWounds();

            bool shouldDie = false;
            float highestDeathChance = 0f;
            if (isEssential)
            {
                foreach (var rule in LayerRules)
                {
                    if (newWoundDepth >= rule.PenetrationRequired)
                    {
                        if (rule.InstantDeathChancePercent > highestDeathChance)
                            highestDeathChance = rule.InstantDeathChancePercent;
                    }
                }

                if (highestDeathChance > 0f && UnityEngine.Random.Range(0f, 100f) <= highestDeathChance)
                    shouldDie = true;
            }

            if (shouldDie)
            {
                string bodyPartName = "Unknown";
                int bodyPartIndex = -1;
                if (_woundsComponent != null)
                {
                    bodyPartIndex = _woundsComponent.LastHitBodyPartIndex;
                    var atlas = _woundsComponent.AtlasData;
                    if (atlas != null && bodyPartIndex >= 0 && bodyPartIndex < atlas.BodyPartMappings.Count)
                        bodyPartName = atlas.BodyPartMappings[bodyPartIndex].PartName;
                }

                Debug.Log($"[Health] {name} died. Body part: {bodyPartName} (idx:{bodyPartIndex}, essential:{isEssential}). Depth: {newWoundDepth:F2}. DeathChance: {highestDeathChance}%");

                _isDead = true;

                var billboard = GetComponent<Billboard>();
                if (billboard != null) billboard.enabled = false;

                if (_woundsComponent != null)
                {
                    _woundsComponent.SetShaderBillboardEnabled(false);
                }

                var navMeshAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navMeshAgent != null) navMeshAgent.enabled = false;

                var agent = GetComponent<Sabrevois.AI.Agent>();
                if (agent != null) agent.enabled = false;

                var rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }

                var cap = _cachedCapsule;
                if (cap == null) cap = GetComponent<CapsuleCollider>();
                if (cap != null)
                    cap.radius = 0.2f;

                float targetY = gameObject.transform.position.y;
                if (Physics.Raycast(gameObject.transform.position + Vector3.up * 1f, Vector3.down,
                    out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    targetY = hit.point.y;
                }
                else if (Terrain.activeTerrain)
                {
                    targetY = Terrain.activeTerrain.SampleHeight(gameObject.transform.position)
                        + Terrain.activeTerrain.transform.position.y;
                }

                StartCoroutine(DieCoroutine(hitDirection, targetY));
            }
        }

        private void AdjustColliderFromWounds()
        {
            if (_isDead) return;
            if (_woundsComponent == null || _woundsComponent.InitialLocalBounds.size.y <= 0f) return;

            float newFraction = _woundsComponent.VisibleHeightFraction;
            if (Mathf.Approximately(newFraction, _previousVisibleHeightFraction)) return;
            _previousVisibleHeightFraction = newFraction;

            if (newFraction >= 1f) return;

            var rootT = transform;

            float groundY = rootT.position.y;
            bool hasGround = false;
            if (Physics.Raycast(rootT.position + Vector3.up * 1f, Vector3.down,
                out RaycastHit groundHit, 5f, ~0, QueryTriggerInteraction.Ignore))
            {
                groundY = groundHit.point.y;
                hasGround = true;
            }
            else if (Terrain.activeTerrain != null)
            {
                groundY = Terrain.activeTerrain.SampleHeight(rootT.position)
                          + Terrain.activeTerrain.transform.position.y;
                hasGround = true;
            }

            var woundsRenderer = _woundsComponent.Renderer;
            if (woundsRenderer != null)
            {
                float boundsHeight = _woundsComponent.InitialLocalBounds.size.y;
                float bottomCut = boundsHeight * _woundsComponent.VisibleBottomFraction;
                float meshOffsetDown = bottomCut * woundsRenderer.transform.localScale.y;

                if (hasGround)
                {
                    float rendererWorldY = woundsRenderer.transform.position.y;
                    float targetY = rendererWorldY - meshOffsetDown;
                    if (targetY < groundY)
                        meshOffsetDown = Mathf.Max(0, rendererWorldY - groundY);
                }

                woundsRenderer.transform.localPosition += Vector3.down * meshOffsetDown;
            }

            if (_cachedCapsule != null)
            {
                float oldHeight = _cachedCapsule.height;
                float newHeight = Mathf.Max(0.1f, oldHeight * newFraction);
                _cachedCapsule.height = newHeight;
            }

            if (woundsRenderer == null) return;

            float spriteHeight = _woundsComponent.InitialLocalBounds.size.y * woundsRenderer.transform.lossyScale.y;
            float heightLost = spriteHeight * (1f - newFraction);
            Vector3 drop = Vector3.down * heightLost;

            if (hasGround)
                drop.y = Mathf.Max(groundY - rootT.position.y, -heightLost);

            rootT.position += drop;

            if (hasGround && woundsRenderer != null)
            {
                float rendererY = woundsRenderer.transform.position.y;
                if (rendererY < groundY)
                    rootT.position += Vector3.up * (groundY - rendererY);
            }
        }

        private System.Collections.IEnumerator DieCoroutine(Vector3? hitDirection, float targetY)
        {
            Vector3 cameraPos = Camera.main != null
                ? Camera.main.transform.position
                : gameObject.transform.position + Vector3.forward;

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
                if (hitDirXZ.sqrMagnitude > 0.001f) hitDirXZ.Normalize();
                else hitDirXZ = gameObject.transform.forward;
            }

            Quaternion startRot = Quaternion.LookRotation(hitDirXZ, Vector3.up);
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
