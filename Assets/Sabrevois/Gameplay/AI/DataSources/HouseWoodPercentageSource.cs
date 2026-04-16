using Sabrevois.AI.Considerations;
using Sabrevois.AI.DataSources;
using Sabrevois.Utils;
using System;
using System.Linq;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    [Serializable]
    public class HouseWoodPercentageSource : IDataSource
    {
        public float GetValue(GameObject agent)
        {
            return WorldObjectRegistry.Instance
                .Get(WorldObjectCategory.House)
                .Select(go => go.GetComponent<House>())
                .Count(h => h.NeedsWood());
        }
    }
}