// File: AGISNodeInspectorPanel.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/Panels/
// Purpose: Node inspector tab — header, entry-node toggle, params, persistent keys,
//          validation, dialogue section, grouped section, delete.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.Runtime;
using AGIS.Dialogue;

namespace AGIS.ESM.RuntimeEditor.Panels
{
    public sealed class AGISNodeInspectorPanel : VisualElement
    {
        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly AGISEditorHistory        _history;
        private readonly AGISNodeTypeRegistry     _nodeTypes;
        private readonly AGISConditionTypeRegistry _condTypes;

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<AGISGuid> OnDeleteNode;
        public event Action<AGISGuid> OnSetEntryNode;
        public event Action<AGISGuid> OnDrillIntoGrouped;

        // ── State ─────────────────────────────────────────────────────────────
        private AGISNodeInstanceDef     _def;
        private IAGISNodeType           _type;
        private AGISStateMachineGraph   _graph;
        private AGISGraphValidationReport _report;

        // ── UI containers ─────────────────────────────────────────────────────
        private readonly ScrollView      _scroll;
        private readonly VisualElement   _inner;
        private readonly Label           _kindLabel;
        private readonly Label           _displayNameLabel;
        private readonly Label           _typeIdLabel;
        private readonly Label           _guidLabel;
        private readonly Button          _copyGuidBtn;
        private readonly Button          _setEntryBtn;
        private readonly VisualElement   _paramsContainer;
        private readonly VisualElement   _persistentContainer;
        private readonly VisualElement   _validationContainer;
        private readonly VisualElement   _dialogueContainer;
        private readonly VisualElement   _groupedContainer;
        private readonly VisualElement   _deleteContainer;

