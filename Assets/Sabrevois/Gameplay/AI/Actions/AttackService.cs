using System;
using SolarHarmony.DynamicWounds2D;
using Sabrevois.Gameplay.Tree;
using Sabrevois.Level.Water;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.Actions
{
    public class AttackService
    {
        public static event Action<Vector3> OnMiss;

        public void Attack(Transform attacker, Ray ray, float attackRange, float woundRadius, float woundPenetration)
        {
            // Use QueryTriggerInteraction.Collide so the raycast can hit the water plane trigger
            RaycastHit[] hits = Physics.RaycastAll(ray, attackRange, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool hadEffect = false;
            Vector3 missPoint = ray.origin + ray.direction * attackRange;

            foreach (var hit in hits)
            {
                // Support for shooting the water directly
                if (hit.collider.gameObject.CompareTag("Water") || hit.collider.gameObject.layer == LayerMask.NameToLayer("Water"))
                {
                    WaterRipplesInteraction.AddDisturbance(new Vector2(hit.point.x, hit.point.z), 0.2f, 1f);
                    hadEffect = true;
                    break;
                }

                // Ignore other triggers so bullets don't get blocked by invisible enemy aggro ranges or event triggers
                if (hit.collider.isTrigger && hit.collider.GetComponentInParent<WoundsComponent>() == null) continue;

                missPoint = hit.point;

                var health = hit.collider.GetComponentInParent<Health>();

                // Resolve which WoundsComponent sprite was actually hit
                // Collect all WoundsComponents under the root, sorted front-to-back by sibling index,
                // and pick the frontmost one with a solid pixel at the hit point.
                // This lets transparent areas on a front sprite pass through to sprites behind it
                // (e.g. wings behind body, or holes in clothing).
                var root = hit.collider.transform.root;
                var allWounds = root.GetComponentsInChildren<WoundsComponent>();
                if (allWounds.Length > 1)
                    System.Array.Sort(allWounds, (a, b) =>
                        a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

                WoundsComponent wounds = null;
                var trueHitNormal = (Camera.main.transform.position - hit.point).normalized;
                foreach (var wc in allWounds)
                {
                    if (wc.IsSolidAtWorldPoint(hit.point))
                    {
                        wounds = wc;
                        break;
                    }
                }

                float weaponPenetration = woundPenetration;
                bool isEssential = true;
                bool isBleeding = false;

                float woundDepth = 0f;
                float resistance = 0f;
                float damage = weaponPenetration;

                if (wounds != null)
                {
                    Vector3 hitVelocity = ray.direction * 5f;

                    if (health != null && health.IsDead)
                    {
                        woundDepth = wounds.ApplyWoundAtPoint(hit.point, trueHitNormal, woundRadius, weaponPenetration, out isEssential, out isBleeding, out resistance, out damage);
                    }
                    else
                    {
                        woundDepth = wounds.ApplyWound(hit, trueHitNormal, woundRadius, weaponPenetration, hitVelocity, out isEssential, out isBleeding, out resistance, out damage);
                    }
                }
                else if (health != null)
                {
                    // No solid WoundsComponent at hit point (grid cells dead / limb severed).
                    // Still try to identify the body part for correct resistance and essential detection.
                    foreach (var wc in allWounds)
                    {
                        if (wc.TryMatchBodyPartAtWorld(hit.point, out _, out isEssential))
                            break;
                    }

                    resistance = health.GetResistanceAtDepth(0f);
                    damage = weaponPenetration * (1f - resistance / 100f);
                    woundDepth = damage;
                }

                if (health != null)
                {
                    health.TakeDamage(woundDepth, ray.direction, isEssential);

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

                    hadEffect = true;
                }

                var tree = hit.collider.GetComponentInParent<FellableTree>();
                if (tree != null)
                {
                    tree.Fell(ray.direction);
                    hadEffect = true;
                }

                if (wounds != null)
                    hadEffect = true;

                break;
            }

            if (!hadEffect)
            {
                OnMiss?.Invoke(missPoint);
            }
        }
    }
}