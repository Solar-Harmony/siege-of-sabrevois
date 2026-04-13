using System;
using System.Collections.Generic;
using Sabrevois.AI.Considerations;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    [Serializable]
    public class WorldObjectQuantitySource : IConsiderationSource
    {
        public WorldObjectCategory objectCategory;
        
        public float GetValue(GameObject agent)
        {
            return WorldObjectRegistry.Instance.Get(objectCategory).Count;
        }
    }
}