using UnityEngine;

namespace SolarHarmony.DynamicWounds2D
{
    public class WoundImpact : MonoBehaviour
    {
        [SerializeField] private int _colorSampleCount = 5;
        [SerializeField] private float _sampleRadius = 0.02f;

        private ParticleSystem _particles;
        private bool _initialized;

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
        }

        public void Initialize(Vector2 hitUV, CharacterAtlasData atlasData, float layerDepth,
            Vector3 worldNormal)
        {
            if (_initialized) return;
            _initialized = true;

            if (_particles == null) return;

            Color[] samples = SampleLayerColors(hitUV, atlasData, layerDepth);
            if (samples != null && samples.Length > 0)
            {
                ApplyColorsToParticles(samples);
            }

            if (worldNormal.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(worldNormal);

            _particles.Play(true);
        }

        public void ResetForPool()
        {
            _initialized = false;
        }

        private Color[] SampleLayerColors(Vector2 uv, CharacterAtlasData atlasData, float depth)
        {
            var sourceTex = atlasData.SourceTexture;
            if (sourceTex == null || !sourceTex.isReadable) return null;

            int layerIndex = Mathf.FloorToInt(depth);
            layerIndex = Mathf.Clamp(layerIndex, 0, atlasData.LayerCount - 1);

            if (atlasData.LayerSprites.Count == 0) return null;
            Sprite baseSprite = atlasData.LayerSprites[0];
            if (baseSprite == null) return null;

            float texW = sourceTex.width;
            float texH = sourceTex.height;

            float baseU = baseSprite.rect.x / texW;
            float baseV = baseSprite.rect.y / texH;
            float baseWidth = baseSprite.rect.width / texW;
            float baseHeight = baseSprite.rect.height / texH;

            float texU = baseU + uv.x * baseWidth;
            float texV = baseV + uv.y * baseHeight;

            Vector2[] deltas = atlasData.GetLayerUVDeltas();
            if (deltas != null && layerIndex < deltas.Length)
            {
                texU += deltas[layerIndex].x;
                texV += deltas[layerIndex].y;
            }

            Color[] samples = new Color[_colorSampleCount];
            for (int i = 0; i < _colorSampleCount; i++)
            {
                float angle = (float)i / _colorSampleCount * Mathf.PI * 2f;
                float r = Random.Range(0f, _sampleRadius);
                float su = Mathf.Clamp01(texU + Mathf.Cos(angle) * r);
                float sv = Mathf.Clamp01(texV + Mathf.Sin(angle) * r);
                samples[i] = sourceTex.GetPixelBilinear(su, sv);
            }

            return samples;
        }

        private void ApplyColorsToParticles(Color[] samples)
        {
            var main = _particles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(samples[0]);

            var col = _particles.colorOverLifetime;
            col.enabled = true;

            var colorKeys = new GradientColorKey[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = Mathf.Min((float)i / Mathf.Max(1, samples.Length - 1), 1f);
                colorKeys[i] = new GradientColorKey(samples[i], t);
            }

            var alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(0f, 1f);

            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            col.color = new ParticleSystem.MinMaxGradient(gradient);
        }
    }
}
