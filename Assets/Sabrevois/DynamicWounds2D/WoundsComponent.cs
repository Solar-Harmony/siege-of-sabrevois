using System;
using System.Collections;
using System.Collections.Generic;
using Sabrevois.Utils;
using UnityEngine;
using Zenject;

namespace SolarHarmony.DynamicWounds2D
{
    public class WoundVFXTracker : MonoBehaviour
    {
        public WoundsComponent Source;
        public Vector3 LocalPoint;

        private void LateUpdate()
        {
            if (Source != null && Source.gameObject.activeInHierarchy)
                transform.position = Source.GetBillboardWorldPosition(LocalPoint);
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
        public float Gravity;
        public GameObject VFX;
    }

    public class WoundsComponent : MonoBehaviour
    {
        private const int MaxWounds = 300;
        private const int GridResolution = 64;

        public event Action<Wound, RaycastHit> OnWoundCreated;
        public event Action OnVisibilityChanged;
        public event Action<GameObject, Vector3> OnLimbSevered;

        public static event Action<WoundsComponent, Wound, RaycastHit> OnAnyWoundCreated;
        public static event Action<WoundsComponent, GameObject, Vector3> OnAnyLimbSevered;

        [Inject] private GlobalWoundManager _woundManager;

        private List<Wound> _wounds = new List<Wound>();
        private int _sliceIndex = -1;

        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private GameObject _severeWoundVFX;
        [SerializeField] private GameObject _bloodPrefab;
        [SerializeField] private GameObject _woundImpactVFX;
        [SerializeField] private GameObject _bloodPoolPrefab;
        [SerializeField] private float _bloodPoolGrowthDuration = 5f;
        [SerializeField] private float _bloodPoolMinSize = 0.8f;
        [SerializeField] private float _bloodPoolMaxSize = 1.4f;
        [SerializeField] private float _bloodVFXDepthThreshold = 1.0f;
        [SerializeField] private float _minBleedDuration = 1.0f;
        [SerializeField] private float _maxBleedDuration = 4.0f;
        [SerializeField] private float _hitImpulseStrength = 0.4f;
        [SerializeField] private Color _bloodColor = new Color(0.5f, 0f, 0f, 1f);
        [SerializeField] private CharacterAtlasData _atlasData;
        [SerializeField] private LayerMask _groundLayerMask = -1;

        private MaterialPropertyBlock _mpb;
        private Camera _mainCamera;
        private MeshFilter _meshFilter;
        private IWoundHost _host;
        private ISeveredPartFactory _severedPartFactory;
        private Coroutine _hitReactionCoroutine;
        private Bounds _initialLocalBounds;
        private float _maxWoundPenetration;
        private bool _deathHandled;
        private float _woundAnimationTime;

        private bool[] _liveGraph;
        private int _graphWidth;
        private int _graphHeight;
        private float _visibleHeightFraction = 1f;
        private int _visibleMinY;
        private int _visibleMaxY;
        private int _lastHitBodyPartIndex = -1;

        private static readonly HashSet<WoundsComponent> s_lookedAtComponents = new HashSet<WoundsComponent>();
        private static int s_lastLookFrame;

        public float VisibleHeightFraction => _visibleHeightFraction;
        public float VisibleBottomFraction => _graphHeight > 0 ? (float)_visibleMinY / _graphHeight : 0f;
        public Bounds InitialLocalBounds => _initialLocalBounds;
        public MeshRenderer Renderer => _renderer;
        public CharacterAtlasData AtlasData => _atlasData;
        public int LastHitBodyPartIndex => _lastHitBodyPartIndex;

        public static WoundsComponent LookedAtWoundsComponent =>
            s_lookedAtComponents.Count > 0 ? new List<WoundsComponent>(s_lookedAtComponents)[0] : null;

        public List<Wound> WoundList => _wounds;
        public float MaxWoundPenetration => _maxWoundPenetration;
        public bool IsBleeding =>
            _maxWoundPenetration >= _bloodVFXDepthThreshold && _host is { IsDead: false };
        public bool IsHostDead => _host is { IsDead: true };
        public IWoundHost WoundHost => _host;
        public bool[] LiveGraph => _liveGraph;
        public int GraphWidth => _graphWidth;
        public int GraphHeight => _graphHeight;
        public int VisibleMinY => _visibleMinY;
        public int VisibleMaxY => _visibleMaxY;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _mainCamera = Camera.main;

            if (_atlasData != null)
            {
                Sprite layer0Sprite = _atlasData.LayerSprites != null && _atlasData.LayerSprites.Count > 0
                    ? _atlasData.LayerSprites[0]
                    : null;

                if (_renderer != null)
                {
                    _meshFilter = _renderer.GetComponent<MeshFilter>();
                    if (_meshFilter != null && layer0Sprite != null)
                    {
                        bool needsCustomMesh = _atlasData.BodyPartsMask != null
                            || _atlasData.LayerSprites.Count > 1;
                        if (needsCustomMesh)
                        {
                            var mesh = BodyPartMeshBuilder.BuildFromLayer0Sprite(layer0Sprite, _meshFilter.mesh);
                            if (mesh != null)
                                _meshFilter.mesh = mesh;
                        }
                    }
                }

                if (_atlasData.BodyPartsMask != null)
                {
                    _liveGraph = BodyPartMeshBuilder.GenerateConnectivityGrid(
                        _atlasData.BodyPartsMask, GridResolution);
                }
                else if (layer0Sprite != null)
                {
                    _liveGraph = BodyPartMeshBuilder.GenerateConnectivityGridFromAlpha(
                        _atlasData.SourceTexture, layer0Sprite, GridResolution);
                }

                if (_liveGraph != null)
                {
                    _graphWidth = GridResolution;
                    _graphHeight = GridResolution;
                    _visibleMaxY = _graphHeight - 1;
                }
                else
                {
                    Debug.LogWarning("[WoundsComponent] Grid generation returned null.", this);
                }
            }

            ApplyAtlasTexture();

