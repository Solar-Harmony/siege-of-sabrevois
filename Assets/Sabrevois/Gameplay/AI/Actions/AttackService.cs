using Sabrevois.Gameplay.Tree;
using Sabrevois.Level.Water;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.Actions
{
    public class AttackService
    {
        public void Attack(Transform attacker, Ray ray, float attackRange, float woundRadius, float woundPenetration)
        {
            Debug.DrawRay(ray.origin, ray.direction * attackRange, Color.red, 1f);

            // Use QueryTriggerInteraction.Collide so the raycast can hit the water plane trigger
            RaycastHit[] hits = Physics.RaycastAll(ray, attackRange, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                // Support for shooting the water directly
                if (hit.collider.gameObject.CompareTag("Water") || hit.collider.gameObject.layer == LayerMask.NameToLayer("Water"))
                {
                    WaterRipplesInteraction.AddDisturbance(new Vector2(hit.point.x, hit.point.z), 0.2f, 1f);
                    break;
                }

                // Ignore other triggers so bullets don't get blocked by invisible enemy aggro ranges or event triggers
                if (hit.collider.isTrigger && hit.collider.GetComponentInParent<WoundsComponent>() == null) continue;

                float weaponPenetration = woundPenetration;
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
                    var trueHitNormal = (Camera.main.transform.position - hit.point).normalized;
                    Vector3 hitVelocity = ray.direction * 5f;

                    woundDepth = wounds.ApplyWound(hit, trueHitNormal, woundRadius, weaponPenetration, hitVelocity, out isEssential, out isBleeding, out resistance, out damage);
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

                    var targetOpponent = health.GetComponent<OpponentComponent>();
                    if (targetOpponent != null)
                    {
                        targetOpponent.CurrentOpponent = attacker;
                    }

                    if (attacker != null)
                    {
                        var attackerOpponent = attacker.GetComponent<OpponentComponent>();
                        if (attackerOpponent != null)
                        {
                            attackerOpponent.CurrentOpponent = health.transform;
                        }
                    }
                }

                var tree = hit.collider.GetComponentInParent<FellableTree>();
                if (tree != null)
                {
                    tree.Fell(ray.direction);
                }

                break;
            }
        }
    }
}