using UnityEngine;

namespace SolarHarmony.DynamicWounds2D
{
    public interface ISeveredPartFactory
    {
        void FinalizeSeveredPart(GameObject severedPart, MeshRenderer sourceRenderer, Vector3 hitDirection);
    }
}
