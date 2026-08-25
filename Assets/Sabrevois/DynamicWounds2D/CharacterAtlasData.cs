using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SolarHarmony.DynamicWounds2D
{
    [Serializable]
    public struct BodyPartMapping
    {
        public Color Color;
        [FormerlySerializedAs("PartName")]
        [FormerlySerializedAs("HumanFriendlyName")]
        public string Name;
        public bool IsEssential;
        [UnityEngine.Range(0f, 100f)]
        public float ArmourPercent;
    }

    [CreateAssetMenu(fileName = "NewCharacterAtlasData", menuName = "Sabrevois/Character Atlas Data")]
    public class CharacterAtlasData : ScriptableObject
    {
        [SerializeField] private Texture2D _sourceTexture;
        public List<Sprite> LayerSprites = new List<Sprite>();
        public Texture2D BodyPartsMask;
        public List<BodyPartMapping> BodyPartMappings = new List<BodyPartMapping>();

        [Header("Optional PBR Atlases (same layout as SourceTexture)")]
        public Texture2D NormalMap;
        public Texture2D SmoothnessMap;
        public Texture2D GlowMap;

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
#if UNITY_EDITOR
                    OnSourceTextureChanged();
#endif
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
            LayerSprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void AnalyzeBodyPartsMask()
        {
            if (BodyPartsMask == null) return;
            EnsureTextureReadable();

            var pixels = BodyPartsMask.GetPixels();
            int w = BodyPartsMask.width;
            int h = BodyPartsMask.height;

            var found = new HashSet<Color>(new ColorEqualityComparer());
            int stride = Mathf.Max(1, Mathf.Min(w, h) / 64);

            for (int y = 0; y < h; y += stride)
            {
                for (int x = 0; x < w; x += stride)
                {
                    var c = pixels[y * w + x];
                    if (c.r < 0.01f && c.g < 0.01f && c.b < 0.01f) continue;

                    c.r = Mathf.Round(c.r / 0.02f) * 0.02f;
                    c.g = Mathf.Round(c.g / 0.02f) * 0.02f;
                    c.b = Mathf.Round(c.b / 0.02f) * 0.02f;

                    found.Add(c);
                }
            }

            CreateMappingsFromColors(found);
        }

        private void EnsureTextureReadable()
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(BodyPartsMask);
            var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
            if (importer == null) return;

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        private void CreateMappingsFromColors(HashSet<Color> foundColors)
        {
            var used = new bool[BodyPartMappings.Count];
            var ceq = new ColorEqualityComparer();
            var newMappings = new List<BodyPartMapping>();

            foreach (var color in foundColors)
            {
                bool matched = false;
                for (int i = 0; i < BodyPartMappings.Count; i++)
                {
                    if (used[i]) continue;
                    if (ceq.Equals(color, BodyPartMappings[i].Color))
                    {
                        used[i] = true;
                        var m = BodyPartMappings[i];
                        m.Color = color;
                        newMappings.Add(m);
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    newMappings.Add(new BodyPartMapping
                    {
                        Color = color,
                        Name = "Part_" + (newMappings.Count + 1),
                        IsEssential = false,
                        ArmourPercent = 0f
                    });
                }
            }

            BodyPartMappings = newMappings;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public class ColorEqualityComparer : IEqualityComparer<Color>
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
