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
    
    [System.Serializable]
    public struct BodyPartMapping
    {
        public Color Color;
        public string PartName;
        public bool IsEssential;
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
        private float _bloodPoolMinSize = 0.8f;
        [SerializeField]
        private float _bloodPoolMaxSize = 1.4f;
        [SerializeField]
        private Texture2D _bodyPartsMask;
        [SerializeField]
        private List<BodyPartMapping> _bodyPartMappings = new List<BodyPartMapping>();
        [SerializeField]
        private float _bloodVFXDepthThreshold = 1.0f;
        [SerializeField]
        private float _minBleedDuration = 1.0f;
        [SerializeField]
        private float _maxBleedDuration = 4.0f;
        [SerializeField]
        private float _hitImpulseStrength = 0.4f;

        [SerializeField]
        private SpriteConnectivityGraph _connectivityGraph;

        private MaterialPropertyBlock _mpb;

        private bool _billboardEnabled = true;
        private bool[] _liveGraph;
        private int _graphWidth;
        private int _graphHeight;
        private BoxCollider _hitbox;
        private Health _health;
        private Coroutine _hitReactionCoroutine;
        private Bounds _initialLocalBounds;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (_connectivityGraph != null)
            {
                _graphWidth = _connectivityGraph.Width;
                _graphHeight = _connectivityGraph.Height;
                _liveGraph = (bool[])_connectivityGraph.Nodes.Clone();
            }
            _health = GetComponentInParent<Health>();
            if (_health != null) _health.OnDeathComplete += HandleDeathComplete;
            _hitbox = GetComponentInChildren<BoxCollider>();
            if (_renderer != null)
            {
                var mf = _renderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    _initialLocalBounds = mf.sharedMesh.bounds;
            }
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
            if (_health != null) _health.OnDeathComplete -= HandleDeathComplete;
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
        
        private static RaycastHit[] _poolHits = new RaycastHit[16];

        private void HandleDeathComplete()
        {
            if (_bloodPoolPrefab == null) return;
            List<Vector3> spawnedPositions = new List<Vector3>();

            foreach(var w in _wounds)
            {
                if (w.Penetration >= 0.1f)
                {
                    Vector3 worldPos = GetBillboardWorldPosition(w.LocalPoint);
                    bool tooClose = false;
                    foreach(var sp in spawnedPositions) 
                    {
                        if (Vector3.Distance(sp, worldPos) < 0.5f) { tooClose = true; break; }
                    }
                    if (tooClose) continue;
                    
                    int hitCount = Physics.RaycastNonAlloc(worldPos + Vector3.up * 0.5f, Vector3.down, _poolHits, 5f, ~0, QueryTriggerInteraction.Ignore);
                    RaycastHit bestHit = default;
                    bool foundHit = false;
                    for (int i = 0; i < hitCount; i++) {
                        if (_poolHits[i].collider.transform.root != this.transform.root) {
                            if (!foundHit || _poolHits[i].distance < bestHit.distance) {
                                bestHit = _poolHits[i]; 
                                foundHit = true;
                            }
                        }
                    }

                    if (foundHit)
                    {
                        var pool = Instantiate(_bloodPoolPrefab, bestHit.point + Vector3.up * 0.01f, Quaternion.FromToRotation(Vector3.up, bestHit.normal));
                        StartCoroutine(GrowBloodPoolRoutine(pool.transform, _bloodPoolGrowthDuration));
                        spawnedPositions.Add(worldPos);
                    }
                }
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
                

                StartCoroutine(StopBleedingRoutine(instance, duration));
            }
        }

        private System.Collections.IEnumerator GrowBloodPoolRoutine(Transform pool, float duration)
        {
            float elapsed = 0f;
            Vector3 initialScale = Vector3.zero;
            Vector3 targetScale = Vector3.one * UnityEngine.Random.Range(_bloodPoolMinSize, _bloodPoolMaxSize);
            
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

        private void CheckAndProcessSeveredLimbs(Vector2 hitUV, float radius, float depth)
        {
            if (_liveGraph == null || depth < 1.5f) return;
            
            float scaleX = 1f;
            float scaleY = 1f;
            if (_renderer != null)
            {
                var mf = _renderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var localBounds = mf.sharedMesh.bounds;
                    scaleX = Mathf.Abs(localBounds.size.x * _renderer.transform.lossyScale.x);
                    scaleY = Mathf.Abs(localBounds.size.y * _renderer.transform.lossyScale.y);
                }
            }

            for (int y = 0; y < _graphHeight; y++)
            {
                for (int x = 0; x < _graphWidth; x++)
                {
                    if (!_liveGraph[y * _graphWidth + x]) continue;

                    float u = (x + 0.5f) / _graphWidth;
                    float v = (y + 0.5f) / _graphHeight;
                    Vector2 nodeUV = new Vector2(u, v);

                    float nodeDepth = 0f;
                    foreach (var w in _wounds)
                    {
                        float rX = w.Radius / scaleX;
                        float rY = w.Radius / scaleY;
                        float wRadius = Mathf.Max(rX, rY);

                        if (Vector2.Distance(w.Position, nodeUV) <= wRadius)
                        {
                            nodeDepth += w.Penetration;
                        }
                    }

                    if (nodeDepth >= 1.5f)
                    {
                        _liveGraph[y * _graphWidth + x] = false;
                    }
                }
            }

            bool[] visited = new bool[_graphWidth * _graphHeight];
            List<List<int>> components = new List<List<int>>();

            for (int i = 0; i < _liveGraph.Length; i++)
            {
                if (_liveGraph[i] && !visited[i])
                {
                    List<int> comp = new List<int>();
                    Queue<int> queue = new Queue<int>();
                    queue.Enqueue(i);
                    visited[i] = true;
                    comp.Add(i);

                    while (queue.Count > 0)
                    {
                        int curr = queue.Dequeue();
                        int cx = curr % _graphWidth;
                        int cy = curr / _graphWidth;

                        int[] dx = { -1, 1, 0, 0, -1, 1, -1, 1 };
                        int[] dy = { 0, 0, -1, 1, -1, -1, 1, 1 };

                        for (int j = 0; j < 8; j++)
                        {
                            int nx = cx + dx[j];
                            int ny = cy + dy[j];

                            if (nx >= 0 && nx < _graphWidth && ny >= 0 && ny < _graphHeight)
                            {
                                int nIndex = ny * _graphWidth + nx;
                                if (_liveGraph[nIndex] && !visited[nIndex])
                                {
                                    visited[nIndex] = true;
                                    queue.Enqueue(nIndex);
                                    comp.Add(nIndex);
                                }
                            }
                        }
                    }
                    components.Add(comp);
                }
            }

            if (components.Count <= 1) return;

            int largestIndex = 0;
            int maxSize = 0;
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i].Count > maxSize)
                {
                    maxSize = components[i].Count;
                    largestIndex = i;
                }
            }

            for (int i = 0; i < components.Count; i++)
            {
                if (i == largestIndex) continue;

                List<Vector2Int> severedNodes = new List<Vector2Int>();
                foreach (int nIndex in components[i])
                {
                    severedNodes.Add(new Vector2Int(nIndex % _graphWidth, nIndex / _graphWidth));
                    _liveGraph[nIndex] = false;
                }

                float avgY = 0f;
                foreach (var sn in severedNodes)
                    avgY += sn.y;
                avgY /= severedNodes.Count;
                float avgV = avgY / _graphHeight;

                if (components[i].Count >= 5) 
                {
                    var slicedPart = SpriteSlicer.CreateSlicedPart(_renderer, severedNodes, _graphWidth, _initialLocalBounds);

                    if (avgV < 0.45f && _health != null && !_health.IsDead)
                    {
                        _health.TakeDamage(999f, null, true);
                    }
                }
                else
                {
                    if (avgV < 0.4f && _health != null && !_health.IsDead)
                    {
                        _health.TakeDamage(999f, null, true);
                    }
                }

            if (_sliceIndex >= 0 && GlobalWoundManager.Instance != null)
            {
                var mf = _renderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var localBounds = mf.sharedMesh.bounds;
                    var quadSize = new Vector2(localBounds.size.x * _renderer.transform.lossyScale.x, localBounds.size.y * _renderer.transform.lossyScale.y);

                    float cellWorldWidth = quadSize.x / _graphWidth;
                    float cellWorldHeight = quadSize.y / _graphHeight;
                    float worldRadius = Mathf.Max(cellWorldWidth, cellWorldHeight) * 3f;

                    foreach (var sn in severedNodes)
                    {
                        float u = (sn.x + 0.5f) / _graphWidth;
                        float v = (sn.y + 0.5f) / _graphHeight;
                        GlobalWoundManager.Instance.AddWoundSplat(_sliceIndex, new Vector2(u, v), worldRadius, 10.0f, quadSize, 1f);
                    }
                }
            }
            }
        }

        private void GetWoundProjection(Vector3 worldHitPoint, out Vector3 localPoint, out Vector2 uv)
        {
            Vector3 cameraPos = Camera.main.transform.position;
            Vector3 rayDir = (worldHitPoint - cameraPos).normalized;

            Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);
            
            if (_renderer != null) 
            {
                var mf = _renderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    localBounds = mf.sharedMesh.bounds;

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
                        localPoint = _renderer.transform.InverseTransformPoint(worldHitPoint);
                    }
                }
                else
                {
                    localPoint = _renderer.transform.InverseTransformPoint(worldHitPoint);
                }
            }
            else
            {
                localPoint = transform.InverseTransformPoint(worldHitPoint);
            }

            // Convert local point to 0-1 UV space of the bounds
            float u = Mathf.InverseLerp(localBounds.min.x, localBounds.max.x, localPoint.x);
            float v = Mathf.InverseLerp(localBounds.min.y, localBounds.max.y, localPoint.y);
            uv = new Vector2(u, v);
        }

        public float ApplySlashWound(Vector3 worldPos, Vector3 slashDirection, float radius, float weaponPenetration, out bool isEssentialHit, out bool isBleeding, out float resistancePercent, out float damage)
        {
            Vector3 localPoint;
            Vector2 uv;
            GetWoundProjection(worldPos, out localPoint, out uv);
            
            Wound wound = new Wound
            {
                Position = uv,
                LocalPoint = localPoint,
                Normal = slashDirection,
                Radius = radius,
                Penetration = weaponPenetration,
                Intensity = 1f,
                VFX = null
            };
            
            return AddWound(wound, slashDirection, out isEssentialHit, out isBleeding, out resistancePercent, out damage);
        }

        private float AddWound(Wound wound, Vector3 hitNormal, out bool isEssentialHit, out bool isBleeding, out float resistancePercent, out float damage)
        {
            isEssentialHit = false;
            isBleeding = false;
            resistancePercent = 0f;
            damage = wound.Penetration;
            
            Vector2 uv = wound.Position;
            
            if (_bodyPartsMask != null && _bodyPartMappings != null && _bodyPartMappings.Count > 0)
            {
                Color hitColor = _bodyPartsMask.GetPixelBilinear(uv.x, uv.y);
                float minDist = float.MaxValue;
                BodyPartMapping? bestMatch = null;
                foreach (var mapping in _bodyPartMappings)
                {
                    float d = (mapping.Color.r - hitColor.r) * (mapping.Color.r - hitColor.r) +
                              (mapping.Color.g - hitColor.g) * (mapping.Color.g - hitColor.g) +
                              (mapping.Color.b - hitColor.b) * (mapping.Color.b - hitColor.b);
                    if (d < minDist)
                    {
                        minDist = d;
                        bestMatch = mapping;
                    }
                }
                
                if (bestMatch.HasValue && minDist < 0.05f)
                {
                    isEssentialHit = bestMatch.Value.IsEssential;
                }
            }
            
            float oldDepth = 0f;
            for (int i = 0; i < _wounds.Count; i++) 
            {
                if (Vector2.Distance(_wounds[i].Position, uv) < _wounds[i].Radius * 1.5f) 
                {
                    oldDepth += _wounds[i].Penetration;
                }
            }
            
            if (_health != null)
            {
                resistancePercent = _health.GetResistanceAtDepth(oldDepth);
            }

            damage = wound.Penetration * (1f - resistancePercent / 100f);
            damage = Mathf.Max(0, damage);

            wound.Penetration = damage;
            
            _wounds.Add(wound);

            float newDepth = oldDepth + damage;

            float ratio = 0f;
            if (newDepth >= _bloodVFXDepthThreshold)
            {
                ratio = Mathf.Clamp01(newDepth / 3.0f);
                PlayBloodVFX(wound.LocalPoint, hitNormal, default, ratio);
                isBleeding = true;
            }

            if (_sliceIndex >= 0 && GlobalWoundManager.Instance != null)
            {
                var mf = _renderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var localBounds = mf.sharedMesh.bounds;
                    var quadSize = new Vector2(localBounds.size.x * _renderer.transform.lossyScale.x, localBounds.size.y * _renderer.transform.lossyScale.y);
                    GlobalWoundManager.Instance.AddWoundSplat(_sliceIndex, uv, wound.Radius, wound.Penetration, quadSize, ratio > 0f ? 1f : 0f);
                }
            }
            
            CheckAndProcessSeveredLimbs(uv, wound.Radius, newDepth);

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
                _hitReactionCoroutine = StartCoroutine(HitReactionRoutine(uv, hitNormal));
            }
            
            return newDepth;
        }

        private void OnDrawGizmos()
        {
            bool[] graphToDraw = _liveGraph;
            int width = _graphWidth;
            int height = _graphHeight;

            if (graphToDraw == null && _connectivityGraph != null)
            {
                graphToDraw = _connectivityGraph.Nodes;
                width = _connectivityGraph.Width;
                height = _connectivityGraph.Height;
            }

            if (graphToDraw != null && width > 0 && height > 0 && _renderer != null)
            {
                var mf = _renderer.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return;
                
                var localBounds = mf.sharedMesh.bounds;
                float stepX = localBounds.size.x / width;
                float stepY = localBounds.size.y / height;
                
                Vector3 centerWS = _renderer.transform.position;
                Vector3 toCamera = centerWS;
                if (Camera.main != null)
                {
                    toCamera = Camera.main.transform.position - centerWS;
                    toCamera.y = 0;
                    if (toCamera.sqrMagnitude > 0.001f) toCamera.Normalize(); else toCamera = -_renderer.transform.forward;
                }
                Quaternion billboardRot = Quaternion.LookRotation(toCamera, Vector3.up);

                Gizmos.matrix = Matrix4x4.identity;
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (graphToDraw[y * width + x])
                        {
                            Vector3 localPos = new Vector3(
                                localBounds.min.x + (x + 0.5f) * stepX, 
                                localBounds.min.y + (y + 0.5f) * stepY, 
                                0);
                            
                            Vector3 scaledPos = new Vector3(
                                localPos.x * _renderer.transform.lossyScale.x, 
                                localPos.y * _renderer.transform.lossyScale.y, 
                                localPos.z * _renderer.transform.lossyScale.z);
                            
                            Vector3 worldPos = centerWS + billboardRot * scaledPos;

                            Gizmos.color = new Color(0, 1, 0, 0.5f); // Green for connected
                            Gizmos.DrawSphere(worldPos, Mathf.Max(stepX * Mathf.Abs(_renderer.transform.lossyScale.x), stepY * Mathf.Abs(_renderer.transform.lossyScale.y)) * 0.4f);
                        }
                    }
                }
            }
        }

        public float ApplyWound(RaycastHit hit, Vector3 trueHitNormal, float radius, float weaponPenetration, Vector3 hitVelocity, out bool isEssentialHit, out bool isBleeding, out float resistancePercent, out float damage)
        {
            isEssentialHit = false;
            isBleeding = false;
            resistancePercent = 0f;
            damage = weaponPenetration;

            Vector3 localPoint;
            Vector2 uv;
            GetWoundProjection(hit.point, out localPoint, out uv);

            Wound wound = new Wound
            {
                Position = uv, // Store UV in position
                LocalPoint = localPoint,
                Normal = trueHitNormal,        
                Radius = radius,
                Penetration = damage, // we store the effective damage done by the weapon into the wound pool
                Intensity = 1f,
                VFX = null
            };
            
            var newDepth = AddWound(wound, trueHitNormal, out isEssentialHit, out isBleeding, out resistancePercent, out damage);

            OnWoundCreated?.Invoke(wound, hit);
            return newDepth;
        }
    }
}