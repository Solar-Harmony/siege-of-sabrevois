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
        [SerializeField] private float _slashRange = 2.5f;
        [SerializeField] private float _slashDamage = 1.0f;
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
                    
                    Vector3 trueHitNormal = hit.normal;
                    float weaponPenetration = _woundPenetration;
                    bool isEssential = true;
                    bool isBleeding = false;
                    
                    float woundDepth = 0f;
                    float resistance = 0f;
                    float damage = weaponPenetration;
                    
                    var wounds = hit.collider.GetComponentInParent<WoundsComponent>();
                    var health = hit.collider.GetComponentInParent<Health>();

                    if (wounds != null)
                    {
                        // Since we shoot a rotated proxy hitbox trigger for billboarding, the "normal" will just match the BoxCollider.
                        // For blood splashes to shoot back at the camera reliably, we use the vector pointing backward to the player instead.
                        trueHitNormal = (Camera.main.transform.position - hit.point).normalized;
                        Vector3 hitVelocity = ray.direction * 5f;
                        
                        woundDepth = wounds.ApplyWound(hit, trueHitNormal, _woundRadius, weaponPenetration, hitVelocity, out isEssential, out isBleeding, out resistance, out damage);
                    }
                    else if (health != null)
                    {
                        resistance = health.GetResistanceAtDepth(0f);
                        damage = weaponPenetration * (1f - resistance / 100f);
                        woundDepth = damage;
                    }
                    
                    if (health != null)
                    {
                        health.TakeDamage(woundDepth, ray.direction, isEssential);
                        
                        Debug.Log($"Target: {health.name} | Weapon Pen: {weaponPenetration} | Resistance: {resistance}% | Damage: {damage:F2} | Wound Depth: {woundDepth:F2} | Essential: {isEssential} | Bleeding: {isBleeding}");
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

            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                
                Debug.DrawRay(ray.origin, ray.direction * _attackRange, Color.blue, 1f);

                // Find all enemies in slash range
                Collider[] colliders = Physics.OverlapSphere(transform.position, _slashRange);
                Health closestEnemy = null;
                float closestDist = float.MaxValue;

                foreach (var col in colliders)
                {
                    var health = col.GetComponentInParent<Health>();
                    if (health != null && health.gameObject != this.gameObject)
                    {
                        float dist = Vector3.Distance(transform.position, health.transform.position);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestEnemy = health;
                        }
                    }
                }

                if (closestEnemy != null)
                {
                    Vector3 slashDirection = (closestEnemy.transform.position - transform.position).normalized;
                    
                    float resistance = closestEnemy.GetResistanceAtDepth(0f);
                    float damage = _slashDamage * (1f - resistance / 100f);
                    
                    closestEnemy.TakeDamage(damage, slashDirection);
                }
            }
        }
    }
}