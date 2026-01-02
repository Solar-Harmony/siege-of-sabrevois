using UnityEngine;

namespace Sabrevois.AI.Considerations
{
    public interface IConsiderationSource
    {
        public float GetValue(GameObject agent);
    }
}