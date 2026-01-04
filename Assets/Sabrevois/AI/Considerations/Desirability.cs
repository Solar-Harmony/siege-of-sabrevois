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
        
        public override float Evaluate(GameObject obj)
        {
            float value = Source.GetValue(obj);
            return Curve.Evaluate(value);
        }
    }
}