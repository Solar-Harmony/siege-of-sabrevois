using System.Collections.Generic;
using UnityEngine;

namespace SolarHarmony.DynamicWounds2D
{
    public class GlobalWoundManager : MonoBehaviour
    {
        public static GlobalWoundManager Instance { get; private set; }

        [SerializeField] private ComputeShader _woundSplatterCompute;
        [SerializeField] private int _splatmapResolution = 256;
        [SerializeField] private int _initialSlices = 64;

        private RenderTexture _splatmapArray;
        private Queue<int> _availableSlices = new Queue<int>();
        private int _splatKernel;
        private int _totalSlices;

        public int TotalSliceCount => _totalSlices;
        public int AvailableSliceCount => _availableSlices.Count;

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
            _totalSlices = _initialSlices;
            CreateSplatmapTexture();

            for (int i = 0; i < _totalSlices; i++)
                _availableSlices.Enqueue(i);

            Shader.SetGlobalTexture("_GlobalWoundSplatmap", _splatmapArray);
            Shader.SetGlobalVector("_GlobalWoundSplatmap_TexelSize",
                new Vector4(1f / _splatmapResolution, 1f / _splatmapResolution, _splatmapResolution, _splatmapResolution));

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

        private void CreateSplatmapTexture()
        {
            if (_splatmapArray != null)
            {
                _splatmapArray.Release();
                Destroy(_splatmapArray);
            }

            _splatmapArray = new RenderTexture(_splatmapResolution, _splatmapResolution, 0,
                UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16_SFloat);
            _splatmapArray.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
            _splatmapArray.volumeDepth = _totalSlices;
            _splatmapArray.enableRandomWrite = true;
            _splatmapArray.Create();

            RenderTexture active = RenderTexture.active;
            for (int i = 0; i < _totalSlices; i++)
            {
                Graphics.SetRenderTarget(_splatmapArray, 0, CubemapFace.Unknown, i);
                GL.Clear(false, true, Color.clear);
            }
            RenderTexture.active = active;
        }

        public int RequestSlice()
        {
            if (_availableSlices.Count == 0)
            {
                GrowSplatmap();
            }

            if (_availableSlices.Count > 0)
            {
                int sliceIndex = _availableSlices.Dequeue();

                RenderTexture active = RenderTexture.active;
                Graphics.SetRenderTarget(_splatmapArray, 0, CubemapFace.Unknown, sliceIndex);
                GL.Clear(false, true, Color.clear);
                RenderTexture.active = active;

                return sliceIndex;
            }

            Debug.LogWarning("GlobalWoundManager ran out of splatmap slices even after growth!");
            return -1;
        }

        private void GrowSplatmap()
        {
            int newTotal = _totalSlices * 2;
            RenderTexture oldRT = _splatmapArray;

            RenderTexture newRT = new RenderTexture(_splatmapResolution, _splatmapResolution, 0,
                UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16_SFloat);
            newRT.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
            newRT.volumeDepth = newTotal;
            newRT.enableRandomWrite = true;
            newRT.Create();

            for (int i = 0; i < _totalSlices; i++)
                Graphics.CopyTexture(oldRT, i, 0, 0, 0, _splatmapResolution, _splatmapResolution,
                    newRT, i, 0, 0, 0);

            RenderTexture active = RenderTexture.active;
            for (int i = _totalSlices; i < newTotal; i++)
            {
                Graphics.SetRenderTarget(newRT, 0, CubemapFace.Unknown, i);
                GL.Clear(false, true, Color.clear);
            }
            RenderTexture.active = active;

            for (int i = _totalSlices; i < newTotal; i++)
                _availableSlices.Enqueue(i);

            _splatmapArray = newRT;
            _totalSlices = newTotal;

            Shader.SetGlobalTexture("_GlobalWoundSplatmap", _splatmapArray);
            if (_woundSplatterCompute != null)
                _woundSplatterCompute.SetTexture(_splatKernel, "Splatmap", _splatmapArray);

            oldRT.Release();
            Destroy(oldRT);
        }

        public void ReleaseSlice(int sliceIndex)
        {
            if (sliceIndex >= 0 && sliceIndex < _totalSlices)
                _availableSlices.Enqueue(sliceIndex);
        }

        public void AddWoundSplat(int sliceIndex, Vector2 uv, float radius, float penetration, Vector2 quadSize, float bloodRatio = 0f)
        {
            if (sliceIndex < 0 || sliceIndex >= _totalSlices || _woundSplatterCompute == null)
                return;

            _woundSplatterCompute.SetVector("HitUV", new Vector4(uv.x, uv.y, 0, 0));
            _woundSplatterCompute.SetVector("QuadSize", new Vector4(quadSize.x, quadSize.y, 0, 0));
            _woundSplatterCompute.SetFloat("Radius", radius);
            _woundSplatterCompute.SetFloat("Penetration", penetration);
            _woundSplatterCompute.SetFloat("BloodRatio", bloodRatio);
            _woundSplatterCompute.SetInt("SliceIndex", sliceIndex);

            int threadGroups = Mathf.CeilToInt(_splatmapResolution / 8f);
            _woundSplatterCompute.Dispatch(_splatKernel, threadGroups, threadGroups, 1);
            GL.Flush();
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
