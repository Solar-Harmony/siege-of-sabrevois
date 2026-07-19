using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarHarmony.DynamicWounds2D
{
    [Serializable]
    public struct BodyPartMapping
    {
        [HideInInspector] public Color Color;
        public string PartName;
        public bool IsEssential;
    }

    [CreateAssetMenu(fileName = "NewCharacterAtlasData", menuName = "Sabrevois/Character Atlas Data")]
    public class CharacterAtlasData : ScriptableObject
    {
        private static readonly float[] PresetValues = { 0.0f, 0.5f, 0.75f, 1.0f };
        private static readonly Color FillerColor = Color.black;
        private const float ColorMatchTolerance = 0.1f;
        private const int ChunkSize = 4096;

        [SerializeField] private Texture2D _sourceTexture;
        public List<Sprite> LayerSprites = new List<Sprite>();
        public Texture2D BodyPartsMask;
        public List<BodyPartMapping> BodyPartMappings = new List<BodyPartMapping>();

        public static IReadOnlyList<float> BodyPartPresetValues => PresetValues;
        public static int MaxBodyPartCount => PresetValues.Length * PresetValues.Length * PresetValues.Length;

        public void AnalyzeBodyPartsMask()
        {
            if (BodyPartsMask == null) return;

#if UNITY_EDITOR
            EnsureTextureReadableAndLinear();
#endif

            var pixels = BodyPartsMask.GetPixels();
            var mappings = BuildMappingsFromPixels(pixels);
            ApplyPreservedMappings(mappings);
        }

#if UNITY_EDITOR
        public void AnalyzeBodyPartsMaskAsync(Action<float> onProgress, Action onComplete)
        {
            if (BodyPartsMask == null)
            {
                onComplete?.Invoke();
                return;
            }

            EnsureTextureReadableAndLinear();

            var pixels = BodyPartsMask.GetPixels();
            var foundSet = new HashSet<Color>(new ColorEqualityComparer());
            var mappings = new List<BodyPartMapping>();
            int total = pixels.Length;
            int processed = 0;
            bool finished = false;

            UnityEditor.EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                int end = Mathf.Min(processed + ChunkSize, total);
                for (int i = processed; i < end; i++)
                {
                    if (TryMatchPresetColor(pixels[i], out var matched))
                        foundSet.Add(matched);
                }
                processed = end;

                float progress = (float)processed / total;
                onProgress?.Invoke(progress);

                if (processed >= total && !finished)
                {
                    finished = true;
                    UnityEditor.EditorApplication.update -= step;
                    mappings = CreateMappingsFromColors(foundSet);
                    ApplyPreservedMappings(mappings);
                    onComplete?.Invoke();
                }
            };

            UnityEditor.EditorApplication.update += step;
        }

        private void EnsureTextureReadableAndLinear()
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(BodyPartsMask);
            var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                dirty = true;
            }
            if (importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                dirty = true;
            }
            if (dirty)
                importer.SaveAndReimport();
        }
