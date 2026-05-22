using UnityEngine;

namespace Sabrevois.Level.Reflections
{
    public class RealtimeReflectionProbe : MonoBehaviour
    {
        private static readonly int PlayerCubemapId = Shader.PropertyToID("_PlayerCubemap");
        
        public ReflectionProbe Probe;
        
        [Min(0)]
        public float UpdateInterval = 1.0f;

        private void Start()
        {
            InvokeRepeating(nameof(UpdateCubemap), 0f, UpdateInterval);
        }

        private void UpdateCubemap()
        {
            Probe.RenderProbe();
            Cubemap cube = Probe.texture as Cubemap;
            Shader.SetGlobalTexture(PlayerCubemapId, cube);
        }
    }
}