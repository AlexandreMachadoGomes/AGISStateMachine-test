// File: AGISNodeCardElement.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/Nodes/
// Purpose: UIToolkit VisualElement representing a single state machine node on the canvas.
//          Fully self-contained — no UnityEditor dependency.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.Runtime;
using AGIS.Dialogue;

namespace AGIS.ESM.RuntimeEditor
{
    public sealed class AGISNodeCardElement : VisualElement
    {
        // ── Public events ─────────────────────────────────────────────────────
        public event Action OnDeleteRequested;
        public event Action<AGISNodeInstanceDef> OnPositionChanged;
        public event Action<AGISNodeInstanceDef> OnParamChanged;
        public event Action<AGISNodeCardElement> OnPortDragStarted;   // OUT port drag began
        public event Action<AGISNodeCardElement> OnHeaderPointerDown; // node drag started

        // ── Data references ───────────────────────────────────────────────────
        public AGISNodeInstanceDef Def { get; private set; }
        public IAGISNodeType NodeType { get; private set; }

        private readonly AGISStateMachineGraph _graph;
        private readonly AGISEditorHistory _history;
        private readonly AGISConditionTypeRegistry _condTypes;

        // ── Visual sub-elements ───────────────────────────────────────────────
        private VisualElement _header;
        private Label _entryStarLabel;
        private Label _titleLabel;
        private Button _collapseBtn;
        private Button _deleteBtn;
        private VisualElement _body;
        private VisualElement _footer;
        private VisualElement _outPort;

        // ── Icons by kind ─────────────────────────────────────────────────────
        private static string GetKindIcon(AGISNodeKind kind) => kind switch
        {
            AGISNodeKind.Normal   => "\u25cf",   // ●
            AGISNodeKind.AnyState => "\u2B21",   // ⬡
            AGISNodeKind.Grouped  => "\u29C9",   // ⧉
            AGISNodeKind.Parallel => "\u29BC",   // ⫼ (substitute)
            _                     => "\u25cf",
        };

        private static string GetHeaderClass(AGISNodeKind kind) => kind switch
        {
            AGISNodeKind.Normal   => "agis-node__header--normal",
            AGISNodeKind.AnyState => "agis-node__header--anystate",
            AGISNodeKind.Grouped  => "agis-node__header--grouped",
            AGISNodeKind.Parallel => "agis-node__header--parallel",
            _                     => "agis-node__header--normal",
        };

        // ─────────────────────────────────────────────────────────────────────
        // Constructor
        // ─────────────────────────────────────────────────────────────────────

        public AGISNodeCardElement(
            AGISNodeInstanceDef def,
            IAGISNodeType type,
            AGISStateMachineGraph graph,
            AGISEditorHistory history,
            AGISConditionTypeRegistry condTypes)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            NodeType = type ?? throw new ArgumentNullException(nameof(type));
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _condTypes = condTypes;

            AddToClassList("agis-node");

            // Position from visual def
            style.position = Position.Absolute;
            style.left = def.visual?.position.x ?? 0f;
            style.top = def.visual?.position.y ?? 0f;

            BuildHeader();
            BuildBody();
            BuildFooter();

            ApplyCollapsedState(def.visual?.collapsed ?? false);

