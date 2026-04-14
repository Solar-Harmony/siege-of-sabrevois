using System;
using Sabrevois.AI.Considerations;
using Sabrevois.AI.DataSources;
using Sabrevois.Utils;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    [Serializable]
    public class AgentWoodPercentageSource : IDataSource
    {
        public float GetValue(GameObject agent)
        {
            agent.GetComponentChecked(out Wood wood);

            return wood.CurrentWood / wood.MaxWood;
        }
    }
}