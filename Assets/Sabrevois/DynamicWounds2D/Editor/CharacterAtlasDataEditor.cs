using UnityEditor;
using UnityEngine;

namespace SolarHarmony.DynamicWounds2D.Editor
{
    [CustomEditor(typeof(CharacterAtlasData))]
    public class CharacterAtlasDataEditor : UnityEditor.Editor
    {
        private bool _isAnalyzing;

        public override void OnInspectorGUI()
        {
            var data = (CharacterAtlasData)target;

            EditorGUI.BeginChangeCheck();
            var tex = (Texture2D)EditorGUILayout.ObjectField(
                "Source Texture", data.SourceTexture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Change Source Texture");
                data.SourceTexture = tex;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Sync Sprites from Texture"))
                data.SyncSpritesFromTexture();
            if (GUILayout.Button("Sort by Name"))
            {
                Undo.RecordObject(data, "Sort Sprites");
                data.LayerSprites.Sort((a, b) =>
                    string.Compare(a != null ? a.name : "", b != null ? b.name : ""));
                EditorUtility.SetDirty(data);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                $"Layer Sprites ({data.LayerSprites.Count}):",
                EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            for (int i = 0; i < data.LayerSprites.Count; i++)
            {
                var s = data.LayerSprites[i];
                EditorGUILayout.LabelField(
                    $"{i}:", s != null ? s.name : "(null)");
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Body Parts Mask", EditorStyles.boldLabel);
            if (data.BodyPartsMask != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.ObjectField("Mask Texture", data.BodyPartsMask, typeof(Texture2D), false);
                EditorGUILayout.LabelField(
                    $"Detected Parts: {(data.BodyPartMappings != null ? data.BodyPartMappings.Count : 0)} / {CharacterAtlasData.MaxBodyPartCount}");
                EditorGUI.indentLevel--;
            }

            EditorGUI.BeginDisabledGroup(_isAnalyzing);
            if (GUILayout.Button(_isAnalyzing ? "Analyzing..." : "Analyze Body Parts Mask"))
            {
                Undo.RecordObject(data, "Analyze Body Parts Mask");
                _isAnalyzing = true;
                data.AnalyzeBodyPartsMaskAsync(
                    progress =>
                    {
                        if (EditorUtility.DisplayCancelableProgressBar(
                            "Analyzing Body Parts Mask",
                            $"Scanning pixels... {Mathf.RoundToInt(progress * 100)}%",
                            progress))
                        {
                            _isAnalyzing = false;
                            EditorUtility.ClearProgressBar();
                        }
                    },
                    () =>
                    {
                        _isAnalyzing = false;
                        EditorUtility.ClearProgressBar();
                        EditorUtility.SetDirty(data);
                        Repaint();
                    });
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject,
                "m_Script", "_sourceTexture", "LayerSprites", "BodyPartsMask", "BodyPartMappings");
            serializedObject.ApplyModifiedProperties();
        }

        private void OnDisable()
        {
            if (_isAnalyzing)
                EditorUtility.ClearProgressBar();
            _isAnalyzing = false;
        }
    }
}
