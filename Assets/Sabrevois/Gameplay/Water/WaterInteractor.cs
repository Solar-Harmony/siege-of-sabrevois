using UnityEngine;

namespace Sabrevois.Level.Water
{
    public class WaterInteractor : MonoBehaviour
    {
        public float radiusMultiplier = 1.0f;
        public float strengthMultiplier = 1.0f;
        
        [HideInInspector]
        public Vector3 lastPos;
    }
}

