using System;
using UnityEngine;

namespace Sabrevois.Utils
{
    public class Billboard : MonoBehaviour
    {
        private Camera _camera;
        
        [SerializeField]
        private bool _horizontalOnly = false;
        
        private void Awake()
        {
            _camera = Camera.main;
        }

        private void LateUpdate()
        {
            Vector3 lookPos = transform.position - _camera.transform.position;
            if (_horizontalOnly)
                lookPos.y = 0;
            
            transform.rotation = Quaternion.LookRotation(lookPos);
        }
    }
}