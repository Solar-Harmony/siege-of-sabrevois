using Sabrevois.Gameplay.Tree;
using Sabrevois.Level;
using Sabrevois.Level.Water;
using Sabrevois.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sabrevois.Gameplay.Input
{
    public class AttackController : MonoBehaviour
    {
        [SerializeField] private InputRouter _input;
        [SerializeField] private int _attackRange = 100;
        [SerializeField] private float _explosionRadius = 5f;
        [SerializeField] private int _explosionDamage = 0;
        [SerializeField] private float _explosionForce = 800f;
        [SerializeField] private float _woundRadius = 0.15f;
        [SerializeField] private float _woundPenetration = 0.6f;
        private Camera _camera;
        
        private void Awake()
        {
            _camera = Camera.main;
            Debug.Assert(_camera);
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

                // Use QueryTriggerInteraction.Collide so the raycast can hit the water plane trigger
                RaycastHit[] hits = Physics.RaycastAll(ray, _attackRange, ~0, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                bool hitValid = false;
                foreach (var hit in hits)
                {
                    if (hit.collider.GetComponentInParent<AttackController>() == this) continue;

                    // Support for shooting the water directly
                    if (hit.collider.gameObject.CompareTag("Water") || hit.collider.gameObject.layer == LayerMask.NameToLayer("Water"))
                    {
                        WaterRipplesInteraction.AddDisturbance(new Vector2(hit.point.x, hit.point.z), 0.2f, 1f);
                        hitValid = true;
                        break;
                    }

                    // Ignore other triggers so bullets don't get blocked by invisible enemy aggro ranges or event triggers
                    if (hit.collider.isTrigger && hit.collider.GetComponentInParent<WoundsComponent>() == null) continue;

                    Debug.LogFormat($"{hit.collider.gameObject.name} was hit.");
                    
                    Vector3 trueHitNormal = hit.normal;
                    float localPenetration = _woundPenetration;
                    
                    var wounds = hit.collider.GetComponentInParent<WoundsComponent>();
                    if (wounds != null)
                    {
                        // Since we shoot a rotated proxy hitbox trigger for billboarding, the "normal" will just match the BoxCollider.
                        // For blood splashes to shoot back at the camera reliably, we use the vector pointing backward to the player instead.
                        trueHitNormal = (Camera.main.transform.position - hit.point).normalized;
                        Vector3 hitVelocity = ray.direction * 5f;
                        localPenetration = wounds.ApplyWound(hit, trueHitNormal, _woundRadius, _woundPenetration, hitVelocity);
                    }
                    
                    var health = hit.collider.GetComponentInParent<Health>();
                    if (health != null)
                    {
                        health.TakeDamage(localPenetration, ray.direction);
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
                            Vector3 expDir = (health.transform.position - hit.point).normalized;
                            health.TakeDamage(_explosionDamage, expDir);
                            var p = health.transform.position;
                            p.y = hit.point.y;

                            var wounds = col.GetComponentInParent<WoundsComponent>();
                            if (wounds != null)
                            {
                                wounds.PlayBloodVFXWorld(p, hit.normal, expDir * (_explosionForce * 0.01f));
                            }
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