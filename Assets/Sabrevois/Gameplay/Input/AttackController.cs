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
                
                hitInfo.collider.gameObject.TryGetComponent(out Health health);
                if (!health)
                    return;
                
                health.TakeDamage(10);
            }
        }
    }
}