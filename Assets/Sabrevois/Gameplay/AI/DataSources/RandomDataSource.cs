using System;
using Sabrevois.AI.Considerations;
using Sabrevois.AI.DataSources;
using Sabrevois.Utils;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    [Serializable]
    public class RandomSource : IDataSource
    {
        public float GetValue(GameObject agent)
        {
            return UnityEngine.Random.value;
        }
    }
}