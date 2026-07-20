using System.Collections.Generic;
using UnityEngine;

namespace Sabrevois.Utils
{
    public enum VFXType
    {
        Blood,
        WoundImpact,
        Smoke
    }

    public class VFXService : MonoBehaviour
    {
        public static VFXService Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private GameObject _bloodPrefab;
        [SerializeField] private GameObject _woundImpactPrefab;
        [SerializeField] private GameObject _smokePrefab;

        [Header("Pool Sizes")]
        [SerializeField] private int _bloodPoolSize = 8;
        [SerializeField] private int _woundImpactPoolSize = 5;

        [Header("Caps")]
        [SerializeField] private int _maxConcurrentWoundImpacts = 5;

        private readonly Queue<GameObject> _bloodPool = new Queue<GameObject>();
        private readonly Queue<GameObject> _woundImpactPool = new Queue<GameObject>();
        private readonly Queue<GameObject> _smokePool = new Queue<GameObject>();

        private readonly HashSet<GameObject> _activeWoundImpacts = new HashSet<GameObject>();

        private Transform _poolRoot;

        public int ActiveWoundImpactCount => _activeWoundImpacts.Count;

        private void Awake()
        {
            Instance = this;
            _poolRoot = new GameObject("VFXPool").transform;
            _poolRoot.SetParent(transform);
            _poolRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            Prewarm(_bloodPool, _bloodPrefab, _bloodPoolSize);
            Prewarm(_woundImpactPool, _woundImpactPrefab, _woundImpactPoolSize);
            Prewarm(_smokePool, _smokePrefab, 4);
        }

        private void Prewarm(Queue<GameObject> pool, GameObject prefab, int count)
        {
            if (prefab == null) return;
            for (int i = 0; i < count; i++)
            {
                var instance = Instantiate(prefab, _poolRoot);
                instance.SetActive(false);
                pool.Enqueue(instance);
            }
        }

        public GameObject Spawn(VFXType type)
        {
            switch (type)
            {
                case VFXType.Blood:
                    return SpawnFromPool(_bloodPool, _bloodPrefab, _bloodPoolSize);

                case VFXType.WoundImpact:
                    if (_activeWoundImpacts.Count >= _maxConcurrentWoundImpacts)
                        return null;
                    var impact = SpawnFromPool(_woundImpactPool, _woundImpactPrefab, _woundImpactPoolSize);
                    if (impact != null)
                        _activeWoundImpacts.Add(impact);
                    return impact;

                case VFXType.Smoke:
                    return SpawnFromPool(_smokePool, _smokePrefab, 4);

                default:
                    return null;
            }
        }

        public void Despawn(GameObject instance, VFXType type)
        {
            if (instance == null) return;

            var ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            var trackers = instance.GetComponents<PooledVFXTracker>();
            foreach (var t in trackers)
                Destroy(t);

            instance.transform.SetParent(_poolRoot);
            instance.SetActive(false);

            switch (type)
            {
                case VFXType.Blood:
                    _bloodPool.Enqueue(instance);
                    break;
                case VFXType.WoundImpact:
                    _activeWoundImpacts.Remove(instance);
                    _woundImpactPool.Enqueue(instance);
                    break;
                case VFXType.Smoke:
                    _smokePool.Enqueue(instance);
                    break;
            }
        }

        public static void SetShapeRadius(GameObject instance, float radius)
        {
            if (instance == null || radius <= 0f) return;

            var ps = instance.GetComponent<ParticleSystem>();
            if (ps == null) return;

            var shape = ps.shape;
            if (shape.enabled)
            {
                shape.radius = radius;
            }
        }

        public static void AttachAutoReturn(GameObject instance, VFXService service, VFXType type)
        {
            var tracker = instance.AddComponent<PooledVFXTracker>();
            tracker.Initialize(service, type);
        }

        private static GameObject SpawnFromPool(Queue<GameObject> pool, GameObject prefab, int prewarmSize)
        {
            if (pool.Count > 0)
            {
                var instance = pool.Dequeue();
                instance.SetActive(true);
                return instance;
            }

            if (prefab == null) return null;
            return Instantiate(prefab);
        }
    }

    public class PooledVFXTracker : MonoBehaviour
    {
        private VFXService _service;
        private VFXType _type;

        public void Initialize(VFXService service, VFXType type)
        {
            _service = service;
            _type = type;
        }

        private void OnParticleSystemStopped()
        {
            if (_service != null)
                _service.Despawn(gameObject, _type);
        }
    }
}
