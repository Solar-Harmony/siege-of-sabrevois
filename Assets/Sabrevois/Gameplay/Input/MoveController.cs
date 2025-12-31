using System;
using Sabrevois.Level;
using UnityEditor;
using UnityEngine;

namespace Sabrevois.Gameplay.Input
{
    public class MoveController : MonoBehaviour
    {
        [SerializeField] private InputRouter _input;
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private Rigidbody _rb;
        private Camera _camera;
        
        private void Awake()
        {
            _camera = Camera.main;
        }

        private void FixedUpdate()
        {
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
            _rb.Move(_rb.position + moveDirection * (_moveSpeed * Time.fixedDeltaTime), Quaternion.Euler(euler));
        }
    }
}