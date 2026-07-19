using System.Collections.Generic;
using UnityEngine;

namespace SolarHarmony.DynamicWounds2D
{
    public static class BodyPartMeshBuilder
    {
        public static Mesh BuildFromLayer0Sprite(Sprite sprite, Mesh reuseMesh = null)
        {
            if (sprite == null)
            {
                Debug.LogWarning("BodyPartMeshBuilder: No layer 0 sprite.");
                return null;
            }

            Vector2[] spriteVerts = sprite.vertices;
            Vector2[] spriteUVs = sprite.uv;
            ushort[] spriteTri = sprite.triangles;

            if (spriteVerts == null || spriteTri == null) return null;

            int texW = sprite.texture != null ? sprite.texture.width : 1024;
            int texH = sprite.texture != null ? sprite.texture.height : 1024;
            float invTexW = 1f / texW;
            float invTexH = 1f / texH;

            var vertices = new List<Vector3>(spriteVerts.Length);
            var atlasUVs = new List<Vector2>(spriteUVs?.Length ?? spriteVerts.Length);
            var charUVs = new List<Vector2>(spriteVerts.Length);
            var indices = new List<int>(spriteTri.Length);

            Rect rect = sprite.rect;

            for (int v = 0; v < spriteVerts.Length; v++)
            {
                float charX = (rect.x + spriteVerts[v].x) * invTexW;
                float charY = (rect.y + spriteVerts[v].y) * invTexH;

                vertices.Add(new Vector3(charX, charY, 0f));

                atlasUVs.Add(spriteUVs != null && v < spriteUVs.Length
                    ? spriteUVs[v]
                    : new Vector2(charX, charY));

                charUVs.Add(new Vector2(charX, charY));
            }

            for (int t = 0; t < spriteTri.Length; t++)
                indices.Add(spriteTri[t]);

            var mesh = reuseMesh ?? new Mesh();
            mesh.name = "AtlasCharacterMesh";
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();

            var bounds = mesh.bounds;
            if (bounds.size.x > 0f && bounds.size.y > 0f)
            {
                float scaleX = 1f / bounds.size.x;
                float scaleY = 1f / bounds.size.y;
                float offX = -bounds.min.x;
                float offY = -bounds.min.y;

                for (int i = 0; i < vertices.Count; i++)
                {
                    vertices[i] = new Vector3(
                        (vertices[i].x + offX) * scaleX,
                        (vertices[i].y + offY) * scaleY,
                        0f);
                    charUVs[i] = new Vector2(
                        (charUVs[i].x + offX) * scaleX,
                        (charUVs[i].y + offY) * scaleY);
                }

                mesh.SetVertices(vertices);
                mesh.RecalculateBounds();

                var normBounds = mesh.bounds;
                float aspect = normBounds.size.y > 0f ? normBounds.size.x / normBounds.size.y : 1f;
                float cx = normBounds.center.x;
                float cy = normBounds.center.y;
                for (int i = 0; i < vertices.Count; i++)
                {
                    vertices[i] = new Vector3(
                        (vertices[i].x - cx) * aspect,
                        vertices[i].y - cy,
                        0f);
                }

                mesh.SetVertices(vertices);
                mesh.RecalculateBounds();
            }

            mesh.SetUVs(0, atlasUVs);
            mesh.SetUVs(1, charUVs);
            mesh.RecalculateNormals();

            return mesh;
        }

        public static bool[] GenerateConnectivityGrid(Texture2D bodyPartsMask, int gridResolution)
        {
            if (bodyPartsMask == null || gridResolution <= 0) return null;

            bool[] grid = new bool[gridResolution * gridResolution];

            for (int gy = 0; gy < gridResolution; gy++)
            {
                for (int gx = 0; gx < gridResolution; gx++)
                {
                    float u = (gx + 0.5f) / gridResolution;
                    float v = (gy + 0.5f) / gridResolution;
                    Color c = bodyPartsMask.GetPixelBilinear(u, v);
                    grid[gy * gridResolution + gx] = c.maxColorComponent >= 0.01f;
                }
            }

            return grid;
        }
    }
}
