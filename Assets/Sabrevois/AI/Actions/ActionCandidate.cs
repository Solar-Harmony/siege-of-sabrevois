using System;
using System.Collections.Generic;
using ArtificeToolkit.Attributes;
using Sabrevois.AI.Considerations;
using UnityEngine;

namespace Sabrevois.AI.Actions
{
    [Serializable]
    public class ActionCandidate
    {
        [SerializeReference] [Required]
        public IAction Action;
        
        // [Tooltip("Conditions that must be met for the action to be considered.")]
        public List<Precondition> Preconditions;
        
        // [Tooltip("Factors that influence the action's desirability.")]
        public List<Desirability> Considerations;
    }
}