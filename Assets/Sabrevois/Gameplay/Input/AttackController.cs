using Sabrevois.Gameplay.AI.Actions;
using Sabrevois.Level;
using UnityEngine;
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
                Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                _attackService.Attack(transform, ray, _attackRange, _woundRadius, _woundPenetration);
            }

            if (_input.SlashHeld)
            {
                Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                _attackService.Attack(transform, ray, _slashRange, _woundRadius, _slashDamage);
            }
        }
    }
}