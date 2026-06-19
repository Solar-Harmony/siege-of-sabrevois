using System;
using UnityEngine;
using Zenject;
using Sabrevois.Gameplay;

namespace Sabrevois.UI
{
    public class DamageNumberSpawner : IInitializable, IDisposable
    {
        private readonly DamageNumber.Pool _pool;

        public DamageNumberSpawner(DamageNumber.Pool pool)
        {
            _pool = pool;
        }

        public void Initialize()
        {
            Health.OnAnyDamageTaken += HandleDamageTaken;
        }

        public void Dispose()
        {
            Health.OnAnyDamageTaken -= HandleDamageTaken;
        }

        private void HandleDamageTaken(Health health, float amount)
        {
            if (health == null) return;
            _pool.Spawn(health.transform.position + Vector3.up * 1.5f, amount);
        }
    }
}