            if (_renderer != null)
            {
                if (_meshFilter == null)
                    _meshFilter = _renderer.GetComponent<MeshFilter>();
                if (_meshFilter != null && _meshFilter.sharedMesh != null)
                    _initialLocalBounds = _meshFilter.sharedMesh.bounds;
            }

            if (_atlasData == null && _meshFilter != null && _meshFilter.sharedMesh != null)
                _initialLocalBounds = _meshFilter.sharedMesh.bounds;

            _host = GetComponentInParent<IWoundHost>();
            if (_host != null) _host.OnDeathComplete += HandleDeathComplete;

            _severedPartFactory = GetComponent<ISeveredPartFactory>();
            if (_severedPartFactory == null)
                _severedPartFactory = GetComponentInChildren<ISeveredPartFactory>();
            if (_severedPartFactory == null)
                _severedPartFactory = GetComponentInParent<ISeveredPartFactory>();

            ApplyEditorPropertyBlock();
        }

        private void Start()
        {
            if (_woundManager == null)
                _woundManager = GlobalWoundManager.Instance;

            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_mpb);

                if (_woundManager != null)
                {
                    _sliceIndex = _woundManager.RequestSlice();
                    if (_sliceIndex >= 0)
                        _mpb.SetFloat("_WoundSliceIndex", _sliceIndex);
                }

                if (_atlasData != null)
                {
                    var deltas = _atlasData.GetLayerUVDeltas();
                    if (deltas != null)
                    {
                        int n = deltas.Length;
                        _mpb.SetVector("_LayerUV00_01", new Vector4(
                            deltas[0].x, deltas[0].y,
                            n > 1 ? deltas[1].x : 0f, n > 1 ? deltas[1].y : 0f));
                        _mpb.SetVector("_LayerUV02_03", n >= 3
                            ? new Vector4(deltas[2].x, deltas[2].y, n > 3 ? deltas[3].x : 0f, n > 3 ? deltas[3].y : 0f)
                            : Vector4.zero);
                        _mpb.SetVector("_LayerUV04_05", n >= 5
                            ? new Vector4(deltas[4].x, deltas[4].y, n > 5 ? deltas[5].x : 0f, n > 5 ? deltas[5].y : 0f)
                            : Vector4.zero);
                        _mpb.SetVector("_LayerUV06_07", n >= 7
                            ? new Vector4(deltas[6].x, deltas[6].y, n > 7 ? deltas[7].x : 0f, n > 7 ? deltas[7].y : 0f)
                            : Vector4.zero);
                    }

                    if (_atlasData.NormalMap != null)
                        _mpb.SetTexture("_NormalMap", _atlasData.NormalMap);
                    if (_atlasData.SmoothnessMap != null)
                        _mpb.SetTexture("_SmoothnessMap", _atlasData.SmoothnessMap);
                    if (_atlasData.GlowMap != null)
                        _mpb.SetTexture("_GlowMap", _atlasData.GlowMap);
                }

                _renderer.SetPropertyBlock(_mpb);
            }
        }

        private void ApplyAtlasTexture()
        {
            if (_atlasData == null || _atlasData.SourceTexture == null) return;
            if (_renderer == null) return;
            if (_renderer.sharedMaterial == null) return;

            _renderer.sharedMaterial.mainTexture = _atlasData.SourceTexture;
        }

        private void ApplyEditorPropertyBlock()
        {
            if (_renderer == null || _atlasData == null || _atlasData.SourceTexture == null)
                return;

            _mpb.SetFloat("_EnableBillboard", 0f);
            _mpb.SetTexture("_MainTex", _atlasData.SourceTexture);
            _mpb.SetInt("_LayerCount", _atlasData.LayerCount);
            if (_atlasData.NormalMap != null)
                _mpb.SetTexture("_NormalMap", _atlasData.NormalMap);
            if (_atlasData.SmoothnessMap != null)
                _mpb.SetTexture("_SmoothnessMap", _atlasData.SmoothnessMap);
            if (_atlasData.GlowMap != null)
                _mpb.SetTexture("_GlowMap", _atlasData.GlowMap);

            var deltas = _atlasData.GetLayerUVDeltas();
            if (deltas != null)
            {
                int n = deltas.Length;
                _mpb.SetVector("_LayerUV00_01", new Vector4(
                    deltas[0].x, deltas[0].y,
                    n > 1 ? deltas[1].x : 0f, n > 1 ? deltas[1].y : 0f));
                _mpb.SetVector("_LayerUV02_03", n >= 3
                    ? new Vector4(deltas[2].x, deltas[2].y, n > 3 ? deltas[3].x : 0f, n > 3 ? deltas[3].y : 0f)
                    : Vector4.zero);
                _mpb.SetVector("_LayerUV04_05", n >= 5
                    ? new Vector4(deltas[4].x, deltas[4].y, n > 5 ? deltas[5].x : 0f, n > 5 ? deltas[5].y : 0f)
                    : Vector4.zero);
                _mpb.SetVector("_LayerUV06_07", n >= 7
                    ? new Vector4(deltas[6].x, deltas[6].y, n > 7 ? deltas[7].x : 0f, n > 7 ? deltas[7].y : 0f)
                    : Vector4.zero);
            }

            _renderer.SetPropertyBlock(_mpb);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_renderer == null || _atlasData == null || _atlasData.SourceTexture == null)
                return;

            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            ApplyAtlasTexture();
            ApplyEditorPropertyBlock();
        }
