using System.Collections.Generic;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    public class WoundVFXTracker : MonoBehaviour
    {
        public WoundsComponent Source;
        public Vector3 LocalPoint;
        
        private void LateUpdate()
        {
            if (Source != null && Source.gameObject.activeInHierarchy)
            {
                transform.position = Source.GetBillboardWorldPosition(LocalPoint);
            }
        }
    }

    public struct Wound
    {
        public Vector2 Position;
        public Vector3 LocalPoint;
        public Vector3 Normal;
        public float Radius;
        public float Penetration;
        public float Intensity;
        public GameObject VFX;
    }
    
    public class WoundsComponent : MonoBehaviour 
    {
        public event System.Action<Wound, RaycastHit> OnWoundCreated;

        private List<Wound> _wounds = new List<Wound>();
        private int _sliceIndex = -1;
        
        [SerializeField]
        private MeshRenderer _renderer;
        [SerializeField] 
        private GameObject _severeWoundVFX;
        [SerializeField]
        private ParticleSystem _bloodVFX;
        [SerializeField]
        private GameObject _bloodPoolPrefab;
        [SerializeField]
        private float _bloodPoolGrowthDuration = 5f;
        [SerializeField]
        private float _bloodVFXDepthThreshold = 1.0f;
        [SerializeField]
        private float _minBleedDuration = 1.0f;
        [SerializeField]
        private float _maxBleedDuration = 4.0f;
        [SerializeField]
        private float _hitImpulseStrength = 0.4f;

        private MaterialPropertyBlock _mpb;

        private bool _billboardEnabled = true;
        private BoxCollider _hitbox;
        private Health _health;
        private Coroutine _hitReactionCoroutine;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _health = GetComponentInParent<Health>();
            _hitbox = GetComponentInChildren<BoxCollider>();
        }

        private void Start()
        {
            if (GlobalWoundManager.Instance != null)
            {
                _sliceIndex = GlobalWoundManager.Instance.RequestSlice();
                if (_sliceIndex >= 0 && _renderer != null)
                {
                    _renderer.GetPropertyBlock(_mpb);
                    _mpb.SetFloat("_WoundSliceIndex", _sliceIndex);
                    _renderer.SetPropertyBlock(_mpb);
                }
            }
        }

        private void OnDestroy()
        {
            if (GlobalWoundManager.Instance != null && _sliceIndex >= 0)
            {
                GlobalWoundManager.Instance.ReleaseSlice(_sliceIndex);
            }
        }
        
        private void Update()
        {
            // Dynamically rotate the Hitbox so it sits functionally flush with the Billboard's perceived facing angle.
            if (_hitbox != null && _billboardEnabled && Camera.main != null)
            {
                Vector3 toCam = Camera.main.transform.position - _hitbox.transform.position;
                toCam.y = 0;
                if (toCam.sqrMagnitude > 0.001f)
                {
                    _hitbox.transform.rotation = Quaternion.LookRotation(-toCam);
                }
            }

            // Only show severe VFX when health is low (<= 40%), attached to the most damaged wounds
            if (_health != null && !_health.IsDead)
            {
                float maxPen = 0f;
                for (int i = 0; i < _wounds.Count; i++)
                {
                    if (_wounds[i].Penetration > maxPen) maxPen = _wounds[i].Penetration;
                }

                if (maxPen >= _bloodVFXDepthThreshold)
                {
                    for (int i = 0; i < _wounds.Count; i++)
                    {
                        Wound w = _wounds[i];
                        // If it's effectively one of the most severe wounds, instantiate the attached VFX
                        if (w.Penetration >= maxPen - 0.05f && w.VFX == null && _severeWoundVFX != null && _renderer != null)
                        {
                            bool tooClose = false;
                            for (int j = 0; j < _wounds.Count; j++)
                            {
                                // Check if there is already another wound with an active VFX nearby
                                if (_wounds[j].VFX != null && Vector3.Distance(w.LocalPoint, _wounds[j].LocalPoint) < 0.25f)
                                {
                                    tooClose = true;
                                    w.VFX = _wounds[j].VFX; // Share the reference so we don't keep evaluating it every frame
                                    break;
                                }
                            }

                            if (!tooClose)
                            {
                                w.VFX = Instantiate(_severeWoundVFX, _renderer.transform);
                                w.VFX.transform.localPosition = w.LocalPoint;

                                Vector3 localNormal = _renderer.transform.InverseTransformDirection(w.Normal);
                                if (localNormal.sqrMagnitude > 0.001f)
                                {
                                    w.VFX.transform.localRotation = Quaternion.LookRotation(localNormal);
                                }
                            }

                            _wounds[i] = w;
                        }
                    }
                }
            }
        }
        
        private System.Collections.IEnumerator HitReactionRoutine(Vector2 hitUV, Vector3 impactNormal)
        {
            if (_renderer == null) yield break;

            float duration = 0.2f;
            float elapsed = 0f;
            
            float hitX = (hitUV.x - 0.5f) * 2f;
            float hitY = (hitUV.y - 0.5f) * 2f;

            var agent = _health != null ? _health.GetComponent<UnityEngine.AI.NavMeshAgent>() : null;
            Vector3 pushDir = -impactNormal;
            pushDir.y = 0;
            if (pushDir.sqrMagnitude > 0.001f) pushDir = pushDir.normalized;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Sinusoidal bounce: peak at exactly halfway through to drive the impulse strength
                float strength = Mathf.Sin(t * Mathf.PI) * _hitImpulseStrength; 
                
                if (agent != null && agent.enabled)
                {
                    agent.Move(pushDir * (strength * Time.deltaTime * 2.5f));
                }
                else if (_health != null && !_health.IsDead)
                {
                    _health.transform.position += pushDir * (strength * Time.deltaTime * 2.5f);
                }

                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetVector("_HitImpulse", new Vector4(hitX, hitY, strength, 0f));
                _renderer.SetPropertyBlock(_mpb);

                yield return null;
            }

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetVector("_HitImpulse", Vector4.zero);
            _renderer.SetPropertyBlock(_mpb);
            
            _hitReactionCoroutine = null;
        }

        public Vector3 GetBillboardWorldPosition(Vector3 localPoint)
        {
            if (_renderer == null) return transform.TransformPoint(localPoint);
            
            if (_billboardEnabled && Camera.main != null)
            {
                Vector3 centerWS = _renderer.transform.position;
                Vector3 viewDir = Camera.main.transform.position - centerWS;
                viewDir.y = 0;
                if (viewDir.sqrMagnitude > 0.001f) viewDir.Normalize(); else viewDir = new Vector3(0,0,-1);
                
                Vector3 upWS = Vector3.up;
                Vector3 rightWS = Vector3.Cross(upWS, viewDir);
                Vector3 forwardWS = -viewDir;
                
                Vector3 scale = _renderer.transform.lossyScale;
                return centerWS + rightWS * (localPoint.x * scale.x) + upWS * (localPoint.y * scale.y) + forwardWS * (localPoint.z * scale.z);
            }
            return _renderer.transform.TransformPoint(localPoint);
        }

        public void SetBillboardEnabled(bool enabled)
        {
            _billboardEnabled = enabled;
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat("_EnableBillboard", enabled ? 1.0f : 0.0f);
                _renderer.SetPropertyBlock(_mpb);
            }
        }
        
        public void PlayBloodVFXWorld(Vector3 worldPosition, Vector3 worldNormal, Vector3 hitVelocity = default, float penetrationRatio = 0.5f)
        {
            Vector3 localPoint = _renderer != null ? _renderer.transform.InverseTransformPoint(worldPosition) : transform.InverseTransformPoint(worldPosition);
            PlayBloodVFX(localPoint, worldNormal, hitVelocity, penetrationRatio);
        }

        public void PlayBloodVFX(Vector3 localPoint, Vector3 worldNormal, Vector3 hitVelocity = default, float penetrationRatio = 0.5f)
        {
            if (_bloodVFX != null && _renderer != null)
            {
                var instance = Instantiate(_bloodVFX, _renderer.transform);
                instance.transform.position = GetBillboardWorldPosition(localPoint);
                
                var tracker = instance.gameObject.AddComponent<WoundVFXTracker>();
                tracker.Source = this;
                tracker.LocalPoint = localPoint;
                
                if (worldNormal.sqrMagnitude > 0.001f)
                    instance.transform.rotation = Quaternion.LookRotation(worldNormal);

                var main = instance.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                
                if (hitVelocity != default)
                {
                    var vel = instance.velocityOverLifetime;
                    vel.enabled = true;
                    vel.space = ParticleSystemSimulationSpace.World;
                    vel.x = new ParticleSystem.MinMaxCurve(hitVelocity.x * 0.5f, hitVelocity.x);
                    vel.y = new ParticleSystem.MinMaxCurve(hitVelocity.y * 0.5f, hitVelocity.y);
                    vel.z = new ParticleSystem.MinMaxCurve(hitVelocity.z * 0.5f, hitVelocity.z);
                }

                float duration = Mathf.Lerp(_minBleedDuration, _maxBleedDuration, penetrationRatio);
                main.loop = true;
                instance.Play(true);
                
                if (_bloodPoolPrefab != null)
                {
                    if (Physics.Raycast(GetBillboardWorldPosition(localPoint), Vector3.down, out RaycastHit groundHit, 5f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var pool = Instantiate(_bloodPoolPrefab, groundHit.point + Vector3.up * 0.01f, Quaternion.FromToRotation(Vector3.up, groundHit.normal));
                        StartCoroutine(GrowBloodPoolRoutine(pool.transform, _bloodPoolGrowthDuration));
                    }
                }

                StartCoroutine(StopBleedingRoutine(instance, duration));
            }
        }

        private System.Collections.IEnumerator GrowBloodPoolRoutine(Transform pool, float duration)
        {
            float elapsed = 0f;
            Vector3 initialScale = Vector3.zero;
            Vector3 targetScale = Vector3.one * UnityEngine.Random.Range(0.8f, 1.4f);
            
            pool.localScale = initialScale;

            while (elapsed < duration)
            {
                if (pool == null) yield break;
                elapsed += Time.deltaTime;
                pool.localScale = Vector3.Lerp(initialScale, targetScale, elapsed / duration);
                yield return null;
            }
        }

        private System.Collections.IEnumerator StopBleedingRoutine(ParticleSystem vfx, float duration)
        {
            yield return new WaitForSeconds(duration);
            
            if (vfx != null)
            {
                var em = vfx.emission;
                em.enabled = false;
                // Wait for existing particles to die before destroying the object
                Destroy(vfx.gameObject, vfx.main.startLifetime.constantMax + 1f);
            }
        }

        public float ApplyWound(RaycastHit hit, Vector3 trueHitNormal, float radius = 0.15f, float penetration = 0.6f, Vector3 hitVelocity = default)
        {
            // Resolve perspective mismatch between thick physics capsule surface and flat visual sprite plane.
            // By casting directly from the player's camera vector to the visual math plane, we find the exact pixel the crosshair was aimed at!
            Vector3 cameraPos = Camera.main.transform.position;
            Vector3 rayDir = (hit.point - cameraPos).normalized;

            Vector3 localPoint;
            Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);
            Vector2 quadSize = Vector2.one;
            
            if (_renderer != null) 
            {
                var mf = _renderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    localBounds = mf.sharedMesh.bounds;
                    
                quadSize = new Vector2(localBounds.size.x * _renderer.transform.lossyScale.x, localBounds.size.y * _renderer.transform.lossyScale.y);

                if (_billboardEnabled)
                {
                    // Create a mathematical plane matching the GPU Billboard facing the camera (Horizontal-only Billboarding assumption)
                    Vector3 toCamera = cameraPos - _renderer.transform.position;
                    toCamera.y = 0; // If you use horizontal-only GPU billboarding
                    Vector3 planeNormal = -toCamera.normalized;
                    
                    if (planeNormal.sqrMagnitude < 0.001f) 
                        planeNormal = -_renderer.transform.forward;

                    Plane quadPlane = new Plane(planeNormal, _renderer.transform.position);
                    
                    // Intersect the player's line of sight to find where it specifically pierces the 2D artwork
                    if (quadPlane.Raycast(new Ray(cameraPos, rayDir), out float enter))
                    {
                        Vector3 intersectPoint = cameraPos + rayDir * enter;
                        
                        // We must manually inverse-transform the point accounting for the billboard rotation!
                        Quaternion billboardRot = Quaternion.LookRotation(-planeNormal, Vector3.up);
                        Vector3 offset = intersectPoint - _renderer.transform.position;
                        Vector3 unrotatedOffset = Quaternion.Inverse(billboardRot) * offset;
                        
                        // Apply inverse scale
                        unrotatedOffset.x /= _renderer.transform.lossyScale.x;
                        unrotatedOffset.y /= _renderer.transform.lossyScale.y;
                        unrotatedOffset.z /= _renderer.transform.lossyScale.z;
                        
                        localPoint = unrotatedOffset;
                    }
                    else
                    {
                        localPoint = _renderer.transform.InverseTransformPoint(hit.point);
                    }
                }
                else
                {
                    localPoint = _renderer.transform.InverseTransformPoint(hit.point);
                }
            }
            else
            {
                localPoint = transform.InverseTransformPoint(hit.point);
                quadSize = new Vector2(transform.lossyScale.x, transform.lossyScale.y);
            }

            // Convert local point to 0-1 UV space of the bounds
            float u = Mathf.InverseLerp(localBounds.min.x, localBounds.max.x, localPoint.x);
            float v = Mathf.InverseLerp(localBounds.min.y, localBounds.max.y, localPoint.y);
            Vector2 uv = new Vector2(u, v);

            Wound wound = new Wound
            {
                Position = uv, // Store UV in position
                LocalPoint = localPoint,
                Normal = trueHitNormal,        
                Radius = radius,
                Penetration = penetration,
                Intensity = 1f,
                VFX = null
            };
            
            _wounds.Add(wound);

            // Check accumulated depth at this UV area to determine if lower layers are breached
            float totalLocalPenetration = 0f;
            for (int i = 0; i < _wounds.Count; i++) 
            {
                // Distance checking
                if (Vector2.Distance(_wounds[i].Position, uv) < _wounds[i].Radius * 1.5f) 
                {
                    totalLocalPenetration += _wounds[i].Penetration;
                }
            }

            float ratio = 0f;
            if (totalLocalPenetration >= _bloodVFXDepthThreshold)
            {
                ratio = Mathf.Clamp01(totalLocalPenetration / 3.0f); // Normalize depth approximation config
                PlayBloodVFX(wound.LocalPoint, trueHitNormal, hitVelocity, ratio);
            }

            if (_sliceIndex >= 0 && GlobalWoundManager.Instance != null)
            {
                GlobalWoundManager.Instance.AddWoundSplat(_sliceIndex, uv, wound.Radius, wound.Penetration, quadSize, ratio > 0f ? 1f : 0f);
            }

            // Trigger visual hit reaction
            if (_hitReactionCoroutine != null)
            {
                StopCoroutine(_hitReactionCoroutine);
                if (_renderer != null) 
                {
                    _renderer.GetPropertyBlock(_mpb);
                    _mpb.SetVector("_HitImpulse", Vector4.zero);
                    _renderer.SetPropertyBlock(_mpb);
                }
            }
            if (_renderer != null && gameObject.activeInHierarchy && _health != null && !_health.IsDead)
            {
                _hitReactionCoroutine = StartCoroutine(HitReactionRoutine(uv, trueHitNormal));
            }


            OnWoundCreated?.Invoke(wound, hit);
            return totalLocalPenetration;
        }
    }
}