using UnityEngine;
using UnityEditor;
using System.IO;

public class TextureArrayCreator
{
    [MenuItem("Assets/Create/Texture2DArray From Selection", false, 10)]
    public static void CreateTextureArray()
    {
        Object[] selectedObjects = Selection.GetFiltered(typeof(Texture2D), SelectionMode.Assets);
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("Please select at least one Texture2D to create an array.");
            return;
        }

        // Sort by name to preserve order in the array
        System.Array.Sort(selectedObjects, (a, b) => a.name.CompareTo(b.name));

        Texture2D firstTex = selectedObjects[0] as Texture2D;
        int width = firstTex.width;
        int height = firstTex.height;
        TextureFormat format = firstTex.format;
        int mipCount = firstTex.mipmapCount;

        Texture2DArray texArray = new Texture2DArray(width, height, selectedObjects.Length, firstTex.graphicsFormat, UnityEngine.Experimental.Rendering.TextureCreationFlags.MipChain);
        texArray.filterMode = firstTex.filterMode;
        texArray.wrapMode = firstTex.wrapMode;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            Texture2D tex = selectedObjects[i] as Texture2D;
            if (tex.width != width || tex.height != height)
            {
                Debug.LogError($"Texture '{tex.name}' does not match the dimensions ({width}x{height}) of the first texture. Creation aborted.");
                return;
            }
            if (tex.graphicsFormat != firstTex.graphicsFormat)
            {
                Debug.LogError($"Texture '{tex.name}' format ({tex.graphicsFormat}) does not match first texture ({firstTex.graphicsFormat}). Creation aborted.");
                return;
            }

            for (int m = 0; m < tex.mipmapCount; m++)
            {
                Graphics.CopyTexture(tex, 0, m, texArray, i, m);
            }
        }
        
        string path = AssetDatabase.GetAssetPath(firstTex);
        string directory = Path.GetDirectoryName(path);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(directory + "/NewTextureArray.asset");

        AssetDatabase.CreateAsset(texArray, assetPath);
        AssetDatabase.SaveAssets();

        Selection.activeObject = texArray;
        Debug.Log($"Created Texture2DArray with {selectedObjects.Length} slices at {assetPath}");
    }
}