            // Wire double-click on header to toggle collapse
            _header.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2 && evt.button == 0)
                {
                    ToggleCollapse();
                    evt.StopPropagation();
                }
            });

            // Pointer down on header — node dragging (handled by canvas)
            _header.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                    OnHeaderPointerDown?.Invoke(this);
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // Header
        // ─────────────────────────────────────────────────────────────────────

        private void BuildHeader()
        {
            _header = new VisualElement();
            _header.AddToClassList("agis-node__header");
            _header.AddToClassList(GetHeaderClass(NodeType.Kind));

            // Kind icon
            var iconLabel = new Label(GetKindIcon(NodeType.Kind));
            iconLabel.AddToClassList("agis-node__icon");
            _header.Add(iconLabel);

            // Entry star (initially hidden; UpdateEntryIndicator shows/hides it)
            _entryStarLabel = new Label("\u2605"); // ★
            _entryStarLabel.AddToClassList("agis-node__entry-star");
            _entryStarLabel.style.display = DisplayStyle.None;
            _header.Add(_entryStarLabel);

            // Title
            _titleLabel = new Label(NodeType.DisplayName ?? NodeType.TypeId);
            _titleLabel.AddToClassList("agis-node__title");
            _header.Add(_titleLabel);

            // Collapse button — hidden for AnyState
            _collapseBtn = new Button(ToggleCollapse);
            _collapseBtn.text = "\u25BE"; // ▾
            _collapseBtn.AddToClassList("agis-node__collapse-btn");
            _collapseBtn.tooltip = "Toggle collapse";
            if (NodeType.Kind == AGISNodeKind.AnyState)
                _collapseBtn.style.display = DisplayStyle.None;
            _header.Add(_collapseBtn);

            // Delete button — hidden for AnyState
            _deleteBtn = new Button(() => OnDeleteRequested?.Invoke());
            _deleteBtn.text = "\u00D7"; // ×
            _deleteBtn.AddToClassList("agis-node__delete-btn");
            _deleteBtn.tooltip = "Delete node";
            if (NodeType.Kind == AGISNodeKind.AnyState)
                _deleteBtn.style.display = DisplayStyle.None;
            _header.Add(_deleteBtn);

            Add(_header);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Body
        // ─────────────────────────────────────────────────────────────────────

        private void BuildBody()
        {
            _body = new VisualElement();
            _body.AddToClassList("agis-node__body");

            if (NodeType.Kind == AGISNodeKind.AnyState)
            {
                // AnyState has no body
                _body.style.display = DisplayStyle.None;
            }
            else
            {
                PopulateBody();
            }

            Add(_body);
        }

        private void PopulateBody()
        {
            _body.Clear();

            var schema = NodeType.Schema;
            var instanceParams = Def.@params ??= new AGISParamTable();

            if (schema != null && schema.Specs != null && schema.Specs.Count > 0)
            {
                foreach (var spec in schema.Specs)
                {
                    if (spec == null) continue;

                    var fieldRow = AGISParamFieldDrawer.CreateField(spec, instanceParams, (key, newValue) =>
                    {
                        OnParamChanged?.Invoke(Def);
                    });

                    // Listen for reset-to-default events so we can rebuild the field
                    fieldRow.RegisterCallback<ResetToDefaultEvent>(evt =>
                    {
                        int idx = _body.IndexOf(fieldRow);
                        var rebuilt = AGISParamFieldDrawer.CreateField(spec, instanceParams, (key2, newValue2) =>
                        {
                            OnParamChanged?.Invoke(Def);
                        });
                        rebuilt.RegisterCallback<ResetToDefaultEvent>(innerEvt =>
                        {
                            // Second-level rebuild omitted for brevity — user can click again
                        });
                        _body.Insert(idx, rebuilt);
                        _body.Remove(fieldRow);
                    });

                    _body.Add(fieldRow);
                }
            }

            // ── Dialogue section ──────────────────────────────────────────────
            if (NodeType.TypeId == "agis.dialogue")
            {
                BuildDialogueSection();
            }

            // ── Grouped section ───────────────────────────────────────────────
            if (NodeType.Kind == AGISNodeKind.Grouped)
            {
                BuildGroupedSection();
            }
        }

        private void BuildDialogueSection()
        {
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            separator.style.marginTop = 4;
            separator.style.marginBottom = 4;
            _body.Add(separator);

            var choiceKey = AGISDialogueConstants.DefaultChoiceKey;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 2;

            var headerLabel = new Label("Choices");
            headerLabel.style.flexGrow = 1;
            headerLabel.style.fontSize = 11;
            headerLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            var addChoiceBtn = new Button(() =>
            {
                AGISDialogueEdgeSync.AddChoice(_graph, Def.nodeId, choiceKey);
                OnParamChanged?.Invoke(Def);
                RefreshDialogueChoiceDisplay();
            });
            addChoiceBtn.text = "+ Choice";
            addChoiceBtn.style.fontSize = 10;
            addChoiceBtn.style.height = 18;
            addChoiceBtn.style.paddingLeft = 4;
            addChoiceBtn.style.paddingRight = 4;

            var removeChoiceBtn = new Button(() =>
            {
                AGISDialogueEdgeSync.RemoveLastChoice(_graph, Def.nodeId, choiceKey);
                OnParamChanged?.Invoke(Def);
                RefreshDialogueChoiceDisplay();
            });
            removeChoiceBtn.text = "- Last";
            removeChoiceBtn.style.fontSize = 10;
            removeChoiceBtn.style.height = 18;
            removeChoiceBtn.style.paddingLeft = 4;
            removeChoiceBtn.style.paddingRight = 4;
            removeChoiceBtn.style.marginLeft = 2;

            headerRow.Add(headerLabel);
            headerRow.Add(addChoiceBtn);
            headerRow.Add(removeChoiceBtn);
            _body.Add(headerRow);

            // Placeholder container updated by RefreshDialogueChoiceDisplay
            var choiceListContainer = new VisualElement();
            choiceListContainer.name = "dialogue-choice-list";
            _body.Add(choiceListContainer);

            RefreshDialogueChoiceDisplay();
        }

        private void RefreshDialogueChoiceDisplay()
        {
            var container = _body.Q<VisualElement>("dialogue-choice-list");
            if (container == null) return;
            container.Clear();

            var choiceKey = AGISDialogueConstants.DefaultChoiceKey;
            var choices = AGISDialogueEdgeSync.FindChoiceEdges(_graph, Def.nodeId);
            var endedEdge = AGISDialogueEdgeSync.FindEndedEdge(_graph, Def.nodeId);

            if (choices.Count == 0)
            {
                var endedLabel = new Label(endedEdge != null ? "Mode: Ended transition" : "Mode: No edges");
                endedLabel.style.fontSize = 10;
                endedLabel.style.color = new StyleColor(new Color(0.55f, 0.55f, 0.55f));
                endedLabel.style.marginLeft = 4;
                container.Add(endedLabel);
            }
            else
            {
                for (int i = 0; i < choices.Count; i++)
                {
                    var (edge, option) = choices[i];
                    var choiceRow = new Label($"Choice {option}: → {(edge.toNodeId.IsValid ? edge.toNodeId.Value.Substring(0, 8) : "unconnected")}");
                    choiceRow.style.fontSize = 10;
                    choiceRow.style.color = new StyleColor(new Color(0.65f, 0.75f, 0.55f));
                    choiceRow.style.marginLeft = 4;
                    container.Add(choiceRow);
                }
            }
        }

        private void BuildGroupedSection()
        {
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            separator.style.marginTop = 4;
            separator.style.marginBottom = 4;
            _body.Add(separator);

            var assetRow = new VisualElement();
            assetRow.style.flexDirection = FlexDirection.Row;
            assetRow.style.alignItems = Align.Center;
            assetRow.style.paddingLeft = 2;
            assetRow.style.paddingRight = 2;

            var assetLabel = new Label("Asset:");
            assetLabel.style.fontSize = 10;
            assetLabel.style.color = new StyleColor(new Color(0.55f, 0.55f, 0.55f));
            assetLabel.style.marginRight = 4;
            assetLabel.style.width = 36;

            var assetIdLabel = new Label(Def.groupAssetId.IsValid
                ? Def.groupAssetId.Value.Substring(0, 8) + "…"
                : "(none)");
            assetIdLabel.style.fontSize = 10;
            assetIdLabel.style.flexGrow = 1;
            assetIdLabel.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.85f));

            var openBtn = new Button();
            openBtn.text = "Open \u25B6";
            openBtn.tooltip = "Drill into grouped sub-graph";
            openBtn.style.fontSize = 10;
            openBtn.style.height = 18;
            openBtn.style.paddingLeft = 4;
            openBtn.style.paddingRight = 4;
            // Opening the sub-graph is handled by the canvas/window via the card's context menu.
            // We leave the button wired to a simple event for now.
            openBtn.clicked += () => OnParamChanged?.Invoke(Def);

            assetRow.Add(assetLabel);
            assetRow.Add(assetIdLabel);
            assetRow.Add(openBtn);
            _body.Add(assetRow);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Footer (OUT port)
        // ─────────────────────────────────────────────────────────────────────

        private void BuildFooter()
        {
            _footer = new VisualElement();
            _footer.AddToClassList("agis-node__footer");

            // AnyState has no footer (it has no outgoing ports in the conventional sense)
            if (NodeType.Kind != AGISNodeKind.AnyState)
            {
                var portLabel = new Label("OUT");
                portLabel.style.fontSize = 9;
                portLabel.style.color = new StyleColor(new Color(0.55f, 0.55f, 0.55f));
                portLabel.style.marginRight = 4;
                _footer.Add(portLabel);

                _outPort = new VisualElement();
                _outPort.AddToClassList("agis-node__port");
                _outPort.tooltip = "Drag to create a transition";

                _outPort.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0)
                    {
                        evt.StopPropagation();
                        OnPortDragStarted?.Invoke(this);
                    }
                });

                _footer.Add(_outPort);
            }

            Add(_footer);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Shows or hides the gold entry star overlay.</summary>
        public void UpdateEntryIndicator(bool isEntry)
        {
            _entryStarLabel.style.display = isEntry ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Adds or removes the .agis-node--selected CSS class.</summary>
        public void SetSelected(bool selected)
        {
            if (selected)
                AddToClassList("agis-node--selected");
            else
                RemoveFromClassList("agis-node--selected");
        }

        /// <summary>Adds or removes the .agis-node--active CSS class (debug overlay).</summary>
        public void SetActive(bool active)
        {
            if (active)
                AddToClassList("agis-node--active");
            else
                RemoveFromClassList("agis-node--active");
        }

        /// <summary>Adds or removes the .agis-node--error CSS class.</summary>
        public void SetError(bool hasError)
        {
            if (hasError)
                AddToClassList("agis-node--error");
            else
                RemoveFromClassList("agis-node--error");
        }

        /// <summary>Adds or removes the .agis-node--warning CSS class.</summary>
        public void SetWarning(bool hasWarning)
        {
            if (hasWarning)
                AddToClassList("agis-node--warning");
            else
                RemoveFromClassList("agis-node--warning");
        }

        /// <summary>
        /// Updates the card's absolute position to match Def.visual.position.
        /// Called by canvas after a drag commit.
        /// </summary>
        public void SyncPosition()
        {
            if (Def.visual == null) return;
            style.left = Def.visual.position.x;
            style.top = Def.visual.position.y;
        }

        /// <summary>
        /// Returns the world-space (canvas-space) center of the OUT port,
        /// used by the edge layer to draw bezier curves.
        /// Returns Vector2.zero if the port doesn't exist (e.g. AnyState).
        /// </summary>
        public Vector2 GetOutPortPosition()
        {
            if (_outPort == null) return Vector2.zero;

            var localBounds = _outPort.layout;
            // Center of port in local space of the card
            var localCenter = new Vector2(
                layout.x + localBounds.x + localBounds.width * 0.5f,
                layout.y + localBounds.y + localBounds.height * 0.5f
            );
            return localCenter;
        }

        /// <summary>
        /// Returns the world-space center-left of the card (input connection point).
        /// </summary>
        public Vector2 GetInPortPosition()
        {
            return new Vector2(layout.x, layout.y + layout.height * 0.5f);
        }

        /// <summary>
        /// Returns the world-space center of the card.
        /// </summary>
        public Vector2 GetCenterPosition()
        {
            return new Vector2(layout.x + layout.width * 0.5f, layout.y + layout.height * 0.5f);
        }

        /// <summary>Rebuilds the body content (called after undo/redo or external param changes).</summary>
        public void Rebuild()
        {
            PopulateBody();
            ApplyCollapsedState(Def.visual?.collapsed ?? false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Collapse
        // ─────────────────────────────────────────────────────────────────────

        private void ToggleCollapse()
        {
            if (NodeType.Kind == AGISNodeKind.AnyState) return;

            bool collapsed = !(Def.visual?.collapsed ?? false);
            if (Def.visual == null) Def.visual = new AGISNodeVisualDef();
            Def.visual.collapsed = collapsed;

            ApplyCollapsedState(collapsed);
        }

        private void ApplyCollapsedState(bool collapsed)
        {
            if (NodeType.Kind == AGISNodeKind.AnyState) return;

            _body.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _footer.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            if (_collapseBtn != null)
                _collapseBtn.text = collapsed ? "\u25B8" : "\u25BE"; // ▸ / ▾
        }
    }
}
