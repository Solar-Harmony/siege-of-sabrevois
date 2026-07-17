using UnityEngine;
using System.Collections.Generic;

namespace Sabrevois.Gameplay
{
    [CreateAssetMenu(fileName = "NewConnectivityGraph", menuName = "Sabrevois/Sprite Connectivity Graph")]
    public class SpriteConnectivityGraph : ScriptableObject
    {
        public int Width;
        public int Height;
        
        [HideInInspector]
        public bool[] Nodes;

        public void Initialize(int width, int height)
        {
            Width = width;
            Height = height;
            Nodes = new bool[width * height];
        }

        public void SetNode(int x, int y, bool isSolid)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                Nodes[y * Width + x] = isSolid;
            }
        }

        public bool GetNode(int x, int y)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                return Nodes[y * Width + x];
            }
            return false;
        }
    }
}