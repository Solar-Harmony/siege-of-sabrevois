using System;
using Sabrevois.AI.Considerations;
using Sabrevois.AI.DataSources;
using Sabrevois.Utils;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    [Serializable]
    public class EnergyPercentageSource : IDataSource
    {
        public float GetValue(GameObject agent)
        {
            agent.GetComponentChecked(out Energy fatigue);
            
            return fatigue.CurrentEnergy01;
        }
    }
}