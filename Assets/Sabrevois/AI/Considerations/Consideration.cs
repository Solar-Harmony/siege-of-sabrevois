using System;
using ArtificeToolkit.Attributes;
using Sabrevois.AI.DataSources;
using UnityEngine;

namespace Sabrevois.AI.Considerations
{
    [Serializable]
    public abstract class Consideration
    {
        [SerializeReference] [Required]
        public IDataSource Source;
        
        public abstract float Evaluate(float value);
    }
}