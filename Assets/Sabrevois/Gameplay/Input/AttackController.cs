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
                    if (hit.collider.transform.root == transform.root) continue;

                    Debug.Log($"Je suis TOUCHE! {hit.collider.gameObject.name}");
                    GameObject victim = hit.collider.gameObject;
                    
                    if (victim.TryGetComponent(out Health health))
                        health.TakeDamage(_damageAmount);

                    if (victim.TryGetComponent(out FellableTree tree))
                    {
                        tree.Fell(ray.direction);
                    }
                    
                    hitValid = true;
                    break;
                }

                if (!hitValid) return;
            }
        }
    }
}