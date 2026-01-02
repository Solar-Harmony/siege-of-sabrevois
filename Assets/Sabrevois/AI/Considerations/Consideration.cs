using System;
using ArtificeToolkit.Attributes;
using UnityEngine;

namespace Sabrevois.AI.Considerations
{
    [Serializable]
    public abstract class Consideration
    {
        [SerializeReference]
        protected IConsiderationSource Source;
        
        public abstract float Evaluate(GameObject agent);
    }
}