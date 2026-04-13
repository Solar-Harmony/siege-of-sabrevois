using System;
using Sabrevois.AI.Considerations;
using Sabrevois.Utils;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    [Serializable]
    public class EnergyPercentageSource : IConsiderationSource
    {
        public float GetValue(GameObject agent)
        {
            agent.GetComponentChecked(out Energy fatigue);
            
            return fatigue.CurrentEnergy01;
        }
    }
}