using System;
using UnityEngine;
using Zenject;
using Sabrevois.Gameplay.AI.Actions;

namespace Sabrevois.UI
{
    public class MissTextSpawner : IInitializable, IDisposable
    {
        private readonly DamageNumber.MissTextPool _pool;

        public MissTextSpawner(DamageNumber.MissTextPool pool)
        {
            _pool = pool;
        }

        public void Initialize()
        {
            AttackService.OnMiss += HandleMiss;
        }

        public void Dispose()
        {
            AttackService.OnMiss -= HandleMiss;
        }

        private void HandleMiss(Vector3 position)
        {
            _pool.Spawn(position);
        }
    }
}
