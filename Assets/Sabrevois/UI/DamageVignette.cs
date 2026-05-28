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
            _vignette.style.opacity = 1f - _health.HealthPercent;
        }
    }
}