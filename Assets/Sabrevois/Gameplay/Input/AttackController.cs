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
                Ray ray = _camera.ScreenPointToRay(new Vector3(
                    Screen.width / 2f,
                    Screen.height / 2f,
                    0f
                ));
                
                Debug.DrawRay(ray.origin, ray.direction * _attackRange, Color.red, 1f);

                if (!Physics.Raycast(ray, out RaycastHit hitInfo, _attackRange))
                    return;

                Debug.Log($"Je suis TOUCHE! {hitInfo.collider.gameObject.name}");
                GameObject victim = hitInfo.collider.gameObject;
                
                if (victim.TryGetComponent(out Health health))
                    health.TakeDamage(_damageAmount);

                if (victim.TryGetComponent(out FellableTree tree))
                {
                    tree.Fell(ray.direction);
                }
            }
        }
    }
}