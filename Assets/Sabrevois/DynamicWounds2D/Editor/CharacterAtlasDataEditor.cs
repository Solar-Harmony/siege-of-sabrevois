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
            if (GUILayout.Button("Sync Sprites"))
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
            EditorGUILayout.LabelField($"Layer Sprites ({data.LayerSprites.Count}):", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            for (int i = 0; i < data.LayerSprites.Count; i++)
            {
                var s = data.LayerSprites[i];
                EditorGUILayout.LabelField($"{i}:", s != null ? s.name : "(null)");
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Body Parts Mask", EditorStyles.boldLabel);

            serializedObject.Update();
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("BodyPartsMask"),
                new GUIContent("Mask Texture"));
            serializedObject.ApplyModifiedProperties();

            if (data.BodyPartsMask != null && GUILayout.Button("Detect Parts from Mask"))
            {
                Undo.RecordObject(data, "Detect Body Parts");
                data.AnalyzeBodyPartsMask();
                EditorUtility.SetDirty(data);
            }

            var mappings = data.BodyPartMappings;
            if (mappings != null && mappings.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(
                    $"Detected Parts ({mappings.Count}):",
                    EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                for (int i = 0; i < mappings.Count; i++)
                {
                    var m = mappings[i];

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();

                    var colorRect = GUILayoutUtility.GetRect(18, 18,
                        GUILayout.ExpandWidth(false));
                    EditorGUI.DrawRect(colorRect, m.Color);

                    string rgb = $"({m.Color.r:F1}, {m.Color.g:F1}, {m.Color.b:F1})";
                    EditorGUILayout.LabelField(rgb);

                    m.IsEssential = EditorGUILayout.ToggleLeft(
                        "Essential", m.IsEssential, GUILayout.Width(65));

                    EditorGUILayout.EndHorizontal();

                    m.ArmourPercent = EditorGUILayout.Slider(
                        "Armour", m.ArmourPercent, 0f, 100f);

                    EditorGUILayout.EndVertical();

                    mappings[i] = m;
                }
                EditorGUI.indentLevel--;

                if (GUILayout.Button("Clear Parts"))
                {
                    Undo.RecordObject(data, "Clear Body Parts");
                    mappings.Clear();
                    EditorUtility.SetDirty(data);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PBR Texture Atlases", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "These atlases must have the same sprite layout as the Source Texture. "
                + "Leave unassigned to use procedural normals and default surface values.",
                MessageType.Info);

            serializedObject.Update();
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("NormalMap"),
                new GUIContent("Normal Map"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("SmoothnessMap"),
                new GUIContent("Smoothness Map"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("GlowMap"),
                new GUIContent("Glow Map"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
