using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace SolarHarmony.DynamicWounds2D.Editor
{
    public class CharacterLayerViewer : EditorWindow
    {
        private WoundsComponent _target;
        private CharacterAtlasData _atlasData;

        private Label _targetLabel;
        private PaperDollElement _paperDoll;
        private SliderInt _layerSlider;
        private Label _layerLabel;
        private Toggle _heatmapToggle;
        private Toggle _connectivityToggle;
        private Label _statsWounds;
        private Label _statsMaxPen;
        private Label _statsBleeding;
        private Label _statsDeath;
        private Label _statsLastHit;
        private Label _statsVisibleFraction;
        private Label _statsSevered;

        private GameObject _sceneHovered;
        private double _lastHoverCheck;
        private int _lastLayer = -1;

        [MenuItem("Sabrevois/Character Layer Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<CharacterLayerViewer>("Character Layers");
            window.minSize = new Vector2(340, 520);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Selection.selectionChanged -= OnSelectionChanged;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSceneGUI(SceneView sv)
        {
            if (_target != null) return;
            if (Event.current == null || Event.current.type != EventType.MouseMove) return;
            if (EditorApplication.timeSinceStartup - _lastHoverCheck < 0.2) return;
            _lastHoverCheck = EditorApplication.timeSinceStartup;
            _sceneHovered = HandleUtility.PickGameObject(Event.current.mousePosition, false);
        }

        private void OnSelectionChanged()
        {
            if (Application.isPlaying) return;
            _target = null;
            _atlasData = null;
        }

        private bool TryAcquireTarget()
        {
            if (Application.isPlaying)
            {
                var lookedAt = WoundsComponent.LookedAtWoundsComponent;
                if (lookedAt != null) { SetTarget(lookedAt); return true; }
            }

            if (Selection.activeGameObject != null)
            {
                var sel = Selection.activeGameObject;
                var wc = sel.GetComponentInParent<WoundsComponent>();
                if (wc == null && sel.transform.parent != null)
                    wc = sel.transform.parent.GetComponentInParent<WoundsComponent>();
                if (wc == null) wc = sel.GetComponentInChildren<WoundsComponent>();
                if (wc == null) wc = sel.transform.root.GetComponentInChildren<WoundsComponent>();
                if (wc != null) { SetTarget(wc); return true; }
            }

            if (_sceneHovered != null)
            {
                var wc = _sceneHovered.GetComponentInParent<WoundsComponent>();
                if (wc == null) wc = _sceneHovered.transform.root.GetComponentInChildren<WoundsComponent>();
                if (wc != null) { SetTarget(wc); return true; }
            }

            return false;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);

            _targetLabel = new Label("No target — hover or select a character");
            _targetLabel.style.flexShrink = 0;
            _targetLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _targetLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            _targetLabel.style.fontSize = 12;
            _targetLabel.style.marginBottom = 8;
            root.Add(_targetLabel);

            _paperDoll = new PaperDollElement();
            _paperDoll.style.flexGrow = 1;
            _paperDoll.style.flexShrink = 1;
            _paperDoll.style.backgroundColor = new Color(0.08f, 0.08f, 0.08f);
            _paperDoll.style.borderTopLeftRadius = 6;
            _paperDoll.style.borderTopRightRadius = 6;
            _paperDoll.style.borderBottomLeftRadius = 6;
            _paperDoll.style.borderBottomRightRadius = 6;
            _paperDoll.style.marginBottom = 8;
            _paperDoll.style.overflow = Overflow.Hidden;
            root.Add(_paperDoll);

            var controlsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
            _layerLabel = new Label("Layer: 0 / 0") { style = { width = 72, fontSize = 12, color = Color.white } };
            _layerSlider = new SliderInt(0, 0) { style = { flexGrow = 1 } };
            _layerSlider.RegisterValueChangedCallback(_ => RefreshPaperDoll());
            controlsRow.Add(_layerLabel);
            controlsRow.Add(_layerSlider);
            root.Add(controlsRow);

            var togglesRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            _heatmapToggle = new Toggle("Heatmap") { style = { flexGrow = 1 } };
            _heatmapToggle.RegisterValueChangedCallback(_ => RefreshPaperDoll());
            _connectivityToggle = new Toggle("Connectivity Map") { style = { flexGrow = 1 } };
            _connectivityToggle.RegisterValueChangedCallback(_ => RefreshPaperDoll());
            togglesRow.Add(_heatmapToggle);
            togglesRow.Add(_connectivityToggle);
            root.Add(togglesRow);

            var divider = new VisualElement { style = { height = 1, backgroundColor = new Color(0.35f, 0.35f, 0.35f), marginBottom = 6 } };
            root.Add(divider);

            var statsHeader = new Label("Statistics") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4, color = new Color(0.8f, 0.8f, 0.8f) } };
            root.Add(statsHeader);

            _statsWounds = MakeStatLabel(); _statsMaxPen = MakeStatLabel();
            _statsBleeding = MakeStatLabel(); _statsDeath = MakeStatLabel();
            _statsLastHit = MakeStatLabel(); _statsVisibleFraction = MakeStatLabel();
            _statsSevered = MakeStatLabel();

            root.Add(_statsWounds); root.Add(_statsMaxPen); root.Add(_statsBleeding);
            root.Add(_statsDeath); root.Add(_statsLastHit); root.Add(_statsVisibleFraction);
            root.Add(_statsSevered);

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

            _paperDoll.SetTarget(null, null, 0, false, false);
        }

        private static Label MakeStatLabel() =>
            new Label { style = { fontSize = 11, color = new Color(0.65f, 0.65f, 0.65f), marginBottom = 1 } };

        private void OnEditorUpdate()
        {
            if (_layerSlider == null) return;

            if (_target == null || _atlasData == null)
                TryAcquireTarget();

            if (_target == null || _atlasData == null) return;

            if (_target.AtlasData != _atlasData)
                SetTarget(_target);

            int maxLayer = Mathf.Max(0, _atlasData.LayerCount - 1);
            if (_atlasData.LayerCount > 1) _layerSlider.highValue = maxLayer;
            _layerLabel.text = $"Layer: {_layerSlider.value} / {maxLayer}";

            RefreshPaperDoll();
            UpdateStats();
        }

        private void SetTarget(WoundsComponent wc)
        {
            _target = wc;
            _atlasData = _target.AtlasData;
            _lastLayer = -1;
            _targetLabel.text = _target.name;

            if (_atlasData != null && _atlasData.LayerCount > 0 && _layerSlider != null)
            {
                _layerSlider.lowValue = 0;
                _layerSlider.highValue = Mathf.Max(0, _atlasData.LayerCount - 1);
                _layerSlider.SetValueWithoutNotify(0);
            }
            if (_layerLabel != null)
                _layerLabel.text = _atlasData != null ? $"Layer: 0 / {Mathf.Max(0, _atlasData.LayerCount - 1)}" : "-";

            RefreshPaperDoll();
            UpdateStats();
        }

        private void RefreshPaperDoll()
        {
            if (_atlasData == null || _atlasData.LayerSprites.Count == 0) return;
            int layer = Mathf.Clamp(_layerSlider.value, 0, _atlasData.LayerSprites.Count - 1);
            _paperDoll.SetTarget(_target, _atlasData, layer, _heatmapToggle.value, _connectivityToggle.value);
            _lastLayer = layer;
        }

        private void UpdateStats()
        {
            if (_target == null)
            {
                _statsWounds.text = "Wounds: -"; _statsMaxPen.text = "Max Penetration: -";
                _statsBleeding.text = "Bleeding: -"; _statsDeath.text = "Dead: -";
                _statsLastHit.text = "Last Hit: -"; _statsVisibleFraction.text = "Visible: -";
                _statsSevered.text = "Severed Parts: -"; return;
            }

            var wounds = _target.WoundList;
            _statsWounds.text = $"Wounds: {wounds?.Count ?? 0}";
            _statsMaxPen.text = $"Max Penetration: {_target.MaxWoundPenetration:F2}";
            _statsBleeding.text = $"Bleeding: {(_target.IsBleeding ? "Yes" : "No")}";
            _statsDeath.text = $"Dead: {(_target.IsHostDead ? "Yes" : "No")}";
            _statsVisibleFraction.text = $"Visible Height: {_target.VisibleHeightFraction:P0}";

            int hitIdx = _target.LastHitBodyPartIndex;
            if (hitIdx >= 0 && _atlasData?.BodyPartMappings != null && hitIdx < _atlasData.BodyPartMappings.Count)
            {
                var bp = _atlasData.BodyPartMappings[hitIdx];
                _statsLastHit.text = $"Last Hit: {bp.Name} (Essential: {bp.IsEssential})";
            }
            else _statsLastHit.text = "Last Hit: -";

            var severed = FindFloatingComponents(_target);
            _statsSevered.text = $"Severed Parts: {severed.Count}";
        }

        private static List<List<Vector2Int>> FindFloatingComponents(WoundsComponent target)
        {
            var result = new List<List<Vector2Int>>();
            var graph = target?.LiveGraph;
            int gw = target?.GraphWidth ?? 0, gh = target?.GraphHeight ?? 0;
            if (graph == null || gw <= 0 || gh <= 0) return result;

            int total = gw * gh;
            var visited = new bool[total];
            for (int i = 0; i < total; i++)
            {
                if (!graph[i] || visited[i]) continue;
                var comp = new List<Vector2Int>();
                var stack = new Stack<int>(); stack.Push(i); visited[i] = true;
                while (stack.Count > 0)
                {
                    int idx = stack.Pop(); int x = idx % gw, y = idx / gw;
                    comp.Add(new Vector2Int(x, y));
                    void T(int nx, int ny) { if (nx < 0 || nx >= gw || ny < 0 || ny >= gh) return; int ni = ny * gw + nx; if (!graph[ni] || visited[ni]) return; visited[ni] = true; stack.Push(ni); }
                    T(x + 1, y); T(x - 1, y); T(x, y + 1); T(x, y - 1);
                    T(x + 1, y + 1); T(x + 1, y - 1); T(x - 1, y + 1); T(x - 1, y - 1);
                }
                if (comp.Count == 0) continue;
                int minY = int.MaxValue; foreach (var c in comp) if (c.y < minY) minY = c.y;
                if (minY > 0) result.Add(comp);
            }
            return result;
        }

        // ─── PaperDollElement ────────────────────────────────────────

        private class PaperDollElement : VisualElement
        {
            private WoundsComponent _target;
            private CharacterAtlasData _atlasData;
            private int _layer;
            private bool _showHeatmap, _showConnectivity;
            private Sprite _cachedSprite;
            private Texture2D _spriteTex;

            public PaperDollElement()
            {
                generateVisualContent += OnPaint;
            }

            public void SetTarget(WoundsComponent target, CharacterAtlasData atlasData,
                int layer, bool showHeatmap, bool showConnectivity)
            {
                _target = target;
                _atlasData = atlasData;
                _layer = layer;
                _showHeatmap = showHeatmap;
                _showConnectivity = showConnectivity;

                var sprite = (atlasData != null && atlasData.LayerSprites.Count > layer && layer >= 0)
                    ? atlasData.LayerSprites[layer] : null;

                if (sprite != _cachedSprite)
                {
                    _cachedSprite = sprite;
                    if (_spriteTex != null) { Object.DestroyImmediate(_spriteTex); _spriteTex = null; }
                    if (sprite != null && sprite.texture != null)
                    {
                        var src = sprite.texture;
                        var r = sprite.rect;
                        int w = Mathf.FloorToInt(r.width), h = Mathf.FloorToInt(r.height);
                        if (w > 0 && h > 0 && src.isReadable)
                        {
                            _spriteTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                            _spriteTex.SetPixels(src.GetPixels((int)r.x, (int)r.y, w, h));
                            _spriteTex.Apply();
                        }
                    }
                }

                MarkDirtyRepaint();
            }

            private void OnPaint(MeshGenerationContext ctx)
            {
                float w = contentRect.width, h = contentRect.height;
                if (w <= 0 || h <= 0) return;

                if (_spriteTex == null)
                {
                    var p = ctx.painter2D;
                    p.fillColor = new Color(0.12f, 0.12f, 0.12f, 1f);
                    p.BeginPath(); p.MoveTo(new Vector2(0, 0)); p.LineTo(new Vector2(w, 0));
                    p.LineTo(new Vector2(w, h)); p.LineTo(new Vector2(0, h)); p.ClosePath(); p.Fill();
                    return;
                }

                float texAspect = (float)_spriteTex.width / _spriteTex.height;
                float frameAspect = w / h;
                float dw, dh, ox, oy;
                if (texAspect > frameAspect) { dw = w; dh = w / texAspect; ox = 0; oy = (h - dh) * 0.5f; }
                else { dh = h; dw = h * texAspect; ox = (w - dw) * 0.5f; oy = 0; }

                var painter = ctx.painter2D;
                DrawSeveredParts(painter, ox, oy, dw, dh);
                DrawSpriteMesh(ctx, _spriteTex, ox, oy, dw, dh);
                if (_showHeatmap) DrawHeatmap(painter, ox, oy, dw, dh);
                if (_showConnectivity) DrawConnectivityGrid(painter, ox, oy, dw, dh);
                DrawWounds(painter, ox, oy, dw, dh);
            }

            private static void DrawSpriteMesh(MeshGenerationContext ctx, Texture2D tex,
                float ox, float oy, float dw, float dh)
            {
                var mesh = ctx.Allocate(4, 6, tex);
                float z = Vertex.nearZ;
                mesh.SetNextVertex(new Vertex { position = new Vector3(ox, oy, z), tint = Color.white, uv = new Vector2(0, 1) });
                mesh.SetNextVertex(new Vertex { position = new Vector3(ox + dw, oy, z), tint = Color.white, uv = new Vector2(1, 1) });
                mesh.SetNextVertex(new Vertex { position = new Vector3(ox, oy + dh, z), tint = Color.white, uv = new Vector2(0, 0) });
                mesh.SetNextVertex(new Vertex { position = new Vector3(ox + dw, oy + dh, z), tint = Color.white, uv = new Vector2(1, 0) });
                mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
                mesh.SetNextIndex(1); mesh.SetNextIndex(3); mesh.SetNextIndex(2);
            }

            // ─── Wounds ─────────────────────────────────────────────

            private void DrawWounds(Painter2D p, float ox, float oy, float dw, float dh)
            {
                var wounds = _target?.WoundList;
                if (wounds == null) return;

                for (int i = 0; i < wounds.Count; i++)
                {
                    var wd = wounds[i];
                    float excess = wd.Penetration - _layer;
                    if (excess <= 0f) continue;

                    float pen = Mathf.Clamp01(excess / 1.5f);
                    float cx = ox + wd.Position.x * dw;
                    float cy = oy + (1f - wd.Position.y) * dh;
                    float r = Mathf.Max(wd.Radius * dw * 0.5f, 3f);

                    p.fillColor = new Color(0.8f, 0.05f, 0.02f, 0.25f + pen * 0.5f);
                    p.BeginPath(); p.Arc(new Vector2(cx, cy), r, 0f, Mathf.PI * 2f, ArcDirection.Clockwise); p.Fill();

                    if (pen > 0.3f)
                    {
                        p.strokeColor = new Color(1f, 0.1f, 0.02f, 0.4f + pen * 0.4f); p.lineWidth = 2f;
                        p.BeginPath(); p.Arc(new Vector2(cx, cy), r, 0f, Mathf.PI * 2f, ArcDirection.Clockwise); p.Stroke();
                    }
                }
            }

            // ─── Heatmap ────────────────────────────────────────────

            private void DrawHeatmap(Painter2D p, float ox, float oy, float dw, float dh)
            {
                var wounds = _target?.WoundList;
                if (wounds == null || wounds.Count == 0) return;

                int res = 48; float cw = dw / res, ch = dh / res;
                for (int gy = 0; gy < res; gy++)
                    for (int gx = 0; gx < res; gx++)
                    {
                        float u = (gx + 0.5f) / res, v = (gy + 0.5f) / res, pen = 0f;
                        for (int i = 0; i < wounds.Count; i++)
                        {
                            var w = wounds[i]; float excess = w.Penetration - _layer;
                            if (excess <= 0f) continue;
                            float dist = Vector2.Distance(new Vector2(u, v), w.Position);
                            if (dist < w.Radius * 0.35f) pen += excess * (1f - dist / (w.Radius * 0.35f));
                        }
                        if (pen < 0.01f) continue;
                        float t = Mathf.Clamp01(pen / 1.5f);
                        p.fillColor = Color.Lerp(new Color(0f, 0.6f, 0.15f, 0.35f), new Color(1f, 0.05f, 0f, 0.6f), t);
                        float px = ox + gx * cw, py = oy + gy * ch;
                        p.BeginPath(); p.MoveTo(new Vector2(px, py)); p.LineTo(new Vector2(px + cw, py));
                        p.LineTo(new Vector2(px + cw, py + ch)); p.LineTo(new Vector2(px, py + ch));
                        p.ClosePath(); p.Fill();
                    }
            }

            // ─── Severed parts — offset copies below the body ──────

            private void DrawSeveredParts(Painter2D p, float ox, float oy, float dw, float dh)
            {
                if (_target == null) return;
                var components = FindFloatingComponents(_target);
                if (components.Count == 0) return;

                float gap = dh * 0.06f; // visual gap between main body and severed pieces

                for (int ci = 0; ci < components.Count; ci++)
                {
                    var comp = components[ci];
                    if (comp.Count == 0) continue;

                    int minX = int.MaxValue, maxX = 0, minY = int.MaxValue, maxY = 0;
                    foreach (var c in comp) { if (c.x < minX) minX = c.x; if (c.x > maxX) maxX = c.x; if (c.y < minY) minY = c.y; if (c.y > maxY) maxY = c.y; }

                    float centerU = (minX + maxX) * 0.5f / _target.GraphWidth;
                    float centerV = (minY + maxY) * 0.5f / _target.GraphHeight;
                    float offsetX = ((ci % 2 == 0) ? 1f : -1f) * dw * 0.08f * (1 + ci / 2);
                    float offsetY = -dh * 0.1f - gap * (1 + ci);

                    // draw each cell of the severed component at the offset position
                    foreach (var cell in comp)
                    {
                        float u = (cell.x + 0.5f) / _target.GraphWidth;
                        float v = (cell.y + 0.5f) / _target.GraphHeight;
                        float cx = ox + u * dw + offsetX;
                        float cy = oy + (1f - v) * dh + offsetY;
                        float cs = dw / _target.GraphWidth * 1.05f;

                        p.fillColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
                        p.BeginPath(); p.MoveTo(new Vector2(cx - cs * 0.5f, cy - cs * 0.5f));
                        p.LineTo(new Vector2(cx + cs * 0.5f, cy - cs * 0.5f));
                        p.LineTo(new Vector2(cx + cs * 0.5f, cy + cs * 0.5f));
                        p.LineTo(new Vector2(cx - cs * 0.5f, cy + cs * 0.5f));
                        p.ClosePath(); p.Fill();
                    }
                }
            }

            // ─── Connectivity grid ──────────────────────────────────

            private void DrawConnectivityGrid(Painter2D p, float ox, float oy, float dw, float dh)
            {
                var graph = _target?.LiveGraph;
                int gw = _target?.GraphWidth ?? 0, gh = _target?.GraphHeight ?? 0;
                if (graph == null || gw <= 0 || gh <= 0) return;

                int res = Mathf.Min(gw, 64); float cs = dw / res;
                for (int gy = 0; gy < res; gy++)
                    for (int gx = 0; gx < res; gx++)
                    {
                        int sx = gx * gw / res, sy = (res - 1 - gy) * gh / res;
                        if (sx < 0 || sx >= gw || sy < 0 || sy >= gh) continue;
                        bool solid = graph[sy * gw + sx];
                        p.fillColor = solid ? new Color(0f, 0.7f, 0f, 0.35f) : new Color(0.8f, 0f, 0f, 0.5f);
                        float px = ox + gx * cs, py = oy + gy * cs;
                        p.BeginPath(); p.MoveTo(new Vector2(px, py)); p.LineTo(new Vector2(px + cs, py));
                        p.LineTo(new Vector2(px + cs, py + cs)); p.LineTo(new Vector2(px, py + cs));
                        p.ClosePath(); p.Fill();
                    }
            }
        }
    }
}
