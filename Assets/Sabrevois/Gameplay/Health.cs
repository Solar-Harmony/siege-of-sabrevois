using System;
using Sabrevois.Utils;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    public class Health : MonoBehaviour
    {
        [Min(1.0f)]
        public float MaxHealth;
        
        public float CurrentHealth { get; private set; }
        public float CurrentHealth01 => CurrentHealth / MaxHealth;
        
        private bool _isDead = false;

        private void Awake()
        {
            CurrentHealth = MaxHealth;
        }
        
        public void TakeDamage(float damage)
        {
            if (_isDead) return;

            CurrentHealth = Mathf.Max(CurrentHealth - damage, 0);
            OnDamageTaken?.Invoke(damage);

            if (CurrentHealth <= 0)
            {
                _isDead = true;

                var billboard = GetComponent<Billboard>();
                if (billboard != null) billboard.enabled = false;
                
                var navMeshAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navMeshAgent != null) navMeshAgent.enabled = false;

                var agent = GetComponent<Sabrevois.AI.Agent>();
                if (agent != null) agent.enabled = false;
                
                var rb  = GetComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                
                var cap  = GetComponent<CapsuleCollider>();
                cap.radius = 0.06f;
                
                gameObject.transform.Rotate(90f, 0f, 0f);
            }
        }
        
        public event Action<float> OnDamageTaken;
    }
}