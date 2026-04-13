using System;
using ArtificeToolkit.Attributes;
using UnityEngine;

namespace Sabrevois.AI.Considerations
{
    [Serializable]
    public class Desirability : Consideration
    {
        [Tooltip("Non normalized curve. Maps source value to action desirability score.")]
        public AnimationCurve Curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        
        public override float Evaluate(float value)
        {
            return Curve.Evaluate(value);
        }
    }
}