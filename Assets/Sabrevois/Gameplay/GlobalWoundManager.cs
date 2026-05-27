using System.Collections.Generic;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    public class GlobalWoundManager : MonoBehaviour
    {
        public static GlobalWoundManager Instance { get; private set; }

        [SerializeField] private ComputeShader _woundSplatterCompute;
        [SerializeField] private int _splatmapResolution = 256;
        [SerializeField] private int _maxSlices = 512;

        private RenderTexture _splatmapArray;
        private Queue<int> _availableSlices = new Queue<int>();
        private int _splatKernel;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeSplatmap();
        }

        private void InitializeSplatmap()
        {
            _splatmapArray = new RenderTexture(_splatmapResolution, _splatmapResolution, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat);
            _splatmapArray.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
            _splatmapArray.volumeDepth = _maxSlices;
            _splatmapArray.enableRandomWrite = true;
            _splatmapArray.Create();

            // Clear the splatmap initially (Black)
            RenderTexture active = RenderTexture.active;
            for (int i = 0; i < _maxSlices; i++)
            {
                Graphics.SetRenderTarget(_splatmapArray, 0, CubemapFace.Unknown, i);
                GL.Clear(false, true, Color.clear);
            }
            RenderTexture.active = active;

            for (int i = 0; i < _maxSlices; i++)
            {
                _availableSlices.Enqueue(i);
            }

            Shader.SetGlobalTexture("_GlobalWoundSplatmap", _splatmapArray);
            
            if (_woundSplatterCompute != null)
            {
                _splatKernel = _woundSplatterCompute.FindKernel("SplatWound");
                _woundSplatterCompute.SetTexture(_splatKernel, "Splatmap", _splatmapArray);
            }
            else
            {
                Debug.LogError("WoundSplatter compute shader not assigned to GlobalWoundManager!");
            }
        }

        public int RequestSlice()
        {
            if (_availableSlices.Count > 0)
            {
                int sliceIndex = _availableSlices.Dequeue();
                
                // Clear the slice when granting it
                RenderTexture active = RenderTexture.active;
                Graphics.SetRenderTarget(_splatmapArray, 0, CubemapFace.Unknown, sliceIndex);
                GL.Clear(false, true, Color.clear);
                RenderTexture.active = active;
                
                return sliceIndex;
            }
            
            Debug.LogWarning("GlobalWoundManager ran out of splatmap slices!");
            return -1;
        }

        public void ReleaseSlice(int sliceIndex)
        {
            if (sliceIndex >= 0 && sliceIndex < _maxSlices)
            {
                _availableSlices.Enqueue(sliceIndex);
            }
        }

        public void AddWoundSplat(int sliceIndex, Vector2 uv, float radius, float penetration, Vector2 quadSize)
        {
            if (sliceIndex < 0 || sliceIndex >= _maxSlices || _woundSplatterCompute == null)
                return;

            _woundSplatterCompute.SetVector("HitUV", new Vector4(uv.x, uv.y, 0, 0));
            _woundSplatterCompute.SetVector("QuadSize", new Vector4(quadSize.x, quadSize.y, 0, 0));
            _woundSplatterCompute.SetFloat("Radius", radius);
            _woundSplatterCompute.SetFloat("Penetration", penetration);
            _woundSplatterCompute.SetInt("SliceIndex", sliceIndex);

            int threadGroups = Mathf.CeilToInt(_splatmapResolution / 8f);
            _woundSplatterCompute.Dispatch(_splatKernel, threadGroups, threadGroups, 1);
        }

        private void OnDestroy()
        {
            if (_splatmapArray != null)
            {
                _splatmapArray.Release();
                Destroy(_splatmapArray);
            }
        }
    }
}
