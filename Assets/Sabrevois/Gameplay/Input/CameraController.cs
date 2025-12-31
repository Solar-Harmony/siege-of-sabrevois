using Sabrevois.Level;
using UnityEngine;

namespace Sabrevois.Gameplay.Input
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private InputRouter _input;
        [SerializeField] private Transform _player;
        [SerializeField] private float _speed = 0.1f;
        private Camera _camera;
        private float _pitch;
        private float _yaw;

        private void Awake()
        {
            _camera = Camera.main;
            Debug.Assert(_camera);
            
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            Vector3 e = _camera!.transform.rotation.eulerAngles;
            _pitch = e.x;
            _yaw = e.y;
        }
            
        private void LateUpdate()
        {
            _pitch -= _input.LookAxis.y * _speed;
            _pitch = Mathf.Clamp(_pitch, -90f, 90f);

            _yaw += _input.LookAxis.x * _speed;

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0);
            
            Vector3 pos = Vector3.Lerp(
                _camera.transform.position, 
                _player.position, 
                Time.deltaTime * 10f
            );
            
            _camera.transform.SetPositionAndRotation(pos, rotation);
        }
    }
}