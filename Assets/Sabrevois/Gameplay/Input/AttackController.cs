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

            if (_input.SlashHeld)
            {
                Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, _slashRange))
                {
                    var wounds = hit.collider.GetComponentInParent<WoundsComponent>();
                    if (wounds != null)
                    {
                        Vector3 slashDirection = -ray.direction;
                        bool isEssential, isBleeding;
                        float resistance, damage;
                        
                        wounds.ApplySlashWound(hit.point, slashDirection, _woundRadius, _slashDamage, out isEssential, out isBleeding, out resistance, out damage);

                        Debug.DrawRay(hit.point, slashDirection * 0.5f, Color.green);
                        
                        var health = wounds.GetComponentInParent<Health>();
                        if (health != null)
                        {
                            health.TakeDamage(damage, slashDirection, isEssential);
                            Debug.Log($"Target: {health.name} | Weapon Pen: {_slashDamage} | Resistance: {resistance}% | Damage: {damage:F2} | Essential: {isEssential} | Bleeding: {isBleeding}");
                        }
                    }
                }
            }
        }
    }
}