using Sabrevois.Gameplay.Tree;
using Sabrevois.Level;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sabrevois.Gameplay.Input
{
    public class AttackController : MonoBehaviour
    {
        [SerializeField] private InputRouter _input;
        [SerializeField] private int _damageAmount = 10;
        [SerializeField] private int _attackRange = 100;
        [SerializeField] private float _explosionRadius = 5f;
        [SerializeField] private int _explosionDamage = 0;
        [SerializeField] private float _explosionForce = 800f;
        [SerializeField] private GameObject _bloodPrefab;
        private Camera _camera;
        
        private void Awake()
        {
            _camera = Camera.main;
            Debug.Assert(_camera);
        }

        private void SpawnBlood(Vector3 hitPosition, Vector3 hitNormal)
        {
            Instantiate(_bloodPrefab, hitPosition, Quaternion.LookRotation(hitNormal));
        }
        
        private void Update()
        {
            if (_input.AttackPressed)
            {
                Ray ray = _camera.ViewportPointToRay(new Vector3(
                    0.5f,
                    0.5f,
                    0f
                ));
                
                Debug.DrawRay(ray.origin, ray.direction * _attackRange, Color.red, 1f);

                RaycastHit[] hits = Physics.RaycastAll(ray, _attackRange, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                bool hitValid = false;
                foreach (var hit in hits)
                {
                    if (hit.collider.GetComponentInParent<AttackController>() == this) continue;

                    Debug.Log($"Je suis touché: {hit.collider.gameObject.name}");
                    
                    var health = hit.collider.GetComponentInParent<Health>();
                    if (health != null)
                    {
                        health.TakeDamage(_damageAmount);
                        SpawnBlood(hit.point, hit.normal);
                    }

                    var tree = hit.collider.GetComponentInParent<FellableTree>();
                    if (tree != null)
                    {
                        tree.Fell(ray.direction);
                    }
                    
                    hitValid = true;
                    break;
                }

                if (!hitValid) return;
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                
                if (Physics.Raycast(ray, out RaycastHit hit, _attackRange, ~0, QueryTriggerInteraction.Ignore))
                {
                    Collider[] colliders = Physics.OverlapSphere(hit.point, _explosionRadius);
                    var hitHealths = new System.Collections.Generic.HashSet<Health>();
                    var hitTrees = new System.Collections.Generic.HashSet<FellableTree>();
                    var hitRbs = new System.Collections.Generic.HashSet<Rigidbody>();

                    foreach (var col in colliders)
                    {
                        if (col.GetComponentInParent<AttackController>() == this) continue;

                        if (col.attachedRigidbody != null && hitRbs.Add(col.attachedRigidbody))
                        {
                            var navAgent = col.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
                            if (navAgent != null)
                            {
                                navAgent.enabled = false;
                            }
                            
                            col.attachedRigidbody.isKinematic = false;
                            col.attachedRigidbody.constraints = RigidbodyConstraints.None;
                            col.attachedRigidbody.AddExplosionForce(_explosionForce, hit.point, _explosionRadius, 2f, ForceMode.Impulse);
                            col.attachedRigidbody.AddTorque(Random.insideUnitSphere * (_explosionForce * 0.05f), ForceMode.Impulse);
                        }

                        var health = col.GetComponentInParent<Health>();
                        if (health && hitHealths.Add(health))
                        {
                            health.TakeDamage(_explosionDamage);
                            var p = health.transform.position;
                            p.y = hit.point.y;
                            SpawnBlood(p, hit.normal);
                        }

                        var tree = col.GetComponentInParent<FellableTree>();
                        if (tree && hitTrees.Add(tree))
                        {
                            Vector3 dir = (col.transform.position - hit.point).normalized;
                            tree.Fell(dir == Vector3.zero ? Vector3.up : dir);
                        }
                    }
                }
            }
        }
    }
}