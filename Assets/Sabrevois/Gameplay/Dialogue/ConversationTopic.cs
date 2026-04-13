using System;
using Sabrevois.AI.Considerations;
using Sabrevois.AI.DataSources;
using UnityEngine;

namespace Sabrevois.Gameplay.Dialogue
{
    [Serializable]
    public record ConversationTopic
    {
        [SerializeField]
        public string[] Responses;
        
        [SerializeField]
        public string[] FollowUps;

        [SerializeReference] 
        public IDataSource Condition;
    }
}