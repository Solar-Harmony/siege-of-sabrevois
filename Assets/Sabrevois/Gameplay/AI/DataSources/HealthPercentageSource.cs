using System;
using Sabrevois.AI.Considerations;
using Sabrevois.Utils;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    [Serializable]
    public class HealthPercentageSource : IConsiderationSource
    {
        public float GetValue(GameObject agent)
        {
            agent.GetComponentChecked(out Health health);
            
            return health.CurrentHealth01;
        }
    }
}