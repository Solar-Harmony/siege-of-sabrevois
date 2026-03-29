using Sabrevois.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sabrevois.UI
{
    public class DamageVignette : MonoBehaviour
    {
        [SerializeField] private Health _health;
        [SerializeField] private UIDocument _document;
        
        private VisualElement _vignette;
        private void Awake()
        {
            _vignette = _document.rootVisualElement.Q<VisualElement>("damage-vignette");
            _health.OnDamageTaken += HandleDamageTaken;
        }
        
        private void HandleDamageTaken(float damage)
        {
            float intensity = Mathf.Clamp01(damage / _health.MaxHealth);
            _vignette.style.opacity = intensity;
        }
    }
}