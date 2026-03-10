// File: AGISEdgeInspectorPanel.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/Panels/
// Purpose: Edge inspector tab — transition settings, condition tree, summary, delete.

using System;
using UnityEngine;
using UnityEngine.UIElements;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.Runtime;

namespace AGIS.ESM.RuntimeEditor.Panels
{
    public sealed class AGISEdgeInspectorPanel : VisualElement
    {
        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly AGISConditionTypeRegistry _condTypes;

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<AGISTransitionEdgeDef> OnEdgeConditionChanged;
        public event Action<AGISGuid>              OnDeleteEdge;

        // ── State ─────────────────────────────────────────────────────────────
        private AGISTransitionEdgeDef    _edge;
        private AGISStateMachineGraph    _graph;
        private AGISEditorHistory        _history;
        private AGISGraphValidationReport _report;

        // ── UI refs ───────────────────────────────────────────────────────────
        private readonly Label              _headerLabel;
        private readonly Label              _fromToLabel;
        private readonly Label              _guidLabel;
        private readonly IntegerField       _priorityField;
        private readonly Toggle             _interruptibleToggle;
        private readonly FloatField         _cooldownField;
        private readonly TextField          _scopeIdField;
        private readonly AGISConditionTreeView _conditionTreeView;
        private readonly Label              _conditionSummaryLabel;
        private readonly VisualElement      _validationContainer;

