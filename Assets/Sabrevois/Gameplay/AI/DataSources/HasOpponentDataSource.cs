using System;
using Sabrevois.AI.DataSources;
using Sabrevois.Gameplay.AI;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    [Serializable]
    public class HasOpponentDataSource : IDataSource
    {
        public float GetValue(GameObject agent)
        {
            var opponentComponent = agent.GetComponent<OpponentComponent>();
            return opponentComponent != null && opponentComponent.CurrentOpponent != null ? 1f : 0f;
        }
    }
}