#endif

        private void OnDestroy()
        {
            if (_host != null)
                _host.OnDeathComplete -= HandleDeathComplete;
            ReleaseWoundSlice();
        }

        public void ReleaseWoundSlice()
        {
            if (_woundManager == null)
                _woundManager = GlobalWoundManager.Instance;

            if (_woundManager != null && _sliceIndex >= 0)
            {
                _woundManager.ReleaseSlice(_sliceIndex);
                _sliceIndex = -1;
            }
        }

        public void SetShaderBillboardEnabled(bool enabled)
        {
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat("_EnableBillboard", enabled ? 1.0f : 0.0f);
                _renderer.SetPropertyBlock(_mpb);
            }
        }

        private void Update()
        {
            if (_renderer != null && _woundAnimationTime < 3f)
            {
                _woundAnimationTime += Time.deltaTime;
                _mpb.SetFloat("_WoundTime", _woundAnimationTime);
                _renderer.SetPropertyBlock(_mpb);
            }

            if (s_lastLookFrame != Time.frameCount && Camera.main != null)
            {
                s_lastLookFrame = Time.frameCount;
                s_lookedAtComponents.Clear();
                Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
                {
                    var root = hit.collider.transform.root;
                    foreach (var wc in root.GetComponentsInChildren<WoundsComponent>())
                        s_lookedAtComponents.Add(wc);
                }
            }

            if (_host == null || _host.IsDead) return;
            if (_maxWoundPenetration < _bloodVFXDepthThreshold) return;

            for (int i = 0; i < _wounds.Count; i++)
            {
                Wound w = _wounds[i];
                if (w.Penetration < _maxWoundPenetration - 0.05f) continue;
                if (w.VFX != null) continue;
                if (VFXService.Instance == null || _renderer == null) continue;

                bool tooClose = false;
                for (int j = 0; j < _wounds.Count; j++)
                {
                    if (_wounds[j].VFX != null && Vector3.Distance(w.LocalPoint, _wounds[j].LocalPoint) < 0.25f)
                    {
                        tooClose = true;
                        w.VFX = _wounds[j].VFX;
                        break;
                    }
                }

                if (!tooClose)
                {
                    w.VFX = VFXService.Instance.Spawn(VFXType.Smoke);
                    if (w.VFX != null)
                    {
                        w.VFX.transform.SetParent(_renderer.transform);
                        w.VFX.transform.localPosition = w.LocalPoint;
                        VFXService.SetShapeRadius(w.VFX, w.Radius);

                        Vector3 localNormal = _renderer.transform.InverseTransformDirection(w.Normal);
                        if (localNormal.sqrMagnitude > 0.001f)
                            w.VFX.transform.localRotation = Quaternion.LookRotation(localNormal);

                        var severePS = w.VFX.GetComponent<ParticleSystem>();
                        if (severePS != null) severePS.Play(true);
                        VFXService.AttachAutoReturn(w.VFX, VFXService.Instance, VFXType.Smoke);
                    }
                }

                _wounds[i] = w;
            }
        }

        private IEnumerator HitReactionRoutine(Vector2 hitUV, Vector3 impactNormal, float uvFalloffRadius)
        {
            if (_renderer == null) yield break;

            float duration = 0.2f;
            float elapsed = 0f;

            float hitX = (hitUV.x - 0.5f) * 2f;
            float hitY = (hitUV.y - 0.5f) * 2f;

            Vector3 pushDir = -impactNormal;
            pushDir.y = 0;
            if (pushDir.sqrMagnitude > 0.001f) pushDir = pushDir.normalized;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float strength = Mathf.Sin(t * Mathf.PI) * _hitImpulseStrength;

                if (_host != null)
                    _host.ApplyMovementImpulse(pushDir, strength * Time.deltaTime * 2.5f);

                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetVector("_HitImpulse", new Vector4(hitX, hitY, strength, 0f));
                _mpb.SetVector("_HitMaskParams", new Vector4(hitUV.x, hitUV.y, uvFalloffRadius, 0f));
                _renderer.SetPropertyBlock(_mpb);

                yield return null;
            }

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetVector("_HitImpulse", Vector4.zero);
            _mpb.SetVector("_HitMaskParams", Vector4.zero);
            _renderer.SetPropertyBlock(_mpb);

            _hitReactionCoroutine = null;
        }

        public Vector3 GetBillboardWorldPosition(Vector3 localPoint)
        {
            if (_renderer == null) return transform.TransformPoint(localPoint);

            if (_mainCamera != null)
            {
                Vector3 centerWS = _renderer.transform.position;
                Vector3 viewDir = _mainCamera.transform.position - centerWS;
                viewDir.y = 0;
                if (viewDir.sqrMagnitude > 0.001f) viewDir.Normalize();
                else viewDir = new Vector3(0, 0, -1);

                Vector3 upWS = Vector3.up;
                Vector3 rightWS = Vector3.Cross(viewDir, upWS);
                Vector3 forwardWS = -viewDir;

                Vector3 scale = _renderer.transform.lossyScale;
                return centerWS + rightWS * (localPoint.x * scale.x)
                               + upWS * (localPoint.y * scale.y)
                               + forwardWS * (localPoint.z * scale.z);
            }

            return _renderer.transform.TransformPoint(localPoint);
        }

        private static RaycastHit[] _poolHits = new RaycastHit[16];

        private void HandleDeathComplete()
        {
            if (_deathHandled) return;
            _deathHandled = true;

            if (_renderer != null)
                _renderer.transform.localPosition = Vector3.zero;

            if (_bloodPoolPrefab == null) return;
            List<Vector3> spawnedPositions = new List<Vector3>();

            foreach (var w in _wounds)
            {
                if (w.Penetration < 0.1f) continue;

                Vector3 worldPos = GetBillboardWorldPosition(w.LocalPoint);
                bool tooClose = false;
                foreach (var sp in spawnedPositions)
                {
                    if (Vector3.Distance(sp, worldPos) < 0.5f) { tooClose = true; break; }
                }
                if (tooClose) continue;

                int hitCount = Physics.RaycastNonAlloc(
                    worldPos + Vector3.up * 0.5f, Vector3.down, _poolHits, 5f,
                    _groundLayerMask, QueryTriggerInteraction.Ignore);

                RaycastHit bestHit = default;
                bool foundHit = false;
                for (int i = 0; i < hitCount; i++)
                {
                    if (_poolHits[i].collider.transform.root != transform.root)
                    {
                        if (!foundHit || _poolHits[i].distance < bestHit.distance)
                        {
                            bestHit = _poolHits[i];
                            foundHit = true;
                        }
                    }
                }

                if (foundHit)
                {
                    var pool = Instantiate(_bloodPoolPrefab, bestHit.point + Vector3.up * 0.01f,
                        Quaternion.FromToRotation(Vector3.up, bestHit.normal));
                    ApplyBloodColorToPool(pool);
                    StartCoroutine(GrowBloodPoolRoutine(pool.transform, _bloodPoolGrowthDuration));
                    spawnedPositions.Add(worldPos);
                }
            }
        }

        public void PlayBloodVFXWorld(Vector3 worldPosition, Vector3 worldNormal,
            Vector3 hitVelocity = default, float penetrationRatio = 0.5f)
        {
            Vector3 localPoint = _renderer != null
                ? _renderer.transform.InverseTransformPoint(worldPosition)
                : transform.InverseTransformPoint(worldPosition);
            PlayBloodVFX(localPoint, worldNormal, hitVelocity, penetrationRatio);
        }

        public void PlayBloodVFX(Vector3 localPoint, Vector3 worldNormal,
            Vector3 hitVelocity = default, float penetrationRatio = 0.5f,
            float woundRadius = 0f)
        {
            if (VFXService.Instance == null || _renderer == null) return;
            if (_host != null && _host.IsDead) return;

            var go = VFXService.Instance.Spawn(VFXType.Blood);
            if (go == null) return;

            foreach (var oldTracker in go.GetComponents<WoundVFXTracker>())
                Destroy(oldTracker);

            var instance = go.GetComponent<ParticleSystem>();
            if (instance == null) { VFXService.Instance.Despawn(go, VFXType.Blood); return; }

            VFXService.SetShapeRadius(go, woundRadius);

            var goTransform = instance.transform;
            goTransform.position = GetBillboardWorldPosition(localPoint);
            goTransform.SetParent(null);

            var tracker = instance.gameObject.AddComponent<WoundVFXTracker>();
            tracker.Source = this;
            tracker.LocalPoint = localPoint;

            if (worldNormal.sqrMagnitude > 0.001f)
                instance.transform.rotation = Quaternion.LookRotation(worldNormal);

            var main = instance.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(_bloodColor);

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

        private void PlayWoundImpactVFX(Vector3 localPoint, Vector2 hitUV, Vector3 worldNormal,
            float layerDepth, float woundRadius = 0f)
        {
            if (VFXService.Instance == null || _renderer == null) return;

            var instance = VFXService.Instance.Spawn(VFXType.WoundImpact);
            if (instance == null) return;

            foreach (var oldTracker in instance.GetComponents<WoundVFXTracker>())
                Destroy(oldTracker);

            instance.transform.position = GetBillboardWorldPosition(localPoint);
            instance.transform.SetParent(null);
            VFXService.SetShapeRadius(instance, woundRadius);

            var tracker = instance.AddComponent<WoundVFXTracker>();
            tracker.Source = this;
            tracker.LocalPoint = localPoint;

            var impact = instance.GetComponent<WoundImpact>();
            if (impact != null)
            {
                impact.ResetForPool();
                impact.Initialize(hitUV, _atlasData, layerDepth, worldNormal);
            }

            VFXService.AttachAutoReturn(instance, VFXService.Instance, VFXType.WoundImpact);
        }

        private void ApplyBloodColorToPool(GameObject pool)
        {
            if (pool == null) return;
            var poolRenderer = pool.GetComponent<MeshRenderer>();
            if (poolRenderer != null)
                poolRenderer.material.SetColor("_Tint", _bloodColor);
        }

        private IEnumerator GrowBloodPoolRoutine(Transform pool, float duration)
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

        private IEnumerator StopBleedingRoutine(ParticleSystem vfx, float duration)
        {
            yield return new WaitForSeconds(duration);

            if (vfx != null)
            {
                var em = vfx.emission;
                em.enabled = false;
                if (VFXService.Instance != null)
                    VFXService.Instance.Despawn(vfx.gameObject, VFXType.Blood);
                else
                    Destroy(vfx.gameObject, vfx.main.startLifetime.constantMax + 1f);
            }
        }

        private void CheckAndProcessSeveredLimbs(Vector2 hitUV, float radius, float depth, Vector3 hitDirection)
        {
            if (_liveGraph == null || depth <= 0f) return;

            int layerCount = _atlasData != null ? _atlasData.LayerCount : 2;
            if (layerCount <= 0) layerCount = 2;

            int[] dirX = { -1, 1, 0, 0, -1, 1, -1, 1 };
            int[] dirY = { 0, 0, -1, 1, -1, -1, 1, 1 };

            float scaleX = 1f;
            float scaleY = 1f;
            if (_renderer != null && _meshFilter != null && _meshFilter.sharedMesh != null)
            {
                var localBounds = _meshFilter.sharedMesh.bounds;
                scaleX = Mathf.Abs(localBounds.size.x * _renderer.transform.lossyScale.x);
                scaleY = Mathf.Abs(localBounds.size.y * _renderer.transform.lossyScale.y);
            }

            List<int> depthKilledNodes = new List<int>();

            for (int y = 0; y < _graphHeight; y++)
            {
                for (int x = 0; x < _graphWidth; x++)
                {
                    int nodeIndex = y * _graphWidth + x;
                    if (!_liveGraph[nodeIndex]) continue;

                    float nodeDepth = 0f;
                    foreach (var w in _wounds)
                    {
                        float rX = w.Radius / scaleX;
                        float rY = w.Radius / scaleY;
                        float wRadius = Mathf.Max(rX, rY);

                        if (Vector2.Distance(w.Position, new Vector2(
                            (x + 0.5f) / _graphWidth, (y + 0.5f) / _graphHeight)) <= wRadius)
                        {
                            nodeDepth += w.Penetration;
                        }
                    }

                    float threshold = layerCount > 1 ? layerCount : 0.01f;
                    if (nodeDepth >= threshold)
                    {
                        _liveGraph[nodeIndex] = false;
                        depthKilledNodes.Add(nodeIndex);
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

                        for (int j = 0; j < 8; j++)
                        {
                            int nx = cx + dirX[j];
                            int ny = cy + dirY[j];

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

            List<int> erodedNodes = null;

            if (components.Count <= 1)
            {
                if (components.Count == 1)
                {
                    erodedNodes = new List<int>();
                    for (int i = 0; i < _liveGraph.Length; i++)
                    {
                        if (!_liveGraph[i]) continue;
                        int cx = i % _graphWidth;
                        int cy = i / _graphWidth;
                        int liveNeighbors = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = cx + dx;
                                int ny = cy + dy;
                                if (nx >= 0 && nx < _graphWidth && ny >= 0 && ny < _graphHeight)
                                {
                                    if (_liveGraph[ny * _graphWidth + nx])
                                        liveNeighbors++;
                                }
                            }
                        }
                        if (liveNeighbors <= 2)
                        {
                            _liveGraph[i] = false;
                            erodedNodes.Add(i);
                        }
                    }

                    if (erodedNodes.Count > 0)
                    {
                        components.Clear();
                        Array.Clear(visited, 0, visited.Length);

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
                                    int qx = curr % _graphWidth;
                                    int qy = curr / _graphWidth;

                                    for (int j = 0; j < 8; j++)
                                    {
                                        int nx = qx + dirX[j];
                                        int ny = qy + dirY[j];

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
                    }
                }

                if (components.Count <= 1) return;
            }

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

            var largestComponent = components[largestIndex];
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            foreach (int nIndex in largestComponent)
            {
                int y = nIndex / _graphWidth;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            float heightFraction = (float)(maxY - minY + 1) / _graphHeight;

            for (int i = 0; i < components.Count; i++)
            {
                if (i == largestIndex) continue;

                List<Vector2Int> severedNodes = new List<Vector2Int>();
                foreach (int nIndex in components[i])
                {
                    severedNodes.Add(new Vector2Int(nIndex % _graphWidth, nIndex / _graphWidth));
                    _liveGraph[nIndex] = false;
                }

                if (components[i].Count >= 5)
                {
                    var severedPart = SpriteSlicer.CreateSlicedPart(
                        _renderer, severedNodes, _graphWidth, _initialLocalBounds,
                        _severedPartFactory, hitDirection, _woundManager, _groundLayerMask,
                        _atlasData);

                    if (severedPart != null)
                    {
                        // Centroid of the severed region in the source mesh's local space
                        float totalX = 0f, totalY = 0f;
                        foreach (var sn in severedNodes)
                        {
                            totalX += sn.x;
                            totalY += sn.y;
                        }
                        float avgGridX = totalX / severedNodes.Count;
                        float avgGridY = totalY / severedNodes.Count;
                        float u = avgGridX / _graphWidth;
                        float v = avgGridY / _graphHeight;

                        Vector3 centroid = new Vector3(
                            _initialLocalBounds.min.x + u * _initialLocalBounds.size.x,
                            _initialLocalBounds.min.y + v * _initialLocalBounds.size.y,
                            0f);

                        if (VFXService.Instance != null)
                        {
                            var bloodGO = VFXService.Instance.Spawn(VFXType.Blood);
                            if (bloodGO != null)
                            {
                                bloodGO.transform.SetParent(severedPart.transform);
                                bloodGO.transform.localPosition = centroid;

                                float severedRadius = Mathf.Sqrt(severedNodes.Count / Mathf.PI)
                                    * Mathf.Max(_initialLocalBounds.size.x, _initialLocalBounds.size.y) / _graphWidth;
                                VFXService.SetShapeRadius(bloodGO, severedRadius);

                                foreach (var oldTracker in bloodGO.GetComponents<WoundVFXTracker>())
                                    Destroy(oldTracker);

                                var ps = bloodGO.GetComponent<ParticleSystem>();
                                if (ps != null)
                                {
                                    var main = ps.main;
                                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                                    main.startColor = new ParticleSystem.MinMaxGradient(_bloodColor);
                                    main.loop = true;
                                    ps.Play(true);
                                    StartCoroutine(StopBleedingRoutine(ps, _maxBleedDuration));
                                }
                            }
                        }

                        OnLimbSevered?.Invoke(severedPart, hitDirection);
                        OnAnyLimbSevered?.Invoke(this, severedPart, hitDirection);
                    }
                }

                if (_sliceIndex >= 0 && _woundManager != null && _meshFilter != null &&
                    _meshFilter.sharedMesh != null)
                {
                    var localBounds = _meshFilter.sharedMesh.bounds;
                    var quadSize = new Vector2(
                        localBounds.size.x * _renderer.transform.lossyScale.x,
                        localBounds.size.y * _renderer.transform.lossyScale.y);
                    float worldRadius = Mathf.Max(quadSize.x / _graphWidth, quadSize.y / _graphHeight) * 6f;

                    foreach (var sn in severedNodes)
                    {
                        float u = (sn.x + 0.5f) / _graphWidth;
                        float v = (sn.y + 0.5f) / _graphHeight;
                        _woundManager.AddWoundSplat(_sliceIndex, new Vector2(u, v), worldRadius, 1000.0f, quadSize, 1f);
                    }
                }
            }

            if (erodedNodes != null && erodedNodes.Count > 0 && _sliceIndex >= 0 &&
                _woundManager != null && _meshFilter != null && _meshFilter.sharedMesh != null)
            {
                var localBounds = _meshFilter.sharedMesh.bounds;
                var quadSize = new Vector2(
                    localBounds.size.x * _renderer.transform.lossyScale.x,
                    localBounds.size.y * _renderer.transform.lossyScale.y);
                float worldRadius = Mathf.Max(quadSize.x / _graphWidth, quadSize.y / _graphHeight) * 1.2f;

                foreach (int nIndex in erodedNodes)
                {
                    float u = (nIndex % _graphWidth + 0.5f) / _graphWidth;
                    float v = (nIndex / _graphHeight + 0.5f) / _graphHeight;
                    _woundManager.AddWoundSplat(_sliceIndex, new Vector2(u, v), worldRadius, 1000.0f, quadSize, 1f);
                }
            }

            if (depthKilledNodes.Count > 0 && _sliceIndex >= 0 && _woundManager != null &&
                _meshFilter != null && _meshFilter.sharedMesh != null)
            {
                var localBounds = _meshFilter.sharedMesh.bounds;
                var quadSize = new Vector2(
                    localBounds.size.x * _renderer.transform.lossyScale.x,
                    localBounds.size.y * _renderer.transform.lossyScale.y);
                float worldRadius = Mathf.Max(quadSize.x / _graphWidth, quadSize.y / _graphHeight) * 1.2f;

                foreach (int nIndex in depthKilledNodes)
                {
                    float u = (nIndex % _graphWidth + 0.5f) / _graphWidth;
                    float v = (nIndex / _graphHeight + 0.5f) / _graphHeight;
                    _woundManager.AddWoundSplat(_sliceIndex, new Vector2(u, v), worldRadius, 1000.0f, quadSize, 1f);
                }
            }

            _visibleHeightFraction = heightFraction;
            _visibleMinY = minY;
            _visibleMaxY = maxY;
            OnVisibilityChanged?.Invoke();
        }

        private void GetWoundProjection(Vector3 worldHitPoint, out Vector3 localPoint, out Vector2 uv)
        {
            if (_mainCamera == null)
            {
                localPoint = _renderer != null
                    ? _renderer.transform.InverseTransformPoint(worldHitPoint)
                    : transform.InverseTransformPoint(worldHitPoint);
                uv = Vector2.zero;
                return;
            }

            Vector3 cameraPos = _mainCamera.transform.position;
            Vector3 rayDir = (worldHitPoint - cameraPos).normalized;

            Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);

            if (_renderer != null)
            {
                if (_meshFilter != null && _meshFilter.sharedMesh != null)
                    localBounds = _meshFilter.sharedMesh.bounds;

                Vector3 toCamera = cameraPos - _renderer.transform.position;
                toCamera.y = 0;
                Vector3 planeNormal = -toCamera.normalized;

                if (planeNormal.sqrMagnitude < 0.001f)
                    planeNormal = -_renderer.transform.forward;

                Plane quadPlane = new Plane(planeNormal, _renderer.transform.position);

                if (quadPlane.Raycast(new Ray(cameraPos, rayDir), out float enter))
                {
                    Vector3 intersectPoint = cameraPos + rayDir * enter;

                    Quaternion billboardRot = Quaternion.LookRotation(planeNormal, Vector3.up);
                    Vector3 offset = intersectPoint - _renderer.transform.position;
                    Vector3 unrotatedOffset = Quaternion.Inverse(billboardRot) * offset;

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
                localPoint = transform.InverseTransformPoint(worldHitPoint);
            }

            float u = Mathf.InverseLerp(localBounds.min.x, localBounds.max.x, localPoint.x);
            float v = Mathf.InverseLerp(localBounds.min.y, localBounds.max.y, localPoint.y);
            uv = new Vector2(u, v);
        }

        private void GetDirectProjection(Vector3 worldHitPoint, out Vector3 localPoint, out Vector2 uv)
        {
            Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);

            if (_renderer != null)
            {
                localPoint = _renderer.transform.InverseTransformPoint(worldHitPoint);
                if (_meshFilter != null && _meshFilter.sharedMesh != null)
                    localBounds = _meshFilter.sharedMesh.bounds;
            }
            else
            {
                localPoint = transform.InverseTransformPoint(worldHitPoint);
            }

            float u = Mathf.InverseLerp(localBounds.min.x, localBounds.max.x, localPoint.x);
            float v = Mathf.InverseLerp(localBounds.min.y, localBounds.max.y, localPoint.y);
            uv = new Vector2(u, v);
        }

        public float ApplySlashWound(Vector3 worldPos, Vector3 slashDirection, float radius,
            float weaponPenetration, out bool isEssentialHit, out bool isBleeding,
            out float resistancePercent, out float damage)
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

            return AddWound(wound, slashDirection, out isEssentialHit, out isBleeding,
                out resistancePercent, out damage);
        }

        private float AddWound(Wound wound, Vector3 hitNormal, out bool isEssentialHit,
            out bool isBleeding, out float resistancePercent, out float damage)
        {
            isEssentialHit = false;
            isBleeding = false;
            resistancePercent = 0f;
            damage = wound.Penetration;

            Vector2 uv = wound.Position;
            TryMatchBodyPart(uv, out _, out isEssentialHit);

            float oldDepth = 0f;
            for (int i = 0; i < _wounds.Count; i++)
            {
                if (Vector2.Distance(_wounds[i].Position, uv) < _wounds[i].Radius * 1.5f)
                {
                    oldDepth += _wounds[i].Penetration;
                }
            }

            if (_host != null)
                resistancePercent = _host.GetResistanceAtDepth(oldDepth, _atlasData, _lastHitBodyPartIndex);

            damage = wound.Penetration * (1f - resistancePercent / 100f);
            damage = Mathf.Max(0, damage);

            wound.Penetration = damage;

            float newDepth = oldDepth + damage;

            // Wounds with zero effective penetration contribute nothing.
            if (damage > 0f)
            {
                wound.Penetration = damage;

                // Merge with a nearby existing wound instead of creating a
                // separate entry.  Sustained attacks on the same spot then
                // occupy one slot forever instead of filling the list and
                // forcing eviction of high-penetration wounds.
                int mergeIndex = -1;
                float mergeThreshold = wound.Radius * 0.5f;
                float bestDist = mergeThreshold;
                for (int i = 0; i < _wounds.Count; i++)
                {
                    float dist = Vector2.Distance(_wounds[i].Position, wound.Position);
                    if (dist < bestDist)
                    {
                        mergeIndex = i;
                        bestDist = dist;
                    }
                }

                if (mergeIndex >= 0)
                {
                    var existing = _wounds[mergeIndex];
                    float totalPen = existing.Penetration + damage;
                    existing.Position = (existing.Position * existing.Penetration
                                        + wound.Position * damage) / totalPen;
                    existing.Penetration = totalPen;
                    existing.Radius = Mathf.Max(existing.Radius, wound.Radius);
                    existing.LocalPoint = Vector3.Lerp(existing.LocalPoint, wound.LocalPoint,
                        damage / totalPen);
                    _wounds[mergeIndex] = existing;
                }
                else
                {
                    if (_wounds.Count >= MaxWounds)
                    {
                        int worstIndex = 0;
                        float worstPen = float.MaxValue;
                        for (int i = 0; i < _wounds.Count; i++)
                        {
                            if (_wounds[i].Penetration < worstPen)
                            {
                                worstPen = _wounds[i].Penetration;
                                worstIndex = i;
                            }
                        }
                        _wounds.RemoveAt(worstIndex);
                    }

                    _wounds.Add(wound);
                }

                if (damage > _maxWoundPenetration)
                    _maxWoundPenetration = damage;

                PlayWoundImpactVFX(wound.LocalPoint, wound.Position, hitNormal, oldDepth, wound.Radius);

                float ratio = 0f;
                if (newDepth >= _bloodVFXDepthThreshold)
                {
                    ratio = Mathf.Clamp01(newDepth / 3.0f);
                    PlayBloodVFX(wound.LocalPoint, hitNormal, default, ratio, wound.Radius);
                    _woundAnimationTime = 0f;
                    isBleeding = true;
                }

                if (_sliceIndex >= 0 && _woundManager != null && _meshFilter != null &&
                    _meshFilter.sharedMesh != null)
                {
                    var localBounds = _meshFilter.sharedMesh.bounds;
                    var quadSize = new Vector2(
                        localBounds.size.x * _renderer.transform.lossyScale.x,
                        localBounds.size.y * _renderer.transform.lossyScale.y);
                    _woundManager.AddWoundSplat(_sliceIndex, uv, wound.Radius, wound.Penetration,
                        quadSize, ratio > 0f ? 1f : 0f);
                }

                CheckAndProcessSeveredLimbs(uv, wound.Radius, newDepth, hitNormal);
            }

            if (_hitReactionCoroutine != null)
            {
                StopCoroutine(_hitReactionCoroutine);
                if (_renderer != null)
                {
                    _renderer.GetPropertyBlock(_mpb);
                    _mpb.SetVector("_HitImpulse", Vector4.zero);
                    _mpb.SetVector("_HitMaskParams", Vector4.zero);
                _mpb.SetColor("_BloodColor", _bloodColor);
                _mpb.SetFloat("_WoundTime", 0f);

                if (_atlasData != null && _atlasData.SourceTexture != null)
                    _mpb.SetTexture("_MainTex", _atlasData.SourceTexture);

                if (_atlasData != null)
                {
                    if (_atlasData.NormalMap != null)
                        _mpb.SetTexture("_NormalMap", _atlasData.NormalMap);
                    if (_atlasData.SmoothnessMap != null)
                        _mpb.SetTexture("_SmoothnessMap", _atlasData.SmoothnessMap);
                    if (_atlasData.GlowMap != null)
                        _mpb.SetTexture("_GlowMap", _atlasData.GlowMap);
                }

                _renderer.SetPropertyBlock(_mpb);
                }
            }

            if (_renderer != null && gameObject.activeInHierarchy && _host != null && !_host.IsDead)
            {
                float uvFalloffRadius = 0.4f;
                if (_meshFilter != null && _meshFilter.sharedMesh != null)
                {
                    var localBounds = _meshFilter.sharedMesh.bounds;
                    var quadSize = new Vector2(
                        Mathf.Abs(localBounds.size.x * _renderer.transform.lossyScale.x),
                        Mathf.Abs(localBounds.size.y * _renderer.transform.lossyScale.y));
                    float maxSize = Mathf.Max(quadSize.x, quadSize.y);
                    if (maxSize > 0.001f)
                        uvFalloffRadius = Mathf.Max(0.4f, wound.Radius / maxSize * 8f);
                }

                _hitReactionCoroutine = StartCoroutine(HitReactionRoutine(uv, hitNormal, uvFalloffRadius));
            }

            return newDepth;
        }

        private void OnDrawGizmos()
        {
            if (!s_lookedAtComponents.Contains(this)) return;
            bool[] graphToDraw = _liveGraph;
            int width = _graphWidth;
            int height = _graphHeight;

            if (graphToDraw == null || width <= 0 || height <= 0 || _renderer == null) return;
            if (_meshFilter == null || _meshFilter.sharedMesh == null) return;

            var localBounds = _meshFilter.sharedMesh.bounds;
            float stepX = localBounds.size.x / width;
            float stepY = localBounds.size.y / height;

            int step = Mathf.Max(1, Mathf.Max(width, height) / 32);

            Matrix4x4 worldMatrix;
            bool useActualTransform = _host != null && _host.IsDead;
            if (!useActualTransform && _mainCamera != null)
            {
                Vector3 centerWS = _renderer.transform.position;
                Vector3 toCamera = _mainCamera.transform.position - centerWS;
                toCamera.y = 0;
                if (toCamera.sqrMagnitude > 0.001f) toCamera.Normalize();
                else toCamera = -_renderer.transform.forward;
                Quaternion billboardRot = Quaternion.LookRotation(-toCamera, Vector3.up);
                worldMatrix = Matrix4x4.TRS(centerWS, billboardRot, _renderer.transform.lossyScale);
            }
            else
            {
                worldMatrix = _renderer.transform.localToWorldMatrix;
            }

            Gizmos.matrix = worldMatrix;
            Gizmos.color = new Color(0, 1, 0, 0.5f);

            float radius = Mathf.Max(stepX, stepY) * 0.4f;

            for (int y = 0; y < height; y += step)
            {
                for (int x = 0; x < width; x += step)
                {
                    if (graphToDraw[y * width + x])
                    {
                        Vector3 localPos = new Vector3(
                            localBounds.min.x + (x + 0.5f) * stepX,
                            localBounds.min.y + (y + 0.5f) * stepY,
                            0);
                        Gizmos.DrawSphere(localPos, radius);
                    }
                }
            }
        }

        public float ApplyWound(RaycastHit hit, Vector3 trueHitNormal, float radius,
            float weaponPenetration, Vector3 hitVelocity, out bool isEssentialHit,
            out bool isBleeding, out float resistancePercent, out float damage)
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
                Position = uv,
                LocalPoint = localPoint,
                Normal = trueHitNormal,
                Radius = radius,
                Penetration = damage,
                Intensity = 1f,
                VFX = null
            };

            var newDepth = AddWound(wound, trueHitNormal, out isEssentialHit, out isBleeding,
                out resistancePercent, out damage);

            OnWoundCreated?.Invoke(wound, hit);
            OnAnyWoundCreated?.Invoke(this, wound, hit);
            return newDepth;
        }

        public float ApplyWoundAtPoint(Vector3 worldPoint, Vector3 hitNormal, float radius,
            float weaponPenetration, out bool isEssentialHit, out bool isBleeding,
            out float resistancePercent, out float damage)
        {
            isEssentialHit = false;
            isBleeding = false;
            resistancePercent = 0f;
            damage = weaponPenetration;

            Vector3 localPoint;
            Vector2 uv;
            GetDirectProjection(worldPoint, out localPoint, out uv);

            Wound wound = new Wound
            {
                Position = uv,
                LocalPoint = localPoint,
                Normal = hitNormal,
                Radius = radius,
                Penetration = damage,
                Intensity = 1f,
                VFX = null
            };

            return AddWound(wound, hitNormal, out isEssentialHit, out isBleeding,
                out resistancePercent, out damage);
        }

        public bool TryMatchBodyPart(Vector2 uv, out int bodyPartIndex, out bool isEssential)
        {
            bodyPartIndex = -1;
            isEssential = false;

            var maskTex = _atlasData != null ? _atlasData.BodyPartsMask : null;
            var mappings = _atlasData != null ? _atlasData.BodyPartMappings : null;

            if (maskTex != null && mappings != null && mappings.Count > 0)
            {
                Color hitColor = maskTex.GetPixelBilinear(uv.x, uv.y);
                float minDist = float.MaxValue;
                BodyPartMapping? bestMatch = null;
                int bestIndex = -1;

                for (int i = 0; i < mappings.Count; i++)
                {
                    var mapping = mappings[i];
                    float d = (mapping.Color.r - hitColor.r) * (mapping.Color.r - hitColor.r) +
                              (mapping.Color.g - hitColor.g) * (mapping.Color.g - hitColor.g) +
                              (mapping.Color.b - hitColor.b) * (mapping.Color.b - hitColor.b);
                    if (d < minDist)
                    {
                        minDist = d;
                        bestMatch = mapping;
                        bestIndex = i;
                    }
                }

                if (bestMatch.HasValue && minDist < 0.05f)
                {
                    bodyPartIndex = bestIndex;
                    isEssential = bestMatch.Value.IsEssential;
                    _lastHitBodyPartIndex = bestIndex;
                    return true;
                }

                _lastHitBodyPartIndex = -1;
                return false;
            }

            // No mask or empty mappings: cannot determine body part,
            // so treat as non-essential (non-lethal).  This also covers
            // decorative WoundsComponents like wings or clothing overlays
            // that share the root but have no body-part configuration.
            isEssential = false;
            _lastHitBodyPartIndex = -1;
            bodyPartIndex = -1;
            return false;
        }

        public bool TryMatchBodyPartAtWorld(Vector3 worldPoint, out int bodyPartIndex, out bool isEssential)
        {
            GetWoundProjection(worldPoint, out _, out Vector2 uv);
            return TryMatchBodyPart(uv, out bodyPartIndex, out isEssential);
        }

        public bool IsSolidAtWorldPoint(Vector3 worldPoint)
        {
            GetWoundProjection(worldPoint, out _, out Vector2 uv);
            return IsSolidAtUV(uv);
        }

        public float GetPlaneDepthAt(Vector3 worldPoint)
        {
            if (_renderer == null || _mainCamera == null) return float.MaxValue;

            Vector3 cameraPos = _mainCamera.transform.position;
            Vector3 rayDir = (worldPoint - cameraPos).normalized;

            Vector3 toCamera = cameraPos - _renderer.transform.position;
            toCamera.y = 0;
            if (toCamera.sqrMagnitude < 0.001f) return float.MaxValue;

            Vector3 planeNormal = -toCamera.normalized;
            Plane quadPlane = new Plane(planeNormal, _renderer.transform.position);

            if (quadPlane.Raycast(new Ray(cameraPos, rayDir), out float enter))
                return enter;

            return float.MaxValue;
        }

        private bool IsSolidAtUV(Vector2 uv)
        {
            const float uvMargin = 0.002f;
            if (uv.x < -uvMargin || uv.x > 1f + uvMargin || uv.y < -uvMargin || uv.y > 1f + uvMargin)
                return false;

            if (_liveGraph == null)
                return IsSpriteOpaqueAtUV(uv);

            int x = Mathf.FloorToInt(uv.x * _graphWidth);
            int y = Mathf.FloorToInt(uv.y * _graphHeight);
            x = Mathf.Clamp(x, 0, _graphWidth - 1);
            y = Mathf.Clamp(y, 0, _graphHeight - 1);
            if (!_liveGraph[y * _graphWidth + x]) return false;
            return IsSpriteOpaqueAtUV(uv);
        }

        private bool IsSpriteOpaqueAtUV(Vector2 uv)
        {
            if (_atlasData == null) return true;
            var sourceTex = _atlasData.SourceTexture;
            if (sourceTex == null || !sourceTex.isReadable) return true;

            var layerSprites = _atlasData.LayerSprites;
            if (layerSprites != null && layerSprites.Count > 0)
            {
                Sprite baseSprite = layerSprites[0];
                if (baseSprite != null)
                {
                    float texW = sourceTex.width;
                    float texH = sourceTex.height;

                    float baseU = baseSprite.rect.x / texW;
                    float baseV = baseSprite.rect.y / texH;
                    float baseWidth = baseSprite.rect.width / texW;
                    float baseHeight = baseSprite.rect.height / texH;

                    float texU = Mathf.Clamp01(baseU + uv.x * baseWidth);
                    float texV = Mathf.Clamp01(baseV + uv.y * baseHeight);

                    Color c = sourceTex.GetPixelBilinear(texU, texV);
                    return c.a >= 0.01f;
                }
            }

            return sourceTex.GetPixelBilinear(uv.x, uv.y).a >= 0.01f;
        }
    }
}
