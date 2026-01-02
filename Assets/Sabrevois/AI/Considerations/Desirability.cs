using System;
using UnityEngine;

namespace Sabrevois.AI.Considerations
{
    [Serializable]
    public class Desirability : Consideration
    {
        public float MinValue = 0f;
        public float MaxValue = 1f;
        public AnimationCurve Curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public override float Evaluate(GameObject obj)
        {
            float value = Source.GetValue(obj);
            float clampedValue = Mathf.Clamp(value, MinValue, MaxValue);
            float normalizedValue = (clampedValue - MinValue) / (MaxValue - MinValue);
            return Curve.Evaluate(normalizedValue);
        }
    }
}