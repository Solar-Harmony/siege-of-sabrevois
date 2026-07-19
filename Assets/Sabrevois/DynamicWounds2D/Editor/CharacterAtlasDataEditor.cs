using UnityEditor;
using UnityEngine;

namespace SolarHarmony.DynamicWounds2D.Editor
{
    [CustomEditor(typeof(CharacterAtlasData))]
    public class CharacterAtlasDataEditor : UnityEditor.Editor
    {
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
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject,
                "m_Script", "_sourceTexture", "LayerSprites");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
