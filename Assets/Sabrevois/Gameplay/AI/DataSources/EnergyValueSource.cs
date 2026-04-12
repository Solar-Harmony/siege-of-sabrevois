using Sabrevois.AI.Considerations;
using Sabrevois.Utils;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    public class EnergyValueSource : IConsiderationSource
    {
        public float GetValue(GameObject agent)
        {
            agent.GetComponentChecked(out Energy energy);
            
            return energy.CurrentEnergy;
        }
    }
}