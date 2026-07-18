using System;
using UnityEngine;

namespace SolarHarmony.DynamicWounds2D
{
    public interface IWoundHost
    {
        bool IsDead { get; }

        event Action OnDeathComplete;

        void ForceKill();

        float GetResistanceAtDepth(float depth);

        void ApplyMovementImpulse(Vector3 direction, float strength);

        Transform Transform { get; }
    }
}
