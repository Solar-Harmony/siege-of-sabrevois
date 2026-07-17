using UnityEngine;
using UnityEditor;
using Sabrevois.Gameplay;

namespace Sabrevois.Editor
{
    public class SpriteConnectivityGraphGenerator : EditorWindow
    {
        private Texture2D _sourceTexture;
        private int _gridResolution = 64;
        private float _alphaThreshold = 0.1f;
        private bool _useChromaKey = true;
        private Color _chromaKeyColor = new Color(1f, 0f, 1f, 1f); // Default Magenta
        private float _chromaKeyTolerance = 0.1f;

        [MenuItem("Sabrevois/Generate Sprite Connectivity Graph")]
        public static void ShowWindow()
        {
            GetWindow<SpriteConnectivityGraphGenerator>("Graph Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Generate Connectivity Graph", EditorStyles.boldLabel);
            
            _sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Source Texture", _sourceTexture, typeof(Texture2D), false);
            _gridResolution = EditorGUILayout.IntField("Grid Resolution", _gridResolution);
            
            _useChromaKey = EditorGUILayout.Toggle("Use Chroma Key", _useChromaKey);
            if (_useChromaKey)
            {
                _chromaKeyColor = EditorGUILayout.ColorField("Chroma Key Color", _chromaKeyColor);
                _chromaKeyTolerance = EditorGUILayout.FloatField("Chroma Tolerance", _chromaKeyTolerance);
            }
            else
            {
                _alphaThreshold = EditorGUILayout.FloatField("Alpha Threshold", _alphaThreshold);
            }

            if (GUILayout.Button("Generate Graph Asset"))
            {
                if (_sourceTexture == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign a Source Texture.", "OK");
                    return;
                }

                GenerateGraph();
            }
        }

        private void GenerateGraph()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Graph", _sourceTexture.name + "_Graph", "asset", "Save Connectivity Graph");
            if (string.IsNullOrEmpty(path)) return;

            SpriteConnectivityGraph graph = ScriptableObject.CreateInstance<SpriteConnectivityGraph>();
            graph.Initialize(_gridResolution, _gridResolution);

            // Need to make texture readable to get pixels
            string texPath = AssetDatabase.GetAssetPath(_sourceTexture);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            for (int y = 0; y < _gridResolution; y++)
            {
                for (int x = 0; x < _gridResolution; x++)
                {
                    float u = (x + 0.5f) / _gridResolution;
                    float v = (y + 0.5f) / _gridResolution;
                    
                    Color c = _sourceTexture.GetPixelBilinear(u, v);
                    bool isSolid = false;
                    if (_useChromaKey)
                    {
                        float dist = Vector3.Distance(new Vector3(c.r, c.g, c.b), new Vector3(_chromaKeyColor.r, _chromaKeyColor.g, _chromaKeyColor.b));
                        isSolid = dist > _chromaKeyTolerance;
                    }
                    else
                    {
                        isSolid = c.a > _alphaThreshold;
                    }
                    graph.SetNode(x, y, isSolid);
                }
            }

            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }

            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();
            
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = graph;
            
            Debug.Log($"Connectivity graph generated at {path}");
        }
    }
}