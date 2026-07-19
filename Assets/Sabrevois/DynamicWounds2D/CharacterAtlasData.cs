using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolarHarmony.DynamicWounds2D
{
    [Serializable]
    public struct BodyPartMapping
    {
        public Color Color;
        public string PartName;
        public bool IsEssential;
    }

    [CreateAssetMenu(fileName = "NewCharacterAtlasData", menuName = "Sabrevois/Character Atlas Data")]
    public class CharacterAtlasData : ScriptableObject
    {
        [SerializeField] private Texture2D _sourceTexture;
        public List<Sprite> LayerSprites = new List<Sprite>();
        public Texture2D BodyPartsMask;
        public List<BodyPartMapping> BodyPartMappings = new List<BodyPartMapping>();

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
#if UNITY_EDITOR
            if (_sourceTexture != null)
                UnityEditor.EditorApplication.delayCall += SyncSpritesFromTexture;
#endif
        }

        public void SyncSpritesFromTexture()
        {
#if UNITY_EDITOR
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
#endif
        }
#endif
    }
}
