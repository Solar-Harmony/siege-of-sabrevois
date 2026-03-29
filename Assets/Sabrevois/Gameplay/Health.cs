using System;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    public class Health : MonoBehaviour
    {
        [Min(1.0f)]
        public float MaxHealth;
        
        public float CurrentHealth { get; private set; }
        public float CurrentHealth01 => CurrentHealth / MaxHealth;
        
        private void Awake()
        {
            CurrentHealth = MaxHealth;
        }
        
        public void TakeDamage(float damage)
        {
            CurrentHealth = Mathf.Max(CurrentHealth - damage, 0);
            OnDamageTaken?.Invoke(damage);
            
            if (CurrentHealth <= 0)
            {
                Destroy(gameObject);
            }
        }
        
        public event Action<float> OnDamageTaken;
    }
}