#endif

        private static bool TryMatchPresetColor(Color pixel, out Color matched)
        {
            if (ColorsMatch(pixel, FillerColor))
            {
                matched = default;
                return false;
            }

            float r = SnapToNearestPreset(pixel.r);
            float g = SnapToNearestPreset(pixel.g);
            float b = SnapToNearestPreset(pixel.b);

            if (Mathf.Abs(r - pixel.r) <= ColorMatchTolerance &&
                Mathf.Abs(g - pixel.g) <= ColorMatchTolerance &&
                Mathf.Abs(b - pixel.b) <= ColorMatchTolerance)
            {
                matched = new Color(r, g, b, 1f);
                return true;
            }

            matched = default;
            return false;
        }

        private static bool ColorsMatch(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) <= ColorMatchTolerance
                && Mathf.Abs(a.g - b.g) <= ColorMatchTolerance
                && Mathf.Abs(a.b - b.b) <= ColorMatchTolerance;
        }

        private static float SnapToNearestPreset(float value)
        {
            float best = PresetValues[0];
            float bestDist = Mathf.Abs(value - best);
            for (int i = 1; i < PresetValues.Length; i++)
            {
                float dist = Mathf.Abs(value - PresetValues[i]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = PresetValues[i];
                }
            }
            return best;
        }

        private List<BodyPartMapping> BuildMappingsFromPixels(Color[] pixels)
        {
            var foundSet = new HashSet<Color>(new ColorEqualityComparer());
            for (int i = 0; i < pixels.Length; i++)
            {
                if (TryMatchPresetColor(pixels[i], out var matched))
                    foundSet.Add(matched);
            }
            return CreateMappingsFromColors(foundSet);
        }

        private List<BodyPartMapping> CreateMappingsFromColors(HashSet<Color> foundColors)
        {
            var existingByKey = new Dictionary<string, BodyPartMapping>();
            foreach (var m in BodyPartMappings)
            {
                var key = ColorToKey(m.Color);
                if (!string.IsNullOrEmpty(key) && !existingByKey.ContainsKey(key))
                    existingByKey[key] = m;
            }

            var newMappings = new List<BodyPartMapping>();
            foreach (var color in foundColors)
            {
                var colorKey = ColorToKey(color);
                if (existingByKey.TryGetValue(colorKey, out var existing))
                {
                    existing.Color = color;
                    newMappings.Add(existing);
                }
                else
                {
                    newMappings.Add(new BodyPartMapping
                    {
                        Color = color,
                        PartName = $"Part_{colorKey}",
                        IsEssential = false
                    });
                }
            }
            return newMappings;
        }

        private void ApplyPreservedMappings(List<BodyPartMapping> mappings)
        {
            BodyPartMappings = mappings;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private static string ColorToKey(Color c)
        {
            return $"R{c.r:F2}_G{c.g:F2}_B{c.b:F2}";
        }

        private class ColorEqualityComparer : IEqualityComparer<Color>
        {
            public bool Equals(Color a, Color b)
            {
                return Mathf.Abs(a.r - b.r) < 0.001f
                    && Mathf.Abs(a.g - b.g) < 0.001f
                    && Mathf.Abs(a.b - b.b) < 0.001f;
            }

            public int GetHashCode(Color c)
            {
                return ((int)(c.r * 1000)) ^ ((int)(c.g * 1000) << 10) ^ ((int)(c.b * 1000) << 20);
            }
        }

        public Texture2D SourceTexture
        {
            get => _sourceTexture;
            set
            {
                if (_sourceTexture != value)
                {
                    _sourceTexture = value;
                    OnSourceTextureChanged();
                }
            }
        }

        public int LayerCount => LayerSprites.Count;

        public Vector2[] GetLayerUVDeltas()
        {
            int count = LayerSprites.Count;
            if (count == 0) return null;

            var deltas = new Vector2[count];
            deltas[0] = Vector2.zero;

            if (count > 1 && LayerSprites[0] != null && LayerSprites[0].texture != null)
            {
                float invW = 1f / LayerSprites[0].texture.width;
                float invH = 1f / LayerSprites[0].texture.height;
                var r0 = LayerSprites[0].rect;

                for (int i = 1; i < count; i++)
                {
                    if (LayerSprites[i] == null) continue;
                    var ri = LayerSprites[i].rect;
                    deltas[i] = new Vector2(
                        (ri.x - r0.x) * invW,
                        (ri.y - r0.y) * invH);
                }
            }

            return deltas;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_sourceTexture != null && LayerSprites.Count == 0)
                UnityEditor.EditorApplication.delayCall += SyncSpritesFromTexture;
        }

        private void OnSourceTextureChanged()
        {
            if (_sourceTexture != null)
                UnityEditor.EditorApplication.delayCall += SyncSpritesFromTexture;
        }

        public void SyncSpritesFromTexture()
        {
            if (_sourceTexture == null) return;
            string path = UnityEditor.AssetDatabase.GetAssetPath(_sourceTexture);
            if (string.IsNullOrEmpty(path)) return;

            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            LayerSprites.Clear();
            foreach (var obj in assets)
            {
                if (obj is Sprite s)
                    LayerSprites.Add(s);
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
