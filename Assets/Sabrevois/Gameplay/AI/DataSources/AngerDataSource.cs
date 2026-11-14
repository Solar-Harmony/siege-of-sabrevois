using System;
using Sabrevois.AI.DataSources;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    /// <summary>
    /// Persisted anger of an NPC, 0..1. Wire it into an AttackAction consideration
    /// (and/or a SaySomethingAction) so an NPC that was hit leans into confrontation.
    /// </summary>
    [Serializable]
    public class AngerDataSource : IDataSource
    {
        public float GetValue(GameObject agent)
        {
            var health = agent.GetComponent<Health>();
            return health != null ? health.Anger : 0f;
        }
    }
}
