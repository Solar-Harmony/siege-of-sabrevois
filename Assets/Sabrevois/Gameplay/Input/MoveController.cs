using Sabrevois.Level;
using UnityEngine;

namespace Sabrevois.Gameplay.Input
{
    public class MoveController : MonoBehaviour
    {
        [SerializeField] private InputRouter _input;
        [SerializeField] private float _baseMoveSpeed = 5f;
        [SerializeField] private float _crouchSpeed = 2.5f;
        [SerializeField] private float _jumpForce = 7f;
        [SerializeField] private float _bHopBoost = 1.2f;
        [SerializeField] private float _maxBHopSpeed = 15f;
        [SerializeField] private float _bHopWindow = 0.2f;
        [SerializeField] private float _gravityMultiplier = 2.5f;
        [SerializeField] private Rigidbody _rb;
        private Camera _camera;
        
        private bool _isCrouching = false;
        private bool _isGrounded = true;
        private float _currentSpeed;
        private float _lastGroundedTime;
        private Vector3 _originalScale;

        private void Awake()
        {
            _camera = Camera.main;
            _currentSpeed = _baseMoveSpeed;
            _originalScale = transform.localScale;
        }

        private void Update()
        {
            if (_input.CrouchPressed)
            {
                _isCrouching = !_isCrouching;
                transform.localScale = _isCrouching ? new Vector3(_originalScale.x, _originalScale.y * 0.5f, _originalScale.z) : _originalScale;
            }

            if (_input.JumpPressed && _isGrounded && !_isCrouching)
            {
                if (Time.time - _lastGroundedTime <= _bHopWindow)
                {
                    _currentSpeed = Mathf.Min(_currentSpeed + _bHopBoost, _maxBHopSpeed);
                }
                
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
                _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
                _isGrounded = false;
            }
        }

        private void FixedUpdate()
        {
            CheckGrounded();
            
            if (_isGrounded && !_input.JumpPressed)
            {
                float targetSpeed = _isCrouching ? _crouchSpeed : _baseMoveSpeed;
                _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.fixedDeltaTime * 5f);
            }

            Vector2 moveInput = _input.MoveAxis;
            Vector3 forward = _camera.transform.forward;
            forward.y = 0;
            forward.Normalize();
            Vector3 right = _camera.transform.right;
            right.y = 0;
            right.Normalize();
            Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;

            Vector3 euler = _camera.transform.rotation.eulerAngles;
            euler.x = 0;
            
            Vector3 targetVelocity = moveDirection * _currentSpeed;
            _rb.linearVelocity = new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z);
            _rb.MoveRotation(Quaternion.Euler(euler));
            
            if (!_isGrounded)
            {
                _rb.AddForce(Physics.gravity * (_gravityMultiplier - 1f), ForceMode.Acceleration);
            }
        }

        private void CheckGrounded()
        {
            bool wasGrounded = _isGrounded;
            _isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.2f);
            
            if (!wasGrounded && _isGrounded)
            {
                _lastGroundedTime = Time.time;
            }
        }
    }
}