using Sabrevois.Level;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sabrevois.UI
{
    public class WeaponVisuals : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private InputRouter _input;
        [SerializeField] private float _bobbingSpeed = 10f;
        [SerializeField] private float _bobbingAmount = 10f;
        [SerializeField] private float _horizontalBobAmount = 5f;
        [SerializeField] private float _smoothTime = 0.1f;
        
        [SerializeField] private Texture2D _weaponIdleTexture;
        [SerializeField] private Texture2D _weaponFireTexture;

        private VisualElement _weapon;
        private float _bobTimer;
        private Vector2 _currentOffset;
        private Vector2 _targetOffset;
        private Vector2 _velocity;

        private void Awake()
        {
            _weapon = _document.rootVisualElement.Q<VisualElement>("weapon");
        }

        private void Update()
        {
            // TODO: use events instead of polling.
            if (_input.AttackPressed)
            {
                _weapon.style.backgroundImage = new StyleBackground(_weaponFireTexture);
            }
            else
            {
                _weapon.style.backgroundImage = new StyleBackground(_weaponIdleTexture);
            }
            
            Vector2 moveInput = _input.MoveAxis;
            float moveMagnitude = moveInput.magnitude;

            if (moveMagnitude > 0.01f)
            {
                _bobTimer += Time.deltaTime * _bobbingSpeed;
                
                float verticalBob = Mathf.Sin(_bobTimer) * _bobbingAmount * moveMagnitude;
                float horizontalBob = Mathf.Cos(_bobTimer * 0.5f) * _horizontalBobAmount * moveMagnitude;
                
                _targetOffset = new Vector2(horizontalBob, verticalBob);
            }
            else
            {
                _bobTimer = 0f;
                _targetOffset = Vector2.zero;
            }

            _currentOffset = Vector2.SmoothDamp(_currentOffset, _targetOffset, ref _velocity, _smoothTime);

            _weapon.style.translate = new Translate(_currentOffset.x, _currentOffset.y);
        }
    }
}

