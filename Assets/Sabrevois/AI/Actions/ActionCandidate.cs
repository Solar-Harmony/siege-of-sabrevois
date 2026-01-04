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
        [SerializeReference]
        public IActionConfig ActionConfig;
        
        public List<Precondition> Preconditions;
        public List<Desirability> Considerations;
    }
    
    
}