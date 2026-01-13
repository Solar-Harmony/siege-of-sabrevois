using System;
using Sabrevois.AI.Considerations;
using Sabrevois.Utils;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    [Serializable]
    public class RandomSource : IConsiderationSource
    {
        public float GetValue(GameObject agent)
        {
            return UnityEngine.Random.value;
        }
    }
}