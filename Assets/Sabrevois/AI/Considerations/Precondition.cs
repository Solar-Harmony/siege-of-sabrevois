using System;
using ArtificeToolkit.Attributes;
using UnityEngine;

namespace Sabrevois.AI.Considerations
{
    [Serializable]
    public class Precondition : Consideration
    {
        [Serializable]
        public enum Comparison
        {
            Less,
            LessOrEqual,
            Greater,
            GreaterOrEqual,
            Equal,
            NotEqual
        }
        
        [HorizontalGroup(nameof(Operator))]
        public Comparison Operator;
        
        [HorizontalGroup(nameof(Operator))]
        public float Threshold;
        
        public override float Evaluate(GameObject agent)
        {
            float value = Source.GetValue(agent);
            return Operator switch
            {
                Comparison.Less           => value <  Threshold,
                Comparison.LessOrEqual    => value <= Threshold,
                Comparison.Greater        => value >  Threshold,
                Comparison.GreaterOrEqual => value >= Threshold,
                Comparison.Equal          =>  Mathf.Approximately(value, Threshold),
                Comparison.NotEqual       => !Mathf.Approximately(value, Threshold),
                _ => false
            } ? 1f : 0f;
        }
    }
}