using System;
using Sabrevois.AI.Considerations;
using UnityEngine;

namespace Sabrevois.AI.Actions
{
    [Serializable]
    public class ActionCandidate
    {
        [SerializeReference]
        public Type ActionType;
        
        [SerializeReference]
        public object Config;
        
        [Tooltip("Conditions that must be met for the action to be considered.")]
        public Precondition[] Preconditions;
        
        [Tooltip("Considerations that influence the desirability (utility score) of the action.")]
        public Desirability[] Considerations;
    }
}