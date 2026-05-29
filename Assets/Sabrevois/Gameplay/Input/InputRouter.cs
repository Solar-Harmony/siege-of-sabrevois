using UnityEngine;
using UnityEngine.InputSystem;

namespace Sabrevois.Level
{
    public class InputRouter : MonoBehaviour
    {
        private InputSystem_Actions _actions;

        public Vector2 MoveAxis => _actions.Player.Move.ReadValue<Vector2>();
        public Vector2 LookAxis => _actions.Player.Look.ReadValue<Vector2>();
        public bool JumpPressed => _actions.Player.Jump.triggered;
        public bool AttackPressed => _actions.Player.Attack.triggered;
        public bool SlashPressed => _actions.Player.SecondaryAttack.triggered; // Keep triggered for single events if needed? No, user wants holding
        public bool SlashHeld => _actions.Player.SecondaryAttack.IsPressed();
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