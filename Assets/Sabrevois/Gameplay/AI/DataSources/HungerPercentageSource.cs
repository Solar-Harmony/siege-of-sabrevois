using Sabrevois.AI.Considerations;
using Sabrevois.Utils;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    public class HungerPercentageSource : IConsiderationSource
    {
        public float GetValue(GameObject agent)
        {
            agent.GetComponentChecked(out Hunger hunger);
            
            return hunger.CurrentHunger01;
        }
    }
}