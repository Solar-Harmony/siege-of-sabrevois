using UnityEngine;

namespace Sabrevois.Level
{
    public class InputRouter : MonoBehaviour
    {
        private InputSystem_Actions _actions;

        public Vector2 MoveAxis => _actions.Player.Move.ReadValue<Vector2>();
        public Vector2 LookAxis => _actions.Player.Look.ReadValue<Vector2>();
        public bool JumpPressed => _actions.Player.Jump.triggered;
        public bool AttackPressed => _actions.Player.Attack.triggered;
        public bool CrouchPressed => _actions.Player.Crouch.triggered;
        
        private void Awake()
        {
            _actions = new InputSystem_Actions();
        }
        
        private void OnEnable()
        {
            _actions.Enable();
        }
        
        private void OnDisable()
        {
            _actions.Disable();
        }

        private void OnDestroy()
        {
            _actions.Dispose();
        }
    }
}