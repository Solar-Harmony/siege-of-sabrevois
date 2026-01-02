using System;
using UnityEngine;

namespace Sabrevois.AI.Considerations
{
    public class Health : MonoBehaviour
    {
        [Min(1)]
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
        }
    }
}