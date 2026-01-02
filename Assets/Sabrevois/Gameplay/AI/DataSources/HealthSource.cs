using System;
using Sabrevois.AI.Considerations;
using Sabrevois.Utils;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    [Serializable]
    public class HealthSource : IConsiderationSource
    {
        public float GetValue(GameObject agent)
        {
            agent.GetComponentChecked(out Health health);
            
            return health.CurrentHealth;
        }
    }
}