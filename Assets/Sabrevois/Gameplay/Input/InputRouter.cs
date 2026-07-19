using UnityEngine;
using UnityEngine.InputSystem;

namespace Sabrevois.Level
{
    public class InputRouter : MonoBehaviour
    {
        private InputSystem_Actions _actions;
        private bool _isSlashing;

        public Vector2 MoveAxis => _actions.Player.Move.ReadValue<Vector2>();
        public Vector2 LookAxis => _actions.Player.Look.ReadValue<Vector2>();
        public bool JumpPressed => _actions.Player.Jump.triggered;
        public bool AttackPressed => _actions.Player.Attack.triggered;
        public bool SlashHeld => _isSlashing;
        public bool CrouchPressed => _actions.Player.Crouch.triggered;

        private void Awake()
        {
            _actions = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            _actions.Player.SecondaryAttack.performed += OnSecondaryAttack;
            _actions.Player.SecondaryAttack.canceled += OnSecondaryAttackCanceled;
            _actions.Enable();
        }

        private void OnDisable()
        {
            _actions.Player.SecondaryAttack.performed -= OnSecondaryAttack;
            _actions.Player.SecondaryAttack.canceled -= OnSecondaryAttackCanceled;
            _actions.Disable();
        }

        private void OnDestroy()
        {
            _actions.Dispose();
        }

        private void OnSecondaryAttack(InputAction.CallbackContext ctx)
        {
            _isSlashing = true;
        }

        private void OnSecondaryAttackCanceled(InputAction.CallbackContext ctx)
        {
            _isSlashing = false;
        }
    }
}