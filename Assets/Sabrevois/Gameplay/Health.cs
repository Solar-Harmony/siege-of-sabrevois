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
        public static event Action<Health> OnAnyDeath;

        [Header("Hitbox")]
        [SerializeField] private Collider _woundHitbox;
        [SerializeField] private WoundsComponent _woundsComponent;
        [SerializeField] private GameObject _dismemberVFXPrefab;

        [Header("Barks")]
        [SerializeField] private BarkPersonality _barkPersonality;
        public BarkPersonality BarkPersonality => _barkPersonality;

        private Camera _mainCamera;
        private Vector3 _lastHitboxToCameraDir;
        private CapsuleCollider _cachedCapsule;
        private float _previousVisibleHeightFraction = 1f;

        private float _fallTargetY;
        private float _fallStartY;
        private float _fallElapsed;
        private const float FallDuration = 0.35f;
        private bool _isFalling;
        private float _rendererTargetLocalY;

        public float GetResistanceAtDepth(float depth, CharacterAtlasData atlas = null, int bodyPartIndex = -1)
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

            var effectiveAtlas = atlas ?? _woundsComponent?.AtlasData;
            int effectiveIndex = bodyPartIndex >= 0 ? bodyPartIndex : _woundsComponent?.LastHitBodyPartIndex ?? -1;
            if (effectiveAtlas != null)
                resistance += effectiveAtlas.GetBodyPartArmour(effectiveIndex);

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
            _fallTargetY = float.MaxValue;
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

            if (!_isFalling) return;

            _fallElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_fallElapsed / FallDuration);
            float eased = 1f - (1f - t) * (1f - t);

            float currentY = Mathf.Lerp(_fallStartY, _fallTargetY, eased);
            transform.position = new Vector3(
                transform.position.x,
                currentY,
                transform.position.z);

            if (_woundsComponent != null)
            {
                var rend = _woundsComponent.Renderer;
                if (rend != null)
                {
                    float rY = Mathf.Lerp(rend.transform.localPosition.y, _rendererTargetLocalY, eased);
                    rend.transform.localPosition = new Vector3(
                        rend.transform.localPosition.x, rY, rend.transform.localPosition.z);
                }
            }

            if (t >= 1f)
            {
                transform.position = new Vector3(
                    transform.position.x,
                    _fallTargetY,
                    transform.position.z);
                if (_woundsComponent != null && _woundsComponent.Renderer != null)
                    _woundsComponent.Renderer.transform.localPosition = new Vector3(
                        _woundsComponent.Renderer.transform.localPosition.x,
                        _rendererTargetLocalY,
                        _woundsComponent.Renderer.transform.localPosition.z);
                _isFalling = false;
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
                        bodyPartName = atlas.BodyPartMappings[bodyPartIndex].Name;
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

                _rendererTargetLocalY = woundsRenderer.transform.localPosition.y - meshOffsetDown;
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
            float desiredY = rootT.position.y - heightLost;

            if (hasGround)
                desiredY = Mathf.Max(groundY, rootT.position.y - heightLost);

            _fallTargetY = Mathf.Min(desiredY, _fallTargetY);

            if (!_isFalling)
            {
                _isFalling = true;
                _fallStartY = rootT.position.y;
                _fallElapsed = 0f;
            }

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
            OnAnyDeath?.Invoke(this);
        }
    }
}
