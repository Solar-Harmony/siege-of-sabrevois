using System;
using Sabrevois.AI.DataSources;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    [Serializable]
    public class WorldObjectQuantitySource : IDataSource
    {
        public WorldObjectCategory objectCategory;
        
        public float GetValue(GameObject agent)
        {
            return WorldObjectRegistry.Instance.Get(objectCategory).Count;
        }
    }
}