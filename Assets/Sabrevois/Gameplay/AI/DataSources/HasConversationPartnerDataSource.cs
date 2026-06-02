using System;
using Sabrevois.AI.DataSources;
using Sabrevois.Gameplay.AI;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.DataSources
{
    [Serializable]
    public class HasConversationPartnerDataSource : IDataSource
    {
        public float GetValue(GameObject agent)
        {
            var conversationComponent = agent.GetComponent<ConversationComponent>();
            return conversationComponent != null && conversationComponent.ConversationPartner != null ? 1f : 0f;
        }
    }
}