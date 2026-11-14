using System;
using Sabrevois.AI.DataSources;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    /// <summary>
    /// Persisted fear of an NPC, 0..1. Wire it into a FleeAction consideration so a
    /// cowardly NPC that is wounded or outmatched actually runs.
    /// </summary>
    [Serializable]
    public class FearDataSource : IDataSource
    {
        public float GetValue(GameObject agent)
        {
            var health = agent.GetComponent<Health>();
            return health != null ? health.Fear : 0f;
        }
    }
}