        // ─────────────────────────────────────────────────────────────────────
        public AGISNodeInspectorPanel(
            AGISEditorHistory history,
            AGISNodeTypeRegistry nodeTypes,
            AGISConditionTypeRegistry condTypes)
        {
            _history   = history;
            _nodeTypes = nodeTypes;
            _condTypes = condTypes;

            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1;
            Add(_scroll);

            _inner = new VisualElement();
            _inner.style.flexDirection = FlexDirection.Column;
            _inner.style.paddingLeft = _inner.style.paddingRight = 8f;
            _inner.style.paddingTop  = _inner.style.paddingBottom = 6f;
            _scroll.Add(_inner);

            // ── Header ────────────────────────────────────────────────────────
            _inner.Add(MakeSectionHeader("Node"));

            _kindLabel = new Label();
            _kindLabel.style.color = new Color(0.6f, 0.6f, 1f);
            _kindLabel.style.marginBottom = 2f;
            _inner.Add(_kindLabel);

            _displayNameLabel = new Label();
            _displayNameLabel.style.fontSize = 14;
            _displayNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _displayNameLabel.style.marginBottom = 2f;
            _inner.Add(_displayNameLabel);

            _typeIdLabel = new Label();
            _typeIdLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            _typeIdLabel.style.marginBottom = 4f;
            _inner.Add(_typeIdLabel);

            var guidRow = new VisualElement();
            guidRow.style.flexDirection = FlexDirection.Row;
            _guidLabel = new Label();
            _guidLabel.style.flexGrow = 1;
            _guidLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            _copyGuidBtn = new Button(() =>
            {
                if (_def != null) GUIUtility.systemCopyBuffer = (string)_def.nodeId;
            }) { text = "Copy" };
            _copyGuidBtn.AddToClassList("agis-toolbar__button");
            guidRow.Add(_guidLabel);
            guidRow.Add(_copyGuidBtn);
            _inner.Add(guidRow);

            // ── Is Entry ──────────────────────────────────────────────────────
            _inner.Add(MakeSectionHeader("Entry Point"));
            _setEntryBtn = new Button(OnSetEntryClicked) { text = "\u2605 Set as Entry" };
            _setEntryBtn.AddToClassList("agis-toolbar__button");
            _inner.Add(_setEntryBtn);

            // ── Params ────────────────────────────────────────────────────────
            _inner.Add(MakeSectionHeader("Parameters"));
            _paramsContainer = new VisualElement();
            _paramsContainer.style.flexDirection = FlexDirection.Column;
            _inner.Add(_paramsContainer);

            // ── Persistent Keys ───────────────────────────────────────────────
            _inner.Add(MakeSectionHeader("Persistent State Keys"));
            _persistentContainer = new VisualElement();
            _persistentContainer.style.flexDirection = FlexDirection.Column;
            _inner.Add(_persistentContainer);

            // ── Validation ────────────────────────────────────────────────────
            _inner.Add(MakeSectionHeader("Validation Issues"));
            _validationContainer = new VisualElement();
            _validationContainer.style.flexDirection = FlexDirection.Column;
            _inner.Add(_validationContainer);

            // ── Dialogue ──────────────────────────────────────────────────────
            _dialogueContainer = new VisualElement();
            _dialogueContainer.style.flexDirection = FlexDirection.Column;
            _inner.Add(_dialogueContainer);

            // ── Grouped ───────────────────────────────────────────────────────
            _groupedContainer = new VisualElement();
            _groupedContainer.style.flexDirection = FlexDirection.Column;
            _inner.Add(_groupedContainer);

            // ── Delete ────────────────────────────────────────────────────────
            _inner.Add(MakeSectionHeader("Danger Zone"));
            _deleteContainer = new VisualElement();
            _deleteContainer.style.flexDirection = FlexDirection.Column;
            _inner.Add(_deleteContainer);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Populate(
            AGISNodeInstanceDef def,
            IAGISNodeType type,
            AGISStateMachineGraph graph,
            AGISGraphValidationReport report)
        {
            _def    = def;
            _type   = type;
            _graph  = graph;
            _report = report;
            Rebuild();
        }

        // ── Rebuild ───────────────────────────────────────────────────────────

        private void Rebuild()
        {
            if (_def == null || _type == null)
            {
                _displayNameLabel.text = "(no node selected)";
                return;
            }

            // Header
            _kindLabel.text        = _type.Kind.ToString();
            _displayNameLabel.text = _type.DisplayName ?? _type.TypeId;
            _typeIdLabel.text      = _type.TypeId;
            _guidLabel.text        = (string)_def.nodeId;

            // Kind color
            _kindLabel.style.color = KindColor(_type.Kind);

            // Entry button
            bool isEntry = _graph != null && _graph.entryNodeId == _def.nodeId;
            _setEntryBtn.SetEnabled(!isEntry);
            _setEntryBtn.text = isEntry ? "\u2605 This is the Entry Node" : "\u2605 Set as Entry";

            // Params
            RebuildParams();

            // Persistent keys
            RebuildPersistentKeys();

            // Validation
            RebuildValidation();

            // Dialogue section
            RebuildDialogueSection();

            // Grouped section
            RebuildGroupedSection();

            // Delete
            RebuildDeleteSection();
        }

        private void RebuildParams()
        {
            _paramsContainer.Clear();

            if (_type?.Schema?.Specs == null || _type.Schema.Specs.Count == 0)
            {
                _paramsContainer.Add(new Label("(no parameters)") { style = { color = new Color(0.5f, 0.5f, 0.5f) } });
                return;
            }

            // Group by category
            var grouped = new Dictionary<string, List<AGISParamSpec>>();
            foreach (var spec in _type.Schema.Specs)
            {
                var cat = string.IsNullOrEmpty(spec.category) ? "General" : spec.category;
                if (!grouped.TryGetValue(cat, out var list))
                {
                    list = new List<AGISParamSpec>();
                    grouped[cat] = list;
                }
                list.Add(spec);
            }

            foreach (var kv in grouped)
            {
                var foldout = new Foldout { text = kv.Key, value = true };
                foldout.style.marginBottom = 4f;

                foreach (var spec in kv.Value)
                {
                    var capturedSpec = spec;
                    var capturedDef  = _def;

                    var field = AGISParamFieldDrawer.CreateField(
                        spec,
                        capturedDef.@params,
                        (key, val) =>
                        {
                            if (capturedDef.@params == null)
                                capturedDef.@params = new AGISParamTable();

                            AGISValue oldVal = default;
                            bool hadOld = capturedDef.@params.TryGet(key, out oldVal);

                            var cmd = new ChangeNodeParamCommand(
                                _graph, capturedDef.nodeId, key, oldVal, val, hadOld);
                            _history?.Push(cmd);
                        });

                    foldout.Add(field);
                }

                _paramsContainer.Add(foldout);
            }
        }

        private void RebuildPersistentKeys()
        {
            _persistentContainer.Clear();

            if (_type is IAGISPersistentNodeType persistent && persistent.PersistentParams != null)
            {
                if (persistent.PersistentParams.Count == 0)
                {
                    _persistentContainer.Add(new Label("(none)") { style = { color = new Color(0.5f, 0.5f, 0.5f) } });
                    return;
                }

                foreach (var spec in persistent.PersistentParams)
                {
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.marginBottom  = 2f;

                    var keyLbl  = new Label(spec.key);
                    keyLbl.style.flexGrow = 1;
                    keyLbl.style.color    = new Color(0.8f, 0.9f, 1f);

                    var typeLbl = new Label($"[{spec.type}]");
                    typeLbl.style.color = new Color(0.6f, 0.6f, 0.6f);
                    typeLbl.style.marginLeft = 4f;

                    var defLbl = new Label(FormatValue(spec.defaultValue));
                    defLbl.style.color = new Color(0.5f, 0.8f, 0.5f);
                    defLbl.style.marginLeft = 6f;

                    row.Add(keyLbl);
                    row.Add(typeLbl);
                    row.Add(defLbl);
                    _persistentContainer.Add(row);
                }
            }
            else
            {
                _persistentContainer.Add(new Label("(none)") { style = { color = new Color(0.5f, 0.5f, 0.5f) } });
            }
        }

        private void RebuildValidation()
        {
            _validationContainer.Clear();

            if (_report == null || _def == null)
            {
                _validationContainer.Add(new Label("(no report)") { style = { color = new Color(0.5f, 0.5f, 0.5f) } });
                return;
            }

            bool any = false;
            foreach (var issue in _report.Issues)
            {
                if (issue.NodeId != _def.nodeId) continue;
                any = true;

                var row = new VisualElement();
                row.AddToClassList("agis-validation-row");
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom  = 2f;

                var icon = new Label(issue.Severity == AGISValidationSeverity.Error ? "\u274c" : "\u26a0\ufe0f");
                icon.AddToClassList("agis-validation-row__icon");
                if (issue.Severity == AGISValidationSeverity.Error)
                    icon.AddToClassList("agis-validation-row__icon--error");
                else
                    icon.AddToClassList("agis-validation-row__icon--warning");

                var msg = new Label($"[{issue.Code}] {issue.Message}");
                msg.AddToClassList("agis-validation-row__msg");
                msg.style.flexGrow = 1;
                msg.style.whiteSpace = WhiteSpace.Normal;

                row.Add(icon);
                row.Add(msg);
                _validationContainer.Add(row);
            }

            if (!any)
            {
                var ok = new Label("\u2713 No issues for this node");
                ok.style.color = new Color(0.2f, 0.8f, 0.2f);
                _validationContainer.Add(ok);
            }
        }

        private void RebuildDialogueSection()
        {
            _dialogueContainer.Clear();

            if (_def == null || _graph == null) return;
            if (_def.nodeTypeId != "agis.dialogue") return;

            _dialogueContainer.Add(MakeSectionHeader("Dialogue"));

            // Read choiceKey from params
            string choiceKey = AGISDialogueConstants.DefaultChoiceKey;
            if (_def.@params != null && _def.@params.TryGet("choice_key", out var ckVal))
                choiceKey = ckVal.AsString(choiceKey);

            var choices = AGISDialogueEdgeSync.FindChoiceEdges(_graph, _def.nodeId);
            var ended   = AGISDialogueEdgeSync.FindEndedEdge(_graph, _def.nodeId);

            string modeText = choices.Count == 0
                ? "Mode: Ended (0 choices)"
                : $"Mode: {choices.Count} choice(s)";

            _dialogueContainer.Add(new Label(modeText));

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop     = 4f;

            var addBtn = new Button(() =>
            {
                AGISDialogueEdgeSync.AddChoice(_graph, _def.nodeId, choiceKey);
                RebuildDialogueSection();
            }) { text = "+ Add Choice" };
            addBtn.AddToClassList("agis-toolbar__button");

            var removeBtn = new Button(() =>
            {
                AGISDialogueEdgeSync.RemoveLastChoice(_graph, _def.nodeId, choiceKey);
                RebuildDialogueSection();
            }) { text = "- Remove Last" };
            removeBtn.AddToClassList("agis-toolbar__button");
            removeBtn.SetEnabled(choices.Count > 0);

            btnRow.Add(addBtn);
            btnRow.Add(removeBtn);
            _dialogueContainer.Add(btnRow);

            // List edges
            if (choices.Count > 0)
            {
                foreach (var (edge, option) in choices)
                {
                    var edgeLabel = new Label($"  Choice {option} → {(edge.toNodeId.IsValid ? (string)edge.toNodeId : "(unconnected)")}");
                    edgeLabel.style.color = new Color(0.7f, 0.9f, 0.7f);
                    _dialogueContainer.Add(edgeLabel);
                }
            }
            else if (ended != null)
            {
                var endedLabel = new Label($"  Ended → {(ended.toNodeId.IsValid ? (string)ended.toNodeId : "(unconnected)")}");
                endedLabel.style.color = new Color(0.7f, 0.7f, 0.9f);
                _dialogueContainer.Add(endedLabel);
            }
        }

        private void RebuildGroupedSection()
        {
            _groupedContainer.Clear();

            if (_def == null || _type == null) return;
            if (_type.Kind != AGISNodeKind.Grouped) return;

            _groupedContainer.Add(MakeSectionHeader("Grouped Node"));

            var assetIdLabel = new Label($"Asset ID: {(string)_def.groupAssetId}");
            assetIdLabel.style.color = new Color(0.7f, 0.9f, 0.7f);
            _groupedContainer.Add(assetIdLabel);

            var openBtn = new Button(() => OnDrillIntoGrouped?.Invoke(_def.nodeId)) { text = "Open Sub-Graph" };
            openBtn.AddToClassList("agis-toolbar__button");
            _groupedContainer.Add(openBtn);

            if (_def.exposedOverrides?.values != null && _def.exposedOverrides.values.Count > 0)
            {
                _groupedContainer.Add(MakeSectionHeader("Exposed Overrides"));
                foreach (var pv in _def.exposedOverrides.values)
                {
                    if (pv == null) continue;
                    var row = new Label($"  {pv.key} = {FormatValue(pv.value)}");
                    row.style.color = new Color(0.8f, 0.8f, 1f);
                    _groupedContainer.Add(row);
                }
            }
        }

        private void RebuildDeleteSection()
        {
            _deleteContainer.Clear();

            if (_def == null || _type == null) return;
            if (_type.Kind == AGISNodeKind.AnyState)
            {
                var disabled = new Label("AnyState nodes cannot be deleted.");
                disabled.style.color = new Color(0.5f, 0.5f, 0.5f);
                _deleteContainer.Add(disabled);
                return;
            }

            // Count edges connected to this node
            int edgeCount = 0;
            if (_graph?.edges != null)
            {
                foreach (var e in _graph.edges)
                {
                    if (e == null) continue;
                    if (e.fromNodeId == _def.nodeId || e.toNodeId == _def.nodeId)
                        edgeCount++;
                }
            }

            bool confirmed = false;
            Button deleteBtn = null;

            string btnText = edgeCount > 0
                ? $"Delete Node ({edgeCount} edges will be removed)"
                : "Delete Node";

            deleteBtn = new Button(() =>
            {
                if (edgeCount > 0 && !confirmed)
                {
                    confirmed = true;
                    deleteBtn.text = "Confirm Delete (irreversible)";
                    deleteBtn.style.backgroundColor = new StyleColor(new Color(0.8f, 0.1f, 0.1f));
                    return;
                }

                var cmd = new RemoveNodeCommand(_graph, _def.nodeId);
                _history?.Push(cmd);
                OnDeleteNode?.Invoke(_def.nodeId);
            });
            deleteBtn.text = btnText;
            deleteBtn.AddToClassList("agis-toolbar__button");
            deleteBtn.style.backgroundColor = new StyleColor(new Color(0.6f, 0.1f, 0.1f));
            _deleteContainer.Add(deleteBtn);
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnSetEntryClicked()
        {
            if (_def == null || _graph == null || _history == null) return;
            var cmd = new SetEntryNodeCommand(_graph, _graph.entryNodeId, _def.nodeId);
            _history.Push(cmd);
            OnSetEntryNode?.Invoke(_def.nodeId);
            _setEntryBtn.SetEnabled(false);
            _setEntryBtn.text = "\u2605 This is the Entry Node";
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Color KindColor(AGISNodeKind kind)
        {
            switch (kind)
            {
                case AGISNodeKind.Normal:   return new Color(0.23f, 0.48f, 0.84f);
                case AGISNodeKind.Grouped:  return new Color(0.10f, 0.55f, 0.48f);
                case AGISNodeKind.Parallel: return new Color(0.42f, 0.25f, 0.63f);
                case AGISNodeKind.AnyState: return new Color(0.55f, 0.10f, 0.10f);
                default: return Color.white;
            }
        }

        private static string FormatValue(AGISValue v)
        {
            switch (v.Type)
            {
                case AGISParamType.Bool:    return v.AsBool().ToString();
                case AGISParamType.Int:     return v.AsInt().ToString();
                case AGISParamType.Float:   return v.AsFloat().ToString("F3");
                case AGISParamType.String:  return $"\"{v.AsString()}\"";
                case AGISParamType.Vector2: return v.AsVector2().ToString();
                case AGISParamType.Vector3: return v.AsVector3().ToString();
                case AGISParamType.Guid:    return (string)v.AsGuid();
                default: return v.Type.ToString();
            }
        }

        private static VisualElement MakeSectionHeader(string title)
        {
            var header = new Label(title);
            header.AddToClassList("agis-panel-section__header");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop    = 8f;
            header.style.marginBottom = 2f;
            return header;
        }
    }
}
