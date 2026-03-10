// File: AGISGraphCanvas.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/
// Purpose: Infinite pan/zoom canvas for the AGIS visual graph editor.
//          Contains node cards, edge layer, grid background, rubber-band selection,
//          minimap, and breadcrumb bar.
//          No UnityEditor dependencies — UIToolkit Runtime only.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.Runtime;
using AGIS.ESM.RuntimeEditor;

namespace AGIS.ESM.RuntimeEditor
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Grid background element
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class AGISGridElement : VisualElement
    {
        private static readonly Color MinorColor = new Color(0.145f, 0.145f, 0.145f, 1f);
        private static readonly Color MajorColor = new Color(0.180f, 0.180f, 0.180f, 1f);

        private const float MinorStep = 20f;
        private const float MajorStep = 100f;
        private const float Extent = 5000f;

        public AGISGridElement()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = -Extent;
            style.top = -Extent;
            style.width = Extent * 2f;
            style.height = Extent * 2f;

            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var w = resolvedStyle.width;
            var h = resolvedStyle.height;

            // Minor grid lines
            painter.strokeColor = MinorColor;
            painter.lineWidth = 1f;

            for (float x = 0; x <= w; x += MinorStep)
            {
                if (Mathf.Approximately(x % MajorStep, 0f)) continue;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0));
                painter.LineTo(new Vector2(x, h));
                painter.Stroke();
            }

            for (float y = 0; y <= h; y += MinorStep)
            {
                if (Mathf.Approximately(y % MajorStep, 0f)) continue;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, y));
                painter.LineTo(new Vector2(w, y));
                painter.Stroke();
            }

            // Major grid lines
            painter.strokeColor = MajorColor;
            painter.lineWidth = 1.5f;

            for (float x = 0; x <= w; x += MajorStep)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0));
                painter.LineTo(new Vector2(x, h));
                painter.Stroke();
            }

            for (float y = 0; y <= h; y += MajorStep)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, y));
                painter.LineTo(new Vector2(w, y));
                painter.Stroke();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Edge layer element
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class AGISEdgeLayerElement : VisualElement
    {
        private const float Extent = 5000f;

        private AGISStateMachineGraph _graph;
        private AGISConditionTypeRegistry _condTypes;
        private Func<AGISGuid, AGISNodeCardElement> _cardLookup;
        private AGISGuid _selectedEdgeId;

        // Pill hit areas (rebuilt each generateVisualContent call)
        private readonly List<(Rect rect, AGISGuid edgeId)> _pillRects = new List<(Rect, AGISGuid)>();

        public event Action<AGISGuid> OnEdgeClicked;

        public AGISEdgeLayerElement()
        {
            pickingMode = PickingMode.Position;
            style.position = Position.Absolute;
            style.left = -Extent;
            style.top = -Extent;
            style.width = Extent * 2f;
            style.height = Extent * 2f;

            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        public void SetGraph(AGISStateMachineGraph graph, AGISConditionTypeRegistry condTypes,
            Func<AGISGuid, AGISNodeCardElement> cardLookup)
        {
            _graph = graph;
            _condTypes = condTypes;
            _cardLookup = cardLookup;
            MarkDirtyRepaint();
        }

        public void SetSelectedEdge(AGISGuid edgeId)
        {
            _selectedEdgeId = edgeId;
            MarkDirtyRepaint();
        }

        public void RefreshEdges() => MarkDirtyRepaint();

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            _pillRects.Clear();

            if (_graph?.edges == null || _cardLookup == null) return;

            var painter = ctx.painter2D;
            float offset = Extent; // shift all world coords by Extent since element is at (-Extent, -Extent)

            foreach (var edge in _graph.edges)
            {
                if (edge == null) continue;

                bool isSelected = edge.edgeId == _selectedEdgeId;
                bool isDangling = !edge.toNodeId.IsValid;
                bool isAnyState = false;

                var fromCard = _cardLookup(edge.fromNodeId);
                if (fromCard == null) continue;

                // Determine if from-node is AnyState kind
                if (fromCard.NodeType.Kind == AGISNodeKind.AnyState)
                    isAnyState = true;

                // Source: out port of fromCard
                var fromPos = fromCard.GetOutPortPosition() + new Vector2(offset, offset);

                Vector2 toPos;
                if (isDangling)
                {
                    // Dangling: draw to a point 80px to the right of the source
                    toPos = fromPos + new Vector2(80f, 0f);
                }
                else
                {
                    var toCard = _cardLookup(edge.toNodeId);
                    if (toCard == null) continue;
                    toPos = toCard.GetInPortPosition() + new Vector2(offset, offset);
                }

                // Bezier control points
                float dx = Mathf.Abs(toPos.x - fromPos.x);
                float bend = Mathf.Clamp(dx * 0.5f, 40f, 200f);
                var ctrl1 = fromPos + new Vector2(bend, 0f);
                var ctrl2 = toPos + new Vector2(-bend, 0f);

                // Color
                Color edgeColor;
                if (isSelected)
                    edgeColor = Color.white;
                else if (isAnyState)
                    edgeColor = new Color(0.55f, 0.1f, 0.1f);
                else
                    edgeColor = new Color(0.5f, 0.65f, 0.85f, 0.85f);

                float lineWidth = isSelected ? 2.5f : 1.5f;

                painter.strokeColor = edgeColor;
                painter.lineWidth = lineWidth;
                painter.BeginPath();
                painter.MoveTo(fromPos);
                painter.BezierCurveTo(ctrl1, ctrl2, toPos);
                painter.Stroke();

                // Arrowhead at toPos (or at midpoint for dangling)
                Vector2 arrowTarget = isDangling ? toPos : toPos;
                Vector2 arrowDir = isDangling
                    ? new Vector2(1f, 0f)
                    : (toPos - ctrl2).normalized;

                if (arrowDir == Vector2.zero) arrowDir = Vector2.right;

                DrawArrowhead(painter, arrowTarget, arrowDir, edgeColor, lineWidth, isDangling);

                // Pill label at bezier midpoint
                Vector2 mid = BezierMidpoint(fromPos, ctrl1, ctrl2, toPos);
                string summary = AGISConditionSummary.Summarize(edge.condition, _condTypes);
                string pillText = $"P{edge.priority}  {summary}";

                // Approximate pill size
                float pillW = Mathf.Clamp(pillText.Length * 6.5f + 12f, 40f, 220f);
                float pillH = 16f;
                var pillRect = new Rect(mid.x - pillW * 0.5f, mid.y - pillH * 0.5f, pillW, pillH);

                // Draw pill background
                painter.fillColor = isSelected
                    ? new Color(0.3f, 0.3f, 0.15f, 0.92f)
                    : new Color(0.12f, 0.12f, 0.12f, 0.88f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(pillRect.x + 6f, pillRect.y));
                painter.LineTo(new Vector2(pillRect.xMax - 6f, pillRect.y));
                painter.ArcTo(new Vector2(pillRect.xMax, pillRect.y), new Vector2(pillRect.xMax, pillRect.y + 6f), 6f);
                painter.LineTo(new Vector2(pillRect.xMax, pillRect.yMax - 6f));
                painter.ArcTo(new Vector2(pillRect.xMax, pillRect.yMax), new Vector2(pillRect.xMax - 6f, pillRect.yMax), 6f);
                painter.LineTo(new Vector2(pillRect.x + 6f, pillRect.yMax));
                painter.ArcTo(new Vector2(pillRect.x, pillRect.yMax), new Vector2(pillRect.x, pillRect.yMax - 6f), 6f);
                painter.LineTo(new Vector2(pillRect.x, pillRect.y + 6f));
                painter.ArcTo(new Vector2(pillRect.x, pillRect.y), new Vector2(pillRect.x + 6f, pillRect.y), 6f);
                painter.Fill();

                // Track pill rect for click detection (in element-local coords, NOT offset)
                _pillRects.Add((new Rect(pillRect.x - offset, pillRect.y - offset, pillRect.width, pillRect.height), edge.edgeId));

                // Open circle for dangling edge endpoint
                if (isDangling)
                {
                    painter.strokeColor = edgeColor;
                    painter.lineWidth = 1.5f;
                    painter.fillColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                    painter.BeginPath();
                    painter.Arc(toPos, 5f, 0f, 360f);
                    painter.Fill();
                    painter.BeginPath();
                    painter.Arc(toPos, 5f, 0f, 360f);
                    painter.Stroke();
                }
            }
        }

        private static void DrawArrowhead(Painter2D painter, Vector2 tip, Vector2 dir, Color color,
            float lineWidth, bool isDangling)
        {
            if (isDangling) return; // dangling edges show open circle instead

            var perp = new Vector2(-dir.y, dir.x);
            float size = 8f;

            var p1 = tip;
            var p2 = tip - dir * size + perp * size * 0.5f;
            var p3 = tip - dir * size - perp * size * 0.5f;

            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(p1);
            painter.LineTo(p2);
            painter.LineTo(p3);
            painter.Fill();
        }

        private static Vector2 BezierMidpoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float t = 0.5f;
            float mt = 1f - t;
            return mt * mt * mt * p0
                 + 3f * mt * mt * t * p1
                 + 3f * mt * t * t * p2
                 + t * t * t * p3;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;

            var localPos = evt.localPosition;

            foreach (var (rect, edgeId) in _pillRects)
            {
                if (rect.Contains(new Vector2(localPos.x, localPos.y)))
                {
                    OnEdgeClicked?.Invoke(edgeId);
                    evt.StopPropagation();
                    return;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Minimap element
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class AGISMinimapElement : VisualElement
    {
        private AGISStateMachineGraph _graph;
        private Func<AGISGuid, AGISNodeCardElement> _cardLookup;
        private Action<Vector2> _onPanRequest; // requests canvas pan in world coords

        private float _canvasZoom = 1f;
        private Vector2 _canvasPan;
        private Vector2 _canvasSize;

        private bool _isDragging;

        public AGISMinimapElement()
        {
            AddToClassList("agis-minimap");
            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        public void Bind(AGISStateMachineGraph graph, Func<AGISGuid, AGISNodeCardElement> cardLookup,
            Action<Vector2> onPanRequest)
        {
            _graph = graph;
            _cardLookup = cardLookup;
            _onPanRequest = onPanRequest;
        }

        public void UpdateViewport(float zoom, Vector2 pan, Vector2 canvasSize)
        {
            _canvasZoom = zoom;
            _canvasPan = pan;
            _canvasSize = canvasSize;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var w = resolvedStyle.width;
            var h = resolvedStyle.height;
            if (w <= 0 || h <= 0) return;

            // Find world bounding box of all nodes
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            bool hasNodes = false;
            if (_graph?.nodes != null && _cardLookup != null)
            {
                foreach (var node in _graph.nodes)
                {
                    if (node?.visual == null) continue;
                    var card = _cardLookup(node.nodeId);
                    float cw = card != null ? card.layout.width : 180f;
                    float ch = card != null ? card.layout.height : 80f;

                    minX = Mathf.Min(minX, node.visual.position.x);
                    minY = Mathf.Min(minY, node.visual.position.y);
                    maxX = Mathf.Max(maxX, node.visual.position.x + cw);
                    maxY = Mathf.Max(maxY, node.visual.position.y + ch);
                    hasNodes = true;
                }
            }

            if (!hasNodes)
            {
                minX = -200f; minY = -200f; maxX = 200f; maxY = 200f;
            }

            float padding = 50f;
            minX -= padding; minY -= padding; maxX += padding; maxY += padding;
            float worldW = maxX - minX;
            float worldH = maxY - minY;
            if (worldW <= 0) worldW = 1;
            if (worldH <= 0) worldH = 1;

            float scaleX = w / worldW;
            float scaleY = h / worldH;
            float scale = Mathf.Min(scaleX, scaleY);

            // Draw background
            painter.fillColor = new Color(0.07f, 0.07f, 0.07f, 0.9f);
            painter.BeginPath();
            painter.MoveTo(Vector2.zero);
            painter.LineTo(new Vector2(w, 0));
            painter.LineTo(new Vector2(w, h));
            painter.LineTo(new Vector2(0, h));
            painter.Fill();

            // Draw node rects
            if (_graph?.nodes != null && _cardLookup != null)
            {
                foreach (var node in _graph.nodes)
                {
                    if (node?.visual == null) continue;
                    var card = _cardLookup(node.nodeId);
                    float cw = card != null ? card.layout.width : 180f;
                    float ch = card != null ? card.layout.height : 80f;

                    float rx = (node.visual.position.x - minX) * scale;
                    float ry = (node.visual.position.y - minY) * scale;
                    float rw = Mathf.Max(cw * scale, 4f);
                    float rh = Mathf.Max(ch * scale, 4f);

                    painter.fillColor = new Color(0.3f, 0.5f, 0.7f, 0.7f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(rx, ry));
                    painter.LineTo(new Vector2(rx + rw, ry));
                    painter.LineTo(new Vector2(rx + rw, ry + rh));
                    painter.LineTo(new Vector2(rx, ry + rh));
                    painter.Fill();
                }
            }

            // Draw viewport indicator
            if (_canvasSize.x > 0 && _canvasSize.y > 0 && _canvasZoom > 0)
            {
                // The viewport in world space
                Vector2 vpMin = (-_canvasPan) / _canvasZoom;
                Vector2 vpMax = vpMin + _canvasSize / _canvasZoom;

                float vx = (vpMin.x - minX) * scale;
                float vy = (vpMin.y - minY) * scale;
                float vw = (vpMax.x - vpMin.x) * scale;
                float vh = (vpMax.y - vpMin.y) * scale;

                painter.strokeColor = new Color(0.9f, 0.8f, 0.3f, 0.9f);
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(vx, vy));
                painter.LineTo(new Vector2(vx + vw, vy));
                painter.LineTo(new Vector2(vx + vw, vy + vh));
                painter.LineTo(new Vector2(vx, vy + vh));
                painter.LineTo(new Vector2(vx, vy));
                painter.Stroke();
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0)
            {
                _isDragging = true;
                this.CapturePointer(evt.pointerId);
                PanToMinimapPos(evt.localPosition);
                evt.StopPropagation();
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_isDragging)
            {
                PanToMinimapPos(evt.localPosition);
                evt.StopPropagation();
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_isDragging)
            {
                _isDragging = false;
                this.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        private void PanToMinimapPos(Vector2 minimapPos)
        {
            if (_onPanRequest == null) return;

            var w = resolvedStyle.width;
            var h = resolvedStyle.height;
            if (w <= 0 || h <= 0) return;

            // Compute world bounding box (same logic as draw)
            float minX = -200f, minY = -200f, maxX = 200f, maxY = 200f;
            bool hasNodes = false;
            if (_graph?.nodes != null && _cardLookup != null)
            {
                float tMinX = float.MaxValue, tMinY = float.MaxValue,
                      tMaxX = float.MinValue, tMaxY = float.MinValue;
                foreach (var node in _graph.nodes)
                {
                    if (node?.visual == null) continue;
                    tMinX = Mathf.Min(tMinX, node.visual.position.x);
                    tMinY = Mathf.Min(tMinY, node.visual.position.y);
                    tMaxX = Mathf.Max(tMaxX, node.visual.position.x + 180f);
                    tMaxY = Mathf.Max(tMaxY, node.visual.position.y + 80f);
                    hasNodes = true;
                }
                if (hasNodes) { minX = tMinX - 50; minY = tMinY - 50; maxX = tMaxX + 50; maxY = tMaxY + 50; }
            }

            float worldW = maxX - minX;
            float worldH = maxY - minY;
            float scaleX = w / worldW;
            float scaleY = h / worldH;
            float scale = Mathf.Min(scaleX, scaleY);
            if (scale <= 0) return;

            float worldX = minimapPos.x / scale + minX;
            float worldY = minimapPos.y / scale + minY;

            _onPanRequest(new Vector2(worldX, worldY));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Breadcrumb bar
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class AGISBreadcrumbBar : VisualElement
    {
        private readonly List<(string label, Action onClick)> _crumbs = new List<(string, Action)>();

        public AGISBreadcrumbBar()
        {
            AddToClassList("agis-breadcrumb");
            style.display = DisplayStyle.None; // hidden when not in sub-graph
        }

        public void SetPath(IEnumerable<(string label, Action onClick)> crumbs, string currentLabel)
        {
            Clear();
            _crumbs.Clear();

            bool any = false;
            foreach (var (label, onClick) in crumbs)
            {
                if (any)
                {
                    var sep = new Label(" > ");
                    sep.AddToClassList("agis-breadcrumb__sep");
                    Add(sep);
                }

                var btn = new Button(() => onClick?.Invoke());
                btn.text = label;
                btn.AddToClassList("agis-breadcrumb__btn");
                Add(btn);
                _crumbs.Add((label, onClick));
                any = true;
            }

            if (any)
            {
                var sep = new Label(" > ");
                sep.AddToClassList("agis-breadcrumb__sep");
                Add(sep);
            }

            var current = new Label(currentLabel);
            current.AddToClassList("agis-breadcrumb__current");
            Add(current);

            style.display = _crumbs.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetRoot()
        {
            Clear();
            _crumbs.Clear();
            style.display = DisplayStyle.None;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Main Canvas
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class AGISGraphCanvas : VisualElement
    {
        // ── Pan / Zoom state ──────────────────────────────────────────────────
        private float _zoom = 1f;
        private Vector2 _pan = Vector2.zero;

        private const float ZoomMin = 0.15f;
        private const float ZoomMax = 3.0f;

        // ── World container and layers ─────────────────────────────────────────
        private readonly VisualElement _worldContainer;
        private readonly AGISGridElement _gridElement;
        private readonly AGISEdgeLayerElement _edgeLayer;
        private readonly VisualElement _nodeLayer;
        private readonly VisualElement _overlayLayer;

        // ── Rubber-band ───────────────────────────────────────────────────────
        private readonly VisualElement _rubberBand;
        private bool _isRubberBanding;
        private Vector2 _rubberBandStart;

        // ── Node cards ────────────────────────────────────────────────────────
        private readonly Dictionary<AGISGuid, AGISNodeCardElement> _nodeCards
            = new Dictionary<AGISGuid, AGISNodeCardElement>();

        // ── Selection ─────────────────────────────────────────────────────────
        private readonly HashSet<AGISGuid> _selectedNodeIds = new HashSet<AGISGuid>();
        private AGISGuid _selectedEdgeId = AGISGuid.Empty;

        public event Action<IReadOnlyCollection<AGISGuid>, AGISGuid> OnSelectionChanged;

        // ── Node drag ─────────────────────────────────────────────────────────
        private bool _isDraggingNodes;
        private Vector2 _dragStartScreenPos;
        private Dictionary<AGISGuid, Vector2> _dragStartPositions;
        private int _dragPointerId = -1;

        // ── Port drag (edge creation) ─────────────────────────────────────────
        private bool _isDraggingPort;
        private AGISNodeCardElement _portDragSourceCard;
        private Vector2 _portDragCurrentPos;

        // ── Middle mouse / Alt pan ────────────────────────────────────────────
        private bool _isPanning;
        private Vector2 _panStartPos;
        private Vector2 _panStartPan;
        private int _panPointerId = -1;

        // ── References ────────────────────────────────────────────────────────
        private AGISStateMachineGraph _graph;
        private AGISNodeTypeRegistry _nodeTypes;
        private AGISConditionTypeRegistry _condTypes;
        private AGISEditorHistory _history;

        // ── Pending edge creation ─────────────────────────────────────────────
        public event Action<AGISGuid, AGISGuid> OnEdgeCreateRequested;   // (fromNodeId, toNodeId)
        public event Action<AGISGuid, Vector2> OnPortDroppedOnEmpty;     // (fromNodeId, canvasPos) → open node picker
        public event Action<AGISGuid> OnNodeDeleteRequested;
        public event Action<AGISGuid, bool> OnSetEntryRequested;         // (nodeId, true)
        public event Action<AGISGuid> OnOpenSubGraphRequested;
        public event Action OnAddNodeAtCenterRequested;

        // ── Copy/paste buffer ─────────────────────────────────────────────────
        private readonly List<AGISNodeInstanceDef> _clipboard = new List<AGISNodeInstanceDef>();

        // ── Minimap ───────────────────────────────────────────────────────────
        public readonly AGISMinimapElement Minimap;
        public readonly AGISBreadcrumbBar Breadcrumb;

        // ─────────────────────────────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────────────────────────────

        public AGISGraphCanvas()
        {
            AddToClassList("agis-canvas");
            focusable = true;
            pickingMode = PickingMode.Position;

            // World container: transforms applied here for pan/zoom
            _worldContainer = new VisualElement();
            _worldContainer.style.position = Position.Absolute;
            _worldContainer.style.left = 0;
            _worldContainer.style.top = 0;
            _worldContainer.style.width = 1;
            _worldContainer.style.height = 1;
            _worldContainer.pickingMode = PickingMode.Ignore;
            Add(_worldContainer);

            _gridElement = new AGISGridElement();
            _worldContainer.Add(_gridElement);

            _edgeLayer = new AGISEdgeLayerElement();
            _edgeLayer.OnEdgeClicked += OnEdgeLayerClicked;
            _worldContainer.Add(_edgeLayer);

            _nodeLayer = new VisualElement();
            _nodeLayer.style.position = Position.Absolute;
            _nodeLayer.style.left = 0;
            _nodeLayer.style.top = 0;
            _nodeLayer.style.width = 1;
            _nodeLayer.style.height = 1;
            _nodeLayer.pickingMode = PickingMode.Ignore;
            _worldContainer.Add(_nodeLayer);

            _overlayLayer = new VisualElement();
            _overlayLayer.style.position = Position.Absolute;
            _overlayLayer.style.left = -5000;
            _overlayLayer.style.top = -5000;
            _overlayLayer.style.width = 10000;
            _overlayLayer.style.height = 10000;
            _overlayLayer.pickingMode = PickingMode.Ignore;
            _worldContainer.Add(_overlayLayer);

            // Rubber-band selection rect (in canvas/screen space, not world space)
            _rubberBand = new VisualElement();
            _rubberBand.style.position = Position.Absolute;
            _rubberBand.style.borderTopWidth = 1;
            _rubberBand.style.borderBottomWidth = 1;
            _rubberBand.style.borderLeftWidth = 1;
            _rubberBand.style.borderRightWidth = 1;
            _rubberBand.style.borderTopColor = new StyleColor(new Color(0.6f, 0.7f, 1f, 0.9f));
            _rubberBand.style.borderBottomColor = new StyleColor(new Color(0.6f, 0.7f, 1f, 0.9f));
            _rubberBand.style.borderLeftColor = new StyleColor(new Color(0.6f, 0.7f, 1f, 0.9f));
            _rubberBand.style.borderRightColor = new StyleColor(new Color(0.6f, 0.7f, 1f, 0.9f));
            _rubberBand.style.backgroundColor = new StyleColor(new Color(0.4f, 0.5f, 0.9f, 0.1f));
            _rubberBand.style.display = DisplayStyle.None;
            Add(_rubberBand);

            // Minimap and breadcrumb overlays
            Minimap = new AGISMinimapElement();
            Add(Minimap);

            Breadcrumb = new AGISBreadcrumbBar();
            Add(Breadcrumb);

            // Canvas-level event handlers
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<ContextClickEvent>(OnContextClick);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        public void RebuildAll(
            AGISStateMachineGraph graph,
            AGISNodeTypeRegistry nodeTypes,
            AGISConditionTypeRegistry condTypes,
            AGISEditorHistory history)
        {
            _graph = graph;
            _nodeTypes = nodeTypes;
            _condTypes = condTypes;
            _history = history;

            _nodeCards.Clear();
            _nodeLayer.Clear();
            _selectedNodeIds.Clear();
            _selectedEdgeId = AGISGuid.Empty;

            if (_graph?.nodes != null)
            {
                foreach (var nodeDef in _graph.nodes)
                {
                    if (nodeDef == null) continue;
                    IAGISNodeType nodeType = null;
                    if (!string.IsNullOrEmpty(nodeDef.nodeTypeId))
                        nodeTypes.TryGet(nodeDef.nodeTypeId, out nodeType);
                    if (nodeType == null)
                        nodeType = new UnknownNodeTypePlaceholder(nodeDef.nodeTypeId);

                    AddNodeCard(nodeDef, nodeType);
                }
            }

            _edgeLayer.SetGraph(_graph, _condTypes, id =>
            {
                _nodeCards.TryGetValue(id, out var card);
                return card;
            });

            Minimap.Bind(_graph, id =>
            {
                _nodeCards.TryGetValue(id, out var card);
                return card;
            }, worldPos =>
            {
                // Pan canvas so worldPos is at center
                _pan = new Vector2(resolvedStyle.width * 0.5f, resolvedStyle.height * 0.5f)
                       - worldPos * _zoom;
                ApplyTransform();
            });

            ApplyTransform();
            UpdateAllEntryIndicators();
        }

        public void AddNodeCard(AGISNodeInstanceDef def, IAGISNodeType type)
        {
            if (def == null || type == null) return;

            var card = new AGISNodeCardElement(def, type, _graph, _history, _condTypes);
            card.OnDeleteRequested   += () => OnNodeDeleteRequested?.Invoke(def.nodeId);
            card.OnParamChanged      += _ => _edgeLayer.RefreshEdges();
            card.OnPortDragStarted   += StartPortDrag;
            card.OnHeaderPointerDown += StartNodeDrag;

            card.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowNodeContextMenu(card, evt);
                evt.StopPropagation();
            });

            _nodeLayer.Add(card);
            _nodeCards[def.nodeId] = card;

            UpdateEntryIndicator(def.nodeId);
        }

        public void RemoveNodeCard(AGISGuid nodeId)
        {
            if (_nodeCards.TryGetValue(nodeId, out var card))
            {
                _nodeLayer.Remove(card);
                _nodeCards.Remove(nodeId);
            }
            _selectedNodeIds.Remove(nodeId);
            _edgeLayer.RefreshEdges();
        }

        public void UpdateEdges(AGISStateMachineGraph graph)
        {
            _graph = graph;
            _edgeLayer.SetGraph(_graph, _condTypes, id =>
            {
                _nodeCards.TryGetValue(id, out var c);
                return c;
            });
        }

        public void RefreshEdgeLayer() => _edgeLayer.RefreshEdges();

        // ── Selection ─────────────────────────────────────────────────────────

        public void SelectNode(AGISGuid id, bool addToSelection = false)
        {
            if (!addToSelection)
            {
                foreach (var selectedId in _selectedNodeIds)
                    if (_nodeCards.TryGetValue(selectedId, out var c)) c.SetSelected(false);
                _selectedNodeIds.Clear();
                _selectedEdgeId = AGISGuid.Empty;
                _edgeLayer.SetSelectedEdge(AGISGuid.Empty);
            }

            if (!id.IsValid) { FireSelectionChanged(); return; }

            _selectedNodeIds.Add(id);
            if (_nodeCards.TryGetValue(id, out var card)) card.SetSelected(true);
            FireSelectionChanged();
        }

        public void SelectEdge(AGISGuid edgeId)
        {
            foreach (var selectedId in _selectedNodeIds)
                if (_nodeCards.TryGetValue(selectedId, out var c)) c.SetSelected(false);
            _selectedNodeIds.Clear();
            _selectedEdgeId = edgeId;
            _edgeLayer.SetSelectedEdge(edgeId);
            FireSelectionChanged();
        }

        public void DeselectAll()
        {
            foreach (var selectedId in _selectedNodeIds)
                if (_nodeCards.TryGetValue(selectedId, out var c)) c.SetSelected(false);
            _selectedNodeIds.Clear();
            _selectedEdgeId = AGISGuid.Empty;
            _edgeLayer.SetSelectedEdge(AGISGuid.Empty);
            FireSelectionChanged();
        }

        public void SelectAll()
        {
            DeselectAll();
            foreach (var kv in _nodeCards)
            {
                _selectedNodeIds.Add(kv.Key);
                kv.Value.SetSelected(true);
            }
            FireSelectionChanged();
        }

        private void FireSelectionChanged() =>
            OnSelectionChanged?.Invoke(_selectedNodeIds, _selectedEdgeId);

        // ── Active node (debug) ───────────────────────────────────────────────

        public void SetActiveNode(AGISGuid activeId)
        {
            foreach (var kv in _nodeCards)
                kv.Value.SetActive(kv.Key == activeId);
        }

        // ── Validation overlay ────────────────────────────────────────────────

        public void ApplyValidationReport(AGISGraphValidationReport report)
        {
            // Clear all
            foreach (var kv in _nodeCards)
            {
                kv.Value.SetError(false);
                kv.Value.SetWarning(false);
            }
            if (report == null) return;

            var errorNodes = new HashSet<AGISGuid>();
            var warnNodes = new HashSet<AGISGuid>();

            foreach (var issue in report.Issues)
            {
                if (issue.NodeId.IsValid)
                {
                    if (issue.Severity == AGISValidationSeverity.Error) errorNodes.Add(issue.NodeId);
                    else if (issue.Severity == AGISValidationSeverity.Warning) warnNodes.Add(issue.NodeId);
                }
                if (issue.EdgeId.IsValid && _graph?.edges != null)
                {
                    // Find fromNodeId for the edge and mark it
                    foreach (var edge in _graph.edges)
                    {
                        if (edge != null && edge.edgeId == issue.EdgeId && edge.fromNodeId.IsValid)
                        {
                            if (issue.Severity == AGISValidationSeverity.Error) errorNodes.Add(edge.fromNodeId);
                            else if (issue.Severity == AGISValidationSeverity.Warning) warnNodes.Add(edge.fromNodeId);
                        }
                    }
                }
            }

            foreach (var nodeId in errorNodes)
                if (_nodeCards.TryGetValue(nodeId, out var c)) c.SetError(true);
            foreach (var nodeId in warnNodes)
                if (_nodeCards.TryGetValue(nodeId, out var c) && !errorNodes.Contains(nodeId)) c.SetWarning(true);
        }

        // ── Frame All / Selected ──────────────────────────────────────────────

        public void FrameAll()
        {
            if (_nodeCards.Count == 0) return;
            FrameNodes(_nodeCards.Keys);
        }

        public void FrameSelected()
        {
            if (_selectedNodeIds.Count == 0) { FrameAll(); return; }
            FrameNodes(_selectedNodeIds);
        }

        private void FrameNodes(IEnumerable<AGISGuid> nodeIds)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool any = false;

            foreach (var id in nodeIds)
            {
                if (!_nodeCards.TryGetValue(id, out var card)) continue;
                var pos = card.Def.visual?.position ?? Vector2.zero;
                float w = Mathf.Max(card.layout.width, 180f);
                float h = Mathf.Max(card.layout.height, 80f);

                minX = Mathf.Min(minX, pos.x);
                minY = Mathf.Min(minY, pos.y);
                maxX = Mathf.Max(maxX, pos.x + w);
                maxY = Mathf.Max(maxY, pos.y + h);
                any = true;
            }

            if (!any) return;

            float margin = 80f;
            float worldW = maxX - minX + margin * 2;
            float worldH = maxY - minY + margin * 2;

            float canvasW = resolvedStyle.width;
            float canvasH = resolvedStyle.height;
            if (canvasW <= 0 || canvasH <= 0) return;

            float newZoom = Mathf.Clamp(Mathf.Min(canvasW / worldW, canvasH / worldH), ZoomMin, ZoomMax);
            float centerWorldX = (minX + maxX) * 0.5f;
            float centerWorldY = (minY + maxY) * 0.5f;

            _zoom = newZoom;
            _pan = new Vector2(canvasW * 0.5f - centerWorldX * _zoom, canvasH * 0.5f - centerWorldY * _zoom);
            ApplyTransform();
        }

        // ── Copy / Paste ──────────────────────────────────────────────────────

        public void CopySelected()
        {
            _clipboard.Clear();
            foreach (var id in _selectedNodeIds)
            {
                if (!_nodeCards.TryGetValue(id, out var card)) continue;
                _clipboard.Add(AGISGraphClone.CloneNode(card.Def));
            }
        }

        public void PasteClipboard()
        {
            if (_clipboard.Count == 0 || _graph == null || _history == null) return;

            DeselectAll();
            var offset = new Vector2(20f, 20f);

            foreach (var srcDef in _clipboard)
            {
                var newDef = AGISGraphClone.CloneNode(srcDef);
                newDef.nodeId = AGISGuid.New();
                newDef.visual ??= new AGISNodeVisualDef();
                newDef.visual.position += offset;

                var cmd = new AddNodeCommand(_graph, newDef);
                _history.Push(cmd);

                IAGISNodeType nodeType = null;
                if (_nodeTypes != null && !string.IsNullOrEmpty(newDef.nodeTypeId))
                    _nodeTypes.TryGet(newDef.nodeTypeId, out nodeType);
                if (nodeType == null) nodeType = new UnknownNodeTypePlaceholder(newDef.nodeTypeId);

                AddNodeCard(newDef, nodeType);
                SelectNode(newDef.nodeId, addToSelection: true);
            }
        }

        public void DuplicateSelected()
        {
            CopySelected();
            PasteClipboard();
        }

        // ── Delete selected ───────────────────────────────────────────────────

        public void DeleteSelected()
        {
            if (_selectedEdgeId.IsValid && _graph != null && _history != null)
            {
                _history.Push(new RemoveEdgeCommand(_graph, _selectedEdgeId));
                _selectedEdgeId = AGISGuid.Empty;
                _edgeLayer.RefreshEdges();
                FireSelectionChanged();
                return;
            }

            foreach (var id in new List<AGISGuid>(_selectedNodeIds))
                OnNodeDeleteRequested?.Invoke(id);
        }

        // ── Zoom level ────────────────────────────────────────────────────────

        public float ZoomLevel => _zoom;

        public void SetZoom(float zoom)
        {
            var center = new Vector2(resolvedStyle.width * 0.5f, resolvedStyle.height * 0.5f);
            var worldCenter = ScreenToWorld(center);
            _zoom = Mathf.Clamp(zoom, ZoomMin, ZoomMax);
            _pan = center - worldCenter * _zoom;
            ApplyTransform();
        }

        // ── Coordinate helpers ────────────────────────────────────────────────

        public Vector2 ScreenToWorld(Vector2 screenPos) => (screenPos - _pan) / _zoom;
        public Vector2 WorldToScreen(Vector2 worldPos) => worldPos * _zoom + _pan;

        // ─────────────────────────────────────────────────────────────────────
        // Entry indicator helpers
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateAllEntryIndicators()
        {
            if (_graph == null) return;
            foreach (var kv in _nodeCards)
                kv.Value.UpdateEntryIndicator(kv.Key == _graph.entryNodeId);
        }

        private void UpdateEntryIndicator(AGISGuid nodeId)
        {
            if (_nodeCards.TryGetValue(nodeId, out var card))
                card.UpdateEntryIndicator(_graph != null && nodeId == _graph.entryNodeId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pan / Zoom / Transform
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyTransform()
        {
            _worldContainer.transform.position = new Vector3(_pan.x, _pan.y, 0f);
            _worldContainer.transform.scale = new Vector3(_zoom, _zoom, 1f);

            Minimap.UpdateViewport(_zoom, _pan, new Vector2(resolvedStyle.width, resolvedStyle.height));
        }

        private void OnWheel(WheelEvent evt)
        {
            float scaleFactor = evt.delta.y < 0 ? 1.1f : 1f / 1.1f;

            var pointerPos = new Vector2(evt.localMousePosition.x, evt.localMousePosition.y);
            var worldUnderPointer = ScreenToWorld(pointerPos);

            float newZoom = Mathf.Clamp(_zoom * scaleFactor, ZoomMin, ZoomMax);
            _pan = pointerPos - worldUnderPointer * newZoom;
            _zoom = newZoom;

            ApplyTransform();
            evt.StopPropagation();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pointer events (pan, rubber-band, node drag, port drag)
        // ─────────────────────────────────────────────────────────────────────

        private void OnPointerDown(PointerDownEvent evt)
        {
            // Middle mouse or Alt+Left → pan
            if (evt.button == 2 || (evt.button == 0 && evt.altKey))
            {
                _isPanning = true;
                _panPointerId = evt.pointerId;
                _panStartPos = new Vector2(evt.position.x, evt.position.y);
                _panStartPan = _pan;
                this.CapturePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }

            // Left click on empty canvas area → rubber-band or deselect
            if (evt.button == 0 && !_isDraggingNodes && !_isDraggingPort)
            {
                // Start rubber-band
                _isRubberBanding = true;
                _rubberBandStart = new Vector2(evt.localPosition.x, evt.localPosition.y);
                UpdateRubberBand(_rubberBandStart, _rubberBandStart);
                _rubberBand.style.display = DisplayStyle.Flex;
                this.CapturePointer(evt.pointerId);

                if (!evt.shiftKey)
                    DeselectAll();
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_isPanning && evt.pointerId == _panPointerId)
            {
                var delta = new Vector2(evt.position.x, evt.position.y) - _panStartPos;
                _pan = _panStartPan + delta;
                ApplyTransform();
                evt.StopPropagation();
                return;
            }

            if (_isRubberBanding)
            {
                var current = new Vector2(evt.localPosition.x, evt.localPosition.y);
                UpdateRubberBand(_rubberBandStart, current);
                evt.StopPropagation();
                return;
            }

            if (_isDraggingNodes && _graph != null)
            {
                var currentScreen = new Vector2(evt.position.x, evt.position.y);
                var currentWorld = ScreenToWorld(currentScreen);

                foreach (var id in _selectedNodeIds)
                {
                    if (!_nodeCards.TryGetValue(id, out var card)) continue;
                    if (!_dragStartPositions.TryGetValue(id, out var startWorldPos)) continue;

                    var delta = currentWorld - ScreenToWorld(_dragStartScreenPos);
                    var newPos = startWorldPos + delta;
                    card.Def.visual ??= new AGISNodeVisualDef();
                    card.Def.visual.position = newPos;
                    card.SyncPosition();
                }

                _edgeLayer.RefreshEdges();
                Minimap.UpdateViewport(_zoom, _pan, new Vector2(resolvedStyle.width, resolvedStyle.height));
                evt.StopPropagation();
                return;
            }

            if (_isDraggingPort)
            {
                _portDragCurrentPos = new Vector2(evt.localPosition.x, evt.localPosition.y);
                // TODO: draw a draft edge using the overlay layer
                evt.StopPropagation();
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_isPanning && evt.pointerId == _panPointerId)
            {
                _isPanning = false;
                _panPointerId = -1;
                this.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }

            if (_isRubberBanding)
            {
                _isRubberBanding = false;
                _rubberBand.style.display = DisplayStyle.None;
                this.ReleasePointer(evt.pointerId);

                // Select nodes in rubber-band rect
                var rbScreenRect = GetRubberBandScreenRect(
                    _rubberBandStart,
                    new Vector2(evt.localPosition.x, evt.localPosition.y));

                if (rbScreenRect.width > 4f || rbScreenRect.height > 4f)
                {
                    // Convert to world rect
                    var worldMin = ScreenToWorld(new Vector2(rbScreenRect.xMin, rbScreenRect.yMin));
                    var worldMax = ScreenToWorld(new Vector2(rbScreenRect.xMax, rbScreenRect.yMax));
                    var worldRect = Rect.MinMaxRect(worldMin.x, worldMin.y, worldMax.x, worldMax.y);

                    foreach (var kv in _nodeCards)
                    {
                        var pos = kv.Value.Def.visual?.position ?? Vector2.zero;
                        var cardRect = new Rect(pos.x, pos.y,
                            Mathf.Max(kv.Value.layout.width, 180f),
                            Mathf.Max(kv.Value.layout.height, 80f));

                        if (worldRect.Overlaps(cardRect))
                            SelectNode(kv.Key, addToSelection: true);
                    }
                }

                evt.StopPropagation();
                return;
            }

            if (_isDraggingNodes)
            {
                CommitNodeDrag();
                this.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
                return;
            }

            if (_isDraggingPort)
            {
                FinishPortDrag(evt);
                evt.StopPropagation();
                return;
            }
        }

        private void UpdateRubberBand(Vector2 a, Vector2 b)
        {
            var rect = GetRubberBandScreenRect(a, b);
            _rubberBand.style.left = rect.x;
            _rubberBand.style.top = rect.y;
            _rubberBand.style.width = rect.width;
            _rubberBand.style.height = rect.height;
        }

        private static Rect GetRubberBandScreenRect(Vector2 a, Vector2 b)
        {
            return Rect.MinMaxRect(
                Mathf.Min(a.x, b.x),
                Mathf.Min(a.y, b.y),
                Mathf.Max(a.x, b.x),
                Mathf.Max(a.y, b.y));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Node drag (card header → pointer captured by canvas)
        // ─────────────────────────────────────────────────────────────────────

        private void StartNodeDrag(AGISNodeCardElement card)
        {
            if (_isDraggingPort) return;

            // Select this card if not already selected
            if (!_selectedNodeIds.Contains(card.Def.nodeId))
                SelectNode(card.Def.nodeId);

            _isDraggingNodes = true;
            _dragStartScreenPos = new Vector2(
                card.worldBound.x + card.resolvedStyle.width * 0.5f,
                card.worldBound.y + card.resolvedStyle.height * 0.5f);

            _dragStartPositions = new Dictionary<AGISGuid, Vector2>();
            foreach (var id in _selectedNodeIds)
            {
                if (_nodeCards.TryGetValue(id, out var c))
                    _dragStartPositions[id] = c.Def.visual?.position ?? Vector2.zero;
            }

            // Capture on canvas
            this.CapturePointer(PointerId.mousePointerId);
        }

        private void CommitNodeDrag()
        {
            _isDraggingNodes = false;
            _dragPointerId = -1;

            if (_history == null || _dragStartPositions == null) return;

            // Compute final positions
            foreach (var id in _selectedNodeIds)
            {
                if (!_nodeCards.TryGetValue(id, out var card)) continue;
                if (!_dragStartPositions.TryGetValue(id, out var startPos)) continue;

                var endPos = card.Def.visual?.position ?? Vector2.zero;
                var delta = endPos - startPos;
                if (delta.sqrMagnitude < 0.01f) continue;

                // Restore start position and issue proper command
                if (card.Def.visual != null) card.Def.visual.position = startPos;
                card.SyncPosition();

                _history.Push(new MoveNodesCommand(_graph, new[] { id }, delta));
                // After command.Do() the position is applied again
                card.SyncPosition();
            }

            _edgeLayer.RefreshEdges();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Port drag (edge creation)
        // ─────────────────────────────────────────────────────────────────────

        private void StartPortDrag(AGISNodeCardElement sourceCard)
        {
            _isDraggingPort = true;
            _portDragSourceCard = sourceCard;
            _portDragCurrentPos = sourceCard.GetOutPortPosition();
        }

        private void FinishPortDrag(PointerUpEvent evt)
        {
            _isDraggingPort = false;
            if (_portDragSourceCard == null) return;

            // Check if pointer is over a node card
            var dropPos = new Vector2(evt.localPosition.x, evt.localPosition.y);
            AGISNodeCardElement dropTarget = null;

            foreach (var kv in _nodeCards)
            {
                if (kv.Value == _portDragSourceCard) continue;
                var cardWorldBound = kv.Value.worldBound;
                // Convert to local (canvas) coords
                var localBound = new Rect(
                    cardWorldBound.x - worldBound.x,
                    cardWorldBound.y - worldBound.y,
                    cardWorldBound.width,
                    cardWorldBound.height);

                if (localBound.Contains(dropPos))
                {
                    dropTarget = kv.Value;
                    break;
                }
            }

            if (dropTarget != null)
            {
                OnEdgeCreateRequested?.Invoke(_portDragSourceCard.Def.nodeId, dropTarget.Def.nodeId);
            }
            else
            {
                var worldPos = ScreenToWorld(dropPos);
                OnPortDroppedOnEmpty?.Invoke(_portDragSourceCard.Def.nodeId, worldPos);
            }

            _portDragSourceCard = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Edge click
        // ─────────────────────────────────────────────────────────────────────

        private void OnEdgeLayerClicked(AGISGuid edgeId)
        {
            SelectEdge(edgeId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Context menus
        // ─────────────────────────────────────────────────────────────────────

        private void OnContextClick(ContextClickEvent evt)
        {
            var menu = new GenericDropdownMenu();
            menu.AddItem("Add Node", false, () => OnAddNodeAtCenterRequested?.Invoke());
            if (_clipboard.Count > 0)
                menu.AddItem("Paste", false, () => PasteClipboard());
            else
                menu.AddDisabledItem("Paste", false);
            menu.AddSeparator("");
            menu.AddItem("Frame All (F)", false, () => FrameAll());
            menu.AddItem("Select All (Ctrl+A)", false, () => SelectAll());
            menu.DropDown(new Rect(evt.mousePosition, Vector2.zero), this, false);
            evt.StopPropagation();
        }

        private void ShowNodeContextMenu(AGISNodeCardElement card, ContextClickEvent evt)
        {
            bool isEntry = _graph != null && card.Def.nodeId == _graph.entryNodeId;

            var menu = new GenericDropdownMenu();
            menu.AddItem("Set as Entry", isEntry, () =>
            {
                if (_graph == null || _history == null) return;
                _history.Push(new SetEntryNodeCommand(_graph, _graph.entryNodeId, card.Def.nodeId));
                UpdateAllEntryIndicators();
                OnSetEntryRequested?.Invoke(card.Def.nodeId, true);
            });
            menu.AddSeparator("");
            menu.AddItem("Duplicate", false, () => { SelectNode(card.Def.nodeId); DuplicateSelected(); });
            menu.AddItem("Copy", false, () => { SelectNode(card.Def.nodeId); CopySelected(); });
            menu.AddSeparator("");

            if (card.NodeType.Kind == AGISNodeKind.Grouped)
            {
                menu.AddItem("Open Sub-Graph \u25B6", false, () =>
                    OnOpenSubGraphRequested?.Invoke(card.Def.nodeId));
                menu.AddSeparator("");
            }

            if (card.NodeType.Kind != AGISNodeKind.AnyState)
                menu.AddItem("Delete", false, () => { SelectNode(card.Def.nodeId); DeleteSelected(); });

            menu.DropDown(new Rect(evt.mousePosition, Vector2.zero), this, false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Keyboard shortcuts
        // ─────────────────────────────────────────────────────────────────────

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
            {
                DeleteSelected();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.F)
            {
                FrameAll();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.F && evt.shiftKey)
            {
                FrameSelected();
                evt.StopPropagation();
                return;
            }

            if (evt.ctrlKey)
            {
                switch (evt.keyCode)
                {
                    case KeyCode.A:
                        SelectAll();
                        evt.StopPropagation();
                        break;
                    case KeyCode.D:
                        DuplicateSelected();
                        evt.StopPropagation();
                        break;
                    case KeyCode.C:
                        CopySelected();
                        evt.StopPropagation();
                        break;
                    case KeyCode.V:
                        PasteClipboard();
                        evt.StopPropagation();
                        break;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Placeholder node type for unknown typeIds (loaded from graph)
    // ═══════════════════════════════════════════════════════════════════════════

    internal sealed class UnknownNodeTypePlaceholder : IAGISNodeType
    {
        public string TypeId { get; }
        public string DisplayName => $"? {TypeId}";
        public AGISNodeKind Kind => AGISNodeKind.Normal;
        public AGISParamSchema Schema { get; } = new AGISParamSchema();

        public UnknownNodeTypePlaceholder(string typeId) => TypeId = typeId ?? "unknown";

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args) => null;
    }
}
