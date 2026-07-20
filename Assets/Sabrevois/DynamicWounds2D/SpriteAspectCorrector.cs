using UnityEngine;
using Newtonsoft.Json;

namespace SolarHarmony.DynamicWounds2D
{
    public class SpriteAspectCorrector : MonoBehaviour
    {
        [SerializeField] private CharacterAtlasData _atlasData;

        private void Start()
        {
            if (_atlasData == null) return;

            var sprites = _atlasData.LayerSprites;
            if (sprites == null || sprites.Count == 0) return;

            var sprite = sprites[0];
            if (sprite == null || sprite.rect.height <= 0f) return;

            float spriteAspect = sprite.rect.width / sprite.rect.height;
            if (spriteAspect <= 0f || Mathf.Approximately(spriteAspect, 1f)) return;

            var sc = transform.localScale;
            if (spriteAspect > 1f)
                sc.x = sc.y * spriteAspect;
            else
                sc.y = sc.x / spriteAspect;
            transform.localScale = sc;
        }
    }
}