        // ─────────────────────────────────────────────────────────────────────
        public AGISEdgeInspectorPanel(AGISConditionTypeRegistry condTypes)
        {
            _condTypes = condTypes ?? throw new ArgumentNullException(nameof(condTypes));

            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            Add(scroll);

            var inner = new VisualElement();
            inner.style.flexDirection = FlexDirection.Column;
            inner.style.paddingLeft = inner.style.paddingRight = 8f;
            inner.style.paddingTop  = inner.style.paddingBottom = 6f;
            scroll.Add(inner);

            // ── Header ────────────────────────────────────────────────────────
            inner.Add(MakeSectionHeader("Transition"));

            _headerLabel = new Label("Transition");
            _headerLabel.style.fontSize = 13;
            _headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            inner.Add(_headerLabel);

            _fromToLabel = new Label("From: — → —");
            _fromToLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            inner.Add(_fromToLabel);

            var guidRow = new VisualElement();
            guidRow.style.flexDirection = FlexDirection.Row;
            guidRow.style.marginTop = 2f;

            _guidLabel = new Label("—");
            _guidLabel.style.flexGrow = 1;
            _guidLabel.style.color = new Color(0.5f, 0.5f, 0.5f);

            var copyGuidBtn = new Button(() =>
            {
                if (_edge != null) GUIUtility.systemCopyBuffer = (string)_edge.edgeId;
            }) { text = "Copy" };
            copyGuidBtn.AddToClassList("agis-toolbar__button");

            guidRow.Add(_guidLabel);
            guidRow.Add(copyGuidBtn);
            inner.Add(guidRow);

            // ── Settings ──────────────────────────────────────────────────────
            inner.Add(MakeSectionHeader("Settings"));

            _priorityField = new IntegerField("Priority");
            _priorityField.RegisterValueChangedCallback(OnPriorityChanged);
            inner.Add(_priorityField);

            _interruptibleToggle = new Toggle("Interruptible");
            _interruptibleToggle.RegisterValueChangedCallback(OnPolicyChanged);
            inner.Add(_interruptibleToggle);

            _cooldownField = new FloatField("Cooldown (s)");
            _cooldownField.RegisterValueChangedCallback(OnCooldownChanged);
            inner.Add(_cooldownField);

            _scopeIdField = new TextField("Scope ID");
            _scopeIdField.RegisterValueChangedCallback(OnScopeIdChanged);
            inner.Add(_scopeIdField);

            // ── Root type selectors ────────────────────────────────────────────
            inner.Add(MakeSectionHeader("Condition Root"));

            var rootBtnRow = new VisualElement();
            rootBtnRow.style.flexDirection = FlexDirection.Row;
            rootBtnRow.style.marginBottom  = 4f;
            rootBtnRow.Add(MakeRootTypeBtn("True",  () => ReplaceConditionRoot(AGISConditionExprDef.True())));
            rootBtnRow.Add(MakeRootTypeBtn("False", () => ReplaceConditionRoot(AGISConditionExprDef.False())));
            rootBtnRow.Add(MakeRootTypeBtn("AND",   () => ReplaceConditionRoot(AGISConditionExprDef.And())));
            rootBtnRow.Add(MakeRootTypeBtn("OR",    () => ReplaceConditionRoot(AGISConditionExprDef.Or())));
            rootBtnRow.Add(MakeRootTypeBtn("LEAF",  () => ReplaceConditionRoot(MakeNewLeaf())));
            inner.Add(rootBtnRow);

            // ── Condition Tree ─────────────────────────────────────────────────
            inner.Add(MakeSectionHeader("Condition Tree"));

            _conditionTreeView = new AGISConditionTreeView(_condTypes, OnConditionChanged);
            inner.Add(_conditionTreeView);

            // ── Condition Summary ─────────────────────────────────────────────
            inner.Add(MakeSectionHeader("Condition Summary"));
            _conditionSummaryLabel = new Label("—");
            _conditionSummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            _conditionSummaryLabel.style.color = new Color(0.8f, 0.9f, 1f);
            inner.Add(_conditionSummaryLabel);

            // ── Validation ────────────────────────────────────────────────────
            inner.Add(MakeSectionHeader("Validation Issues"));
            _validationContainer = new VisualElement();
            _validationContainer.style.flexDirection = FlexDirection.Column;
            inner.Add(_validationContainer);

            // ── Delete ────────────────────────────────────────────────────────
            inner.Add(MakeSectionHeader("Danger Zone"));

            var deleteBtn = new Button(OnDeleteClicked) { text = "Delete Edge" };
            deleteBtn.AddToClassList("agis-toolbar__button");
            deleteBtn.style.backgroundColor = new StyleColor(new Color(0.6f, 0.1f, 0.1f));
            inner.Add(deleteBtn);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Populate(
            AGISTransitionEdgeDef edge,
            AGISStateMachineGraph graph,
            AGISEditorHistory history,
            AGISGraphValidationReport report)
        {
            _edge    = edge;
            _graph   = graph;
            _history = history;
            _report  = report;
            Rebuild();
        }

        // ── Rebuild ───────────────────────────────────────────────────────────

        private void Rebuild()
        {
            if (_edge == null)
            {
                _headerLabel.text  = "No edge selected";
                _fromToLabel.text  = "";
                _guidLabel.text    = "";
                _conditionTreeView.SetExpression(null);
                _conditionSummaryLabel.text = "—";
                return;
            }

            _headerLabel.text = "Transition";
            _guidLabel.text   = (string)_edge.edgeId;

            // From / to node names
            string fromName = ResolveNodeName(_edge.fromNodeId);
            string toName   = ResolveNodeName(_edge.toNodeId);
            _fromToLabel.text = $"From: {fromName}  \u2192  {toName}";

            // Settings — suppress callbacks during population
            _priorityField.UnregisterValueChangedCallback(OnPriorityChanged);
            _priorityField.value = _edge.priority;
            _priorityField.RegisterValueChangedCallback(OnPriorityChanged);

            _interruptibleToggle.UnregisterValueChangedCallback(OnPolicyChanged);
            _interruptibleToggle.value = _edge.policy?.interruptible ?? true;
            _interruptibleToggle.RegisterValueChangedCallback(OnPolicyChanged);

            _cooldownField.UnregisterValueChangedCallback(OnCooldownChanged);
            _cooldownField.value = _edge.policy?.cooldownSeconds ?? 0f;
            _cooldownField.RegisterValueChangedCallback(OnCooldownChanged);

            _scopeIdField.UnregisterValueChangedCallback(OnScopeIdChanged);
            _scopeIdField.value = _edge.scopeId ?? "Any";
            _scopeIdField.RegisterValueChangedCallback(OnScopeIdChanged);

            // Condition tree
            _conditionTreeView.SetExpression(_edge.condition);

            // Summary
            UpdateSummary();

            // Validation
            RebuildValidation();
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnPriorityChanged(ChangeEvent<int> evt)
        {
            if (_edge == null || _graph == null) return;
            var cmd = new ChangeEdgePriorityCommand(_graph, _edge.edgeId, _edge.priority, evt.newValue);
            _history?.Push(cmd);
        }

        private void OnPolicyChanged(ChangeEvent<bool> evt)
        {
            if (_edge == null || _graph == null) return;
            var oldPolicy = new AGISTransitionPolicy
            {
                interruptible   = !evt.newValue,
                cooldownSeconds = _edge.policy?.cooldownSeconds ?? 0f
            };
            var newPolicy = new AGISTransitionPolicy
            {
                interruptible   = evt.newValue,
                cooldownSeconds = _edge.policy?.cooldownSeconds ?? 0f
            };
            var cmd = new ChangeEdgePolicyCommand(_graph, _edge.edgeId, oldPolicy, newPolicy);
            _history?.Push(cmd);
        }

        private void OnCooldownChanged(ChangeEvent<float> evt)
        {
            if (_edge == null || _graph == null) return;
            var oldPolicy = new AGISTransitionPolicy
            {
                interruptible   = _edge.policy?.interruptible ?? true,
                cooldownSeconds = _edge.policy?.cooldownSeconds ?? 0f
            };
            var newPolicy = new AGISTransitionPolicy
            {
                interruptible   = _edge.policy?.interruptible ?? true,
                cooldownSeconds = evt.newValue
            };
            var cmd = new ChangeEdgePolicyCommand(_graph, _edge.edgeId, oldPolicy, newPolicy);
            _history?.Push(cmd);
        }

        private void OnScopeIdChanged(ChangeEvent<string> evt)
        {
            if (_edge == null) return;
            _edge.scopeId = evt.newValue;
        }

        private void OnConditionChanged(AGISConditionExprDef newExpr)
        {
            if (_edge == null || _graph == null) return;
            var old = _edge.condition;
            var cmd = new ChangeEdgeConditionCommand(_graph, _edge.edgeId, old, newExpr);
            _history?.Push(cmd);
            UpdateSummary();
            OnEdgeConditionChanged?.Invoke(_edge);
        }

        private void OnDeleteClicked()
        {
            if (_edge == null || _graph == null) return;
            var id = _edge.edgeId;
            var cmd = new RemoveEdgeCommand(_graph, id);
            _history?.Push(cmd);
            OnDeleteEdge?.Invoke(id);
        }

        private void ReplaceConditionRoot(AGISConditionExprDef newRoot)
        {
            if (_edge == null || _graph == null) return;
            var old = _edge.condition;
            var cmd = new ChangeEdgeConditionCommand(_graph, _edge.edgeId, old, newRoot);
            _history?.Push(cmd);
            _edge.condition = newRoot;
            _conditionTreeView.SetExpression(newRoot);
            UpdateSummary();
            OnEdgeConditionChanged?.Invoke(_edge);
        }

        private void UpdateSummary()
        {
            if (_edge == null)
            {
                _conditionSummaryLabel.text = "—";
                return;
            }
            _conditionSummaryLabel.text = AGISConditionSummary.Summarize(_edge.condition, _condTypes);
        }

        private void RebuildValidation()
        {
            _validationContainer.Clear();

            if (_report == null || _edge == null)
            {
                _validationContainer.Add(new Label("(no report)") { style = { color = new Color(0.5f, 0.5f, 0.5f) } });
                return;
            }

            bool any = false;
            foreach (var issue in _report.Issues)
            {
                if (issue.EdgeId != _edge.edgeId) continue;
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
                msg.style.flexGrow   = 1;
                msg.style.whiteSpace = WhiteSpace.Normal;

                row.Add(icon);
                row.Add(msg);
                _validationContainer.Add(row);
            }

            if (!any)
            {
                var ok = new Label("\u2713 No issues for this edge");
                ok.style.color = new Color(0.2f, 0.8f, 0.2f);
                _validationContainer.Add(ok);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string ResolveNodeName(AGISGuid id)
        {
            if (!id.IsValid) return "(unconnected)";
            if (_graph?.nodes != null)
            {
                foreach (var n in _graph.nodes)
                    if (n != null && n.nodeId == id)
                        return n.nodeTypeId ?? (string)id;
            }
            return (string)id;
        }

        private static AGISConditionExprDef MakeNewLeaf()
        {
            return AGISConditionExprDef.Leaf(new AGISConditionInstanceDef
            {
                conditionId     = AGISGuid.New(),
                conditionTypeId = "",
                @params         = new AGISParamTable(),
            });
        }

        private Button MakeRootTypeBtn(string label, Action onClick)
        {
            var btn = new Button(onClick) { text = label };
            btn.AddToClassList("agis-toolbar__button");
            btn.style.marginRight = 2f;
            return btn;
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
