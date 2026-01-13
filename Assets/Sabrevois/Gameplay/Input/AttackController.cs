using Sabrevois.Gameplay.Tree;
using Sabrevois.Level;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sabrevois.Gameplay.Input
{
    public class AttackController : MonoBehaviour
    {
        [SerializeField] private InputRouter _input;
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

                if (!Physics.Raycast(ray, out RaycastHit hitInfo, 100f))
                    return;

                GameObject victim = hitInfo.collider.gameObject;
                
                if (victim.TryGetComponent(out Health health))
                    health.TakeDamage(10);

                if (victim.TryGetComponent(out FellableTree tree))
                {
                    tree.Fell(ray.direction);
                }
            }
        }
    }
}