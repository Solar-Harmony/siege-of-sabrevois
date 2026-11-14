using UnityEngine;
using Newtonsoft.Json;

namespace SolarHarmony.DynamicWounds2D
{
    public class SpriteAspectCorrector : MonoBehaviour
    {
        [SerializeField] private CharacterAtlasData _atlasData;

        private void OnEnable()
        {
            // Keep the editor preview on the exact same scale play mode ends up with,
            // otherwise the sprite (and its feet) sits differently between edit and play.
            if (!Application.isPlaying)
                ApplyAspect();
        }

        private void Start()
        {
            ApplyAspect();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                ApplyAspect();
        }
#endif

        private void ApplyAspect()
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

            // The character mesh is a unit quad centered on this transform, so its bottom edge
            // (the feet) sits half the scaled height below the origin. Pin that edge to the parent
            // origin, otherwise the aspect stretch pushes the feet below the root and the NPC sinks
            // into the ground once the nav agent seats the root on the navmesh.
            var pos = transform.localPosition;
            pos.y = sc.y * 0.5f;

            // Never touch the transform when the values are already correct; this runs in the
            // editor too and must not dirty prefabs/scenes that were already baked.
            if (sc == transform.localScale && pos == transform.localPosition) return;

            transform.localScale = sc;
            transform.localPosition = pos;
        }
    }
}
