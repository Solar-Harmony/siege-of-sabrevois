using System.Collections.Generic;
using ArtificeToolkit.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SolarHarmony.DynamicWounds2D.Editor
{
    [CustomEditor(typeof(CharacterAtlasData))]
    public class CharacterAtlasDataEditor : UnityEditor.Editor
    {
        private ArtificeDrawer _drawer;
        private CharacterAtlasData _data;
        private VisualElement _mapContainer;
        private VisualElement _detailPanel;
        private int _selectedIndex = -1;
        private VisualElement _selectedZone;
        private List<Rect> _partBounds;
        private readonly List<VisualElement> _zones = new();

        public override VisualElement CreateInspectorGUI()
        {
            _drawer?.Dispose();
            _drawer = new ArtificeDrawer();
            _data = (CharacterAtlasData)target;

            var root = new VisualElement();

            root.Add(_drawer.CreatePropertyGUI(serializedObject.FindProperty("_sourceTexture")));
            root.Add(_drawer.CreatePropertyGUI(serializedObject.FindProperty("LayerSprites")));

            var btnRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            btnRow.Add(new Button(() =>
            {
                Undo.RecordObject(_data, "Sync Sprites");
                _data.SyncSpritesFromTexture();
            }) { text = "Sync Sprites" });
            btnRow.style.marginBottom = 8;
            root.Add(btnRow);

            root.Add(_drawer.CreatePropertyGUI(serializedObject.FindProperty("BodyPartsMask")));

            root.Add(new Button(() =>
            {
                Undo.RecordObject(_data, "Detect Body Parts");
                _data.AnalyzeBodyPartsMask();
                EditorUtility.SetDirty(_data);
                ScheduleRebuild();
            }) { text = "Detect Parts from Mask" });

            _mapContainer = new VisualElement();
            _mapContainer.style.marginTop = 6;
            _mapContainer.style.marginBottom = 6;
            root.Add(_mapContainer);

            _detailPanel = new VisualElement();
            root.Add(_detailPanel);

            var pbrLabel = new Label("PBR Texture Atlases");
            pbrLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            pbrLabel.style.marginTop = 8;
            root.Add(pbrLabel);

            root.Add(_drawer.CreatePropertyGUI(serializedObject.FindProperty("NormalMap")));
            root.Add(_drawer.CreatePropertyGUI(serializedObject.FindProperty("SmoothnessMap")));
            root.Add(_drawer.CreatePropertyGUI(serializedObject.FindProperty("GlowMap")));

            root.RegisterCallback<AttachToPanelEvent>(_ => ScheduleRebuild());

            return root;
        }

        private void ScheduleRebuild()
        {
            _mapContainer.schedule.Execute(RebuildMap).StartingIn(100);
        }

        private void RebuildMap()
        {
            _mapContainer.Clear();
            _detailPanel.Clear();
            _detailPanel.style.display = DisplayStyle.None;
            _selectedIndex = -1;
            _selectedZone = null;
            _zones.Clear();

            if (_data.BodyPartsMask == null || _data.BodyPartMappings == null || _data.BodyPartMappings.Count == 0)
                return;

            ComputePartBounds();

            var texture = _data.BodyPartsMask;

            var wrapper = new VisualElement();
            wrapper.style.position = Position.Relative;
            wrapper.style.aspectRatio = (float)texture.width / texture.height;
            wrapper.style.width = Length.Percent(100);
            wrapper.style.maxHeight = 400;
            wrapper.style.marginBottom = 4;

            var image = new Image { image = texture, scaleMode = ScaleMode.StretchToFill };
            image.style.position = Position.Absolute;
            image.style.left = 0;
            image.style.top = 0;
            image.style.right = 0;
            image.style.bottom = 0;
            image.pickingMode = PickingMode.Ignore;
            wrapper.Add(image);

            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            wrapper.Add(overlay);

            for (int i = 0; i < _data.BodyPartMappings.Count; i++)
            {
                if (i >= _partBounds.Count) break;
                var bounds = _partBounds[i];
                if (bounds.width < 0.005f || bounds.height < 0.005f) continue;

                var mapping = _data.BodyPartMappings[i];

                var zone = new VisualElement();
                zone.style.position = Position.Absolute;
                zone.style.left = Length.Percent(bounds.xMin * 100f);
                zone.style.top = Length.Percent((1f - bounds.yMax) * 100f);
                zone.style.width = Length.Percent(bounds.width * 100f);
                zone.style.height = Length.Percent(bounds.height * 100f);

                var zoneColor = mapping.Color;
                zoneColor.a = 0.25f;
                zone.style.backgroundColor = zoneColor;
                zone.style.borderLeftWidth = 1;
                zone.style.borderRightWidth = 1;
                zone.style.borderTopWidth = 1;
                zone.style.borderBottomWidth = 1;
                zone.style.borderLeftColor = new Color(1f, 1f, 1f, 0.5f);
                zone.style.borderRightColor = new Color(1f, 1f, 1f, 0.5f);
                zone.style.borderTopColor = new Color(1f, 1f, 1f, 0.5f);
                zone.style.borderBottomColor = new Color(1f, 1f, 1f, 0.5f);

                var displayName = mapping.Name ?? "";
                if (displayName.StartsWith("Part_") || displayName.StartsWith("Part_R"))
                    displayName = "";

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    var label = new Label(displayName);
                    label.style.color = Color.white;
                    label.style.fontSize = 9;
                    label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    label.style.overflow = Overflow.Hidden;
                    label.style.textOverflow = TextOverflow.Ellipsis;
                    label.style.whiteSpace = WhiteSpace.Normal;
                    label.style.paddingLeft = 2;
                    label.style.paddingRight = 2;
                    zone.Add(label);
                }

                int captured = i;
                zone.RegisterCallback<ClickEvent>(_ => SelectPart(captured));
                overlay.Add(zone);
                _zones.Add(zone);
            }

            _mapContainer.Add(wrapper);
        }

        private void ComputePartBounds()
        {
            _partBounds = new List<Rect>();
            var tex = _data.BodyPartsMask;
            if (tex == null) return;

            var path = AssetDatabase.GetAssetPath(tex);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool wasReadable = importer.isReadable;
            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var pixels = tex.GetPixels();
            int w = tex.width;
            int h = tex.height;
            var mappings = _data.BodyPartMappings;
            var ceq = new CharacterAtlasData.ColorEqualityComparer();

            for (int i = 0; i < mappings.Count; i++)
            {
                var target = mappings[i].Color;
                float minX = w, minY = h, maxX = 0, maxY = 0;
                bool found = false;
                int stride = Mathf.Max(1, Mathf.Min(w, h) / 64);

                for (int y = 0; y < h; y += stride)
                {
                    for (int x = 0; x < w; x += stride)
                    {
                        if (ceq.Equals(pixels[y * w + x], target))
                        {
                            if (x < minX) minX = x;
                            if (y < minY) minY = y;
                            if (x > maxX) maxX = x;
                            if (y > maxY) maxY = y;
                            found = true;
                        }
                    }
                }

                if (found)
                {
                    float margin = stride;
                    _partBounds.Add(new Rect(
                        Mathf.Max(0, minX - margin) / w,
                        Mathf.Max(0, minY - margin) / h,
                        Mathf.Min(w, maxX + margin * 2) / w - Mathf.Max(0, minX - margin) / w,
                        Mathf.Min(h, maxY + margin * 2) / h - Mathf.Max(0, minY - margin) / h
                    ));
                }
                else
                {
                    _partBounds.Add(new Rect(0, 0, 0, 0));
                }
            }

            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        private void SelectPart(int index)
        {
            if (_selectedZone != null && _selectedIndex >= 0 && _selectedIndex < _data.BodyPartMappings.Count)
            {
                var c = _data.BodyPartMappings[_selectedIndex].Color;
                c.a = 0.25f;
                _selectedZone.style.backgroundColor = c;
                _selectedZone.style.borderLeftWidth = 1;
                _selectedZone.style.borderRightWidth = 1;
                _selectedZone.style.borderTopWidth = 1;
                _selectedZone.style.borderBottomWidth = 1;
                _selectedZone.style.borderLeftColor = new Color(1f, 1f, 1f, 0.5f);
                _selectedZone.style.borderRightColor = new Color(1f, 1f, 1f, 0.5f);
                _selectedZone.style.borderTopColor = new Color(1f, 1f, 1f, 0.5f);
                _selectedZone.style.borderBottomColor = new Color(1f, 1f, 1f, 0.5f);
            }

            _selectedIndex = index;
            _detailPanel.Clear();
            _detailPanel.style.display = DisplayStyle.Flex;
            _detailPanel.style.marginTop = 6;
            _detailPanel.style.paddingLeft = 8;
            _detailPanel.style.paddingRight = 8;
            _detailPanel.style.paddingTop = 6;
            _detailPanel.style.paddingBottom = 6;
            _detailPanel.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);

            var mapping = _data.BodyPartMappings[index];

            var header = new Label($"Part {index}");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 4;
            _detailPanel.Add(header);

            var colorRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var swatch = new VisualElement();
            swatch.style.width = 20;
            swatch.style.height = 20;
            swatch.style.backgroundColor = mapping.Color;
            swatch.style.marginRight = 6;
            swatch.style.borderLeftWidth = 1;
            swatch.style.borderRightWidth = 1;
            swatch.style.borderTopWidth = 1;
            swatch.style.borderBottomWidth = 1;
            swatch.style.borderLeftColor = Color.gray;
            swatch.style.borderRightColor = Color.gray;
            swatch.style.borderTopColor = Color.gray;
            swatch.style.borderBottomColor = Color.gray;
            colorRow.Add(swatch);
            colorRow.Add(new Label($"({mapping.Color.r:F1}, {mapping.Color.g:F1}, {mapping.Color.b:F1})"));
            colorRow.style.marginBottom = 4;
            _detailPanel.Add(colorRow);

            AddField("Name", mapping.Name, newValue =>
            {
                var m = _data.BodyPartMappings[index];
                m.Name = newValue;
                _data.BodyPartMappings[index] = m;
                EditorUtility.SetDirty(_data);
            });

            var toggle = new Toggle("Essential") { value = mapping.IsEssential };
            toggle.style.marginTop = 2;
            toggle.RegisterValueChangedCallback(evt =>
            {
                var m = _data.BodyPartMappings[index];
                m.IsEssential = evt.newValue;
                _data.BodyPartMappings[index] = m;
                EditorUtility.SetDirty(_data);
            });
            _detailPanel.Add(toggle);

            var slider = new Slider(0f, 100f) { value = mapping.ArmourPercent, showInputField = true };
            slider.label = "Armour %";
            slider.style.marginTop = 2;
            slider.RegisterValueChangedCallback(evt =>
            {
                var m = _data.BodyPartMappings[index];
                m.ArmourPercent = evt.newValue;
                _data.BodyPartMappings[index] = m;
                EditorUtility.SetDirty(_data);
            });
            _detailPanel.Add(slider);

            if (index >= 0 && index < _zones.Count)
            {
                _selectedZone = _zones[index];
                _selectedZone.style.backgroundColor = new Color(1f, 1f, 1f, 0.35f);
                _selectedZone.style.borderLeftWidth = 2;
                _selectedZone.style.borderRightWidth = 2;
                _selectedZone.style.borderTopWidth = 2;
                _selectedZone.style.borderBottomWidth = 2;
                _selectedZone.style.borderLeftColor = new Color(0f, 0.8f, 1f, 1f);
                _selectedZone.style.borderRightColor = new Color(0f, 0.8f, 1f, 1f);
                _selectedZone.style.borderTopColor = new Color(0f, 0.8f, 1f, 1f);
                _selectedZone.style.borderBottomColor = new Color(0f, 0.8f, 1f, 1f);
            }
        }

        private void AddField(string label, string initialValue, System.Action<string> onChanged)
        {
            _detailPanel.Add(new Label(label));
            var field = new TextField { value = initialValue ?? "" };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            _detailPanel.Add(field);
        }

        private void OnDisable()
        {
            _drawer?.Dispose();
            _drawer = null;
        }
    }
}
