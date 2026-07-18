using Sabrevois.Gameplay.AI.Actions;
using Sabrevois.Gameplay.Tree;
using Sabrevois.Level;
using Sabrevois.Level.Water;
using Sabrevois.Utils;
using SolarHarmony.DynamicWounds2D;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

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
        private AttackService _attackService;

        [Inject]
        public void Construct(AttackService attackService)
        {
            _attackService = attackService;
        }
        
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
                
                _attackService.Attack(transform, ray, _attackRange, _woundRadius, _woundPenetration);
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