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
        [Range(0f, 100f)]
        public float ArmourPercent;
    }

    [CreateAssetMenu(fileName = "NewCharacterAtlasData", menuName = "Sabrevois/Character Atlas Data")]
    public class CharacterAtlasData : ScriptableObject
    {
        [SerializeField] private Texture2D _sourceTexture;
        public List<Sprite> LayerSprites = new List<Sprite>();
        public Texture2D BodyPartsMask;
        public List<BodyPartMapping> BodyPartMappings = new List<BodyPartMapping>();

        public float GetBodyPartArmour(int bodyPartIndex)
        {
            if (bodyPartIndex < 0 || bodyPartIndex >= BodyPartMappings.Count) return 0f;
            return BodyPartMappings[bodyPartIndex].ArmourPercent;
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

        public void AnalyzeBodyPartsMask()
        {
            if (BodyPartsMask == null) return;
            EnsureTextureReadableAndLinear();

            var presets = new float[] { 0f, 0.5f, 0.75f, 1f };
            var foundSet = new HashSet<Color>(new ColorEqualityComparer());
            int res = 64;

            for (int gy = 0; gy < res; gy++)
            {
                for (int gx = 0; gx < res; gx++)
                {
                    float u = (gx + 0.5f) / res;
                    float v = (gy + 0.5f) / res;
                    Color c = BodyPartsMask.GetPixelBilinear(u, v);

                    c.r = SnapToPreset(c.r, presets);
                    c.g = SnapToPreset(c.g, presets);
                    c.b = SnapToPreset(c.b, presets);

                    if (c.r < 0.01f && c.g < 0.01f && c.b < 0.01f) continue;

                    foundSet.Add(new Color(c.r, c.g, c.b, 1f));
                }
            }

            var validated = new List<Color>();
            foreach (var preset in foundSet)
            {
                if (HasRawPixelMatch(preset, 0.1f))
                    validated.Add(preset);
            }

            CreateMappingsFromColors(new HashSet<Color>(validated, new ColorEqualityComparer()));
        }

        private bool HasRawPixelMatch(Color target, float tolerance)
        {
            var pixels = BodyPartsMask.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (Mathf.Abs(pixels[i].r - target.r) <= tolerance &&
                    Mathf.Abs(pixels[i].g - target.g) <= tolerance &&
                    Mathf.Abs(pixels[i].b - target.b) <= tolerance)
                    return true;
            }
            return false;
        }

        private static float SnapToPreset(float value, float[] presets)
        {
            float best = presets[0];
            float bestDist = Mathf.Abs(value - best);
            for (int i = 1; i < presets.Length; i++)
            {
                float dist = Mathf.Abs(value - presets[i]);
                if (dist < bestDist) { bestDist = dist; best = presets[i]; }
            }
            return best;
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

        private void CreateMappingsFromColors(HashSet<Color> foundColors)
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
                        PartName = "Part_" + newMappings.Count,
                        IsEssential = false,
                        ArmourPercent = 0f
                    });
                }
            }
            BodyPartMappings = newMappings;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private static string ColorToKey(Color c)
        {
            return $"{c.r:F3}_{c.g:F3}_{c.b:F3}";
        }

        private class ColorEqualityComparer : IEqualityComparer<Color>
        {
            public bool Equals(Color a, Color b)
            {
                return Mathf.Abs(a.r - b.r) < 0.02f
                    && Mathf.Abs(a.g - b.g) < 0.02f
                    && Mathf.Abs(a.b - b.b) < 0.02f;
            }

            public int GetHashCode(Color c)
            {
                return ((int)(c.r * 50)) ^ ((int)(c.g * 50) << 10) ^ ((int)(c.b * 50) << 20);
            }
        }
#endif
    }
}
