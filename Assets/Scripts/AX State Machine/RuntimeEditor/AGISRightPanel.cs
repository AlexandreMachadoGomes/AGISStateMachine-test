// File: AGISRightPanel.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/
// Purpose: Wrapper around the standalone Panels/* implementations.
//          Provides a 5-tab bar (Node | Edge | Graph | Grouped | Blackboard)
//          with a collapse sliver and public API methods called by AGISGraphEditorWindow.

using System;
using UnityEngine;
using UnityEngine.UIElements;
using AGIS.ESM.UGC;
using AGIS.ESM.Runtime;
using AGIS.ESM.RuntimeEditor.Panels;

namespace AGIS.ESM.RuntimeEditor
{
    public sealed class AGISRightPanel : VisualElement
    {
        // ── Tab constants ─────────────────────────────────────────────────────
        private static readonly string[] TabNames = { "Node", "Edge", "Graph", "Grouped", "Blackboard" };
        private const int TAB_NODE       = 0;
        private const int TAB_EDGE       = 1;
        private const int TAB_GRAPH      = 2;
        private const int TAB_GROUPED    = 3;
        private const int TAB_BLACKBOARD = 4;

        // ── Stored dependencies ───────────────────────────────────────────────
        private readonly AGISEditorHistory _history;
        private AGISStateMachineRunner     _runner;
        private int                        _slotIndex;

        // ── Child panels ──────────────────────────────────────────────────────
        private readonly AGISNodeInspectorPanel   _nodeTab;
        private readonly AGISEdgeInspectorPanel   _edgeTab;
        private readonly AGISGraphPropertiesPanel  _graphTab;
        private readonly AGISGroupedAssetPanel    _groupedTab;
        private readonly AGISBlackboardPanel      _blackboardTab;

        // ── Layout elements ───────────────────────────────────────────────────
        private readonly Button        _collapseBtn;
        private readonly VisualElement _panelBody;
        private readonly VisualElement _tabBar;
        private readonly VisualElement _contentArea;
        private readonly Button[]      _tabButtons;
        private int  _activeTabIndex = TAB_GRAPH;
        private bool _collapsed = false;

        // ── Public events ─────────────────────────────────────────────────────
        public event Action<AGISGuid> OnNodeSelectRequested;
        public event Action OnGraphSaveRequested;
        public event Action OnGraphRevertRequested;

        // ─────────────────────────────────────────────────────────────────────
        public AGISRightPanel(
            AGISEditorHistory history,
            AGISNodeTypeRegistry nodeTypes,
            AGISConditionTypeRegistry condTypes)
        {
            _history = history;

            AddToClassList("agis-right-panel");
            style.flexDirection = FlexDirection.Row;

            // ── Collapse sliver (20 px) ───────────────────────────────────────
            _collapseBtn = new Button(ToggleCollapse);
            _collapseBtn.AddToClassList("agis-right-panel__collapse-btn");
            _collapseBtn.style.width     = 20;
            _collapseBtn.style.flexShrink = 0;
            _collapseBtn.style.alignSelf  = Align.Stretch;
            _collapseBtn.text = "\u25C4"; // ◄
            Add(_collapseBtn);

            // ── Panel body ────────────────────────────────────────────────────
            _panelBody = new VisualElement();
            _panelBody.AddToClassList("agis-right-panel__body");
            _panelBody.style.flexDirection = FlexDirection.Column;
            _panelBody.style.flexGrow = 1;
            Add(_panelBody);

            // Tab bar
            _tabBar = new VisualElement();
            _tabBar.AddToClassList("agis-panel-tab-bar");
            _tabBar.style.flexDirection = FlexDirection.Row;
            _panelBody.Add(_tabBar);

            _tabButtons = new Button[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                int captured = i;
                var btn = new Button(() => SwitchTab(captured));
                btn.text = TabNames[i];
                btn.AddToClassList("agis-panel-tab");
                _tabBar.Add(btn);
                _tabButtons[i] = btn;
            }

            // Content area
            _contentArea = new VisualElement();
            _contentArea.AddToClassList("agis-right-panel__content");
            _contentArea.style.flexGrow = 1;
            _panelBody.Add(_contentArea);

            // ── Instantiate panels ────────────────────────────────────────────
            _nodeTab       = new AGISNodeInspectorPanel(history, nodeTypes, condTypes);
            _edgeTab       = new AGISEdgeInspectorPanel(condTypes);
            _graphTab      = new AGISGraphPropertiesPanel();
            _groupedTab    = new AGISGroupedAssetPanel();
            _blackboardTab = new AGISBlackboardPanel();

            // Wire _graphTab.OnGoToNode → OnNodeSelectRequested
            _graphTab.OnGoToNode += id => OnNodeSelectRequested?.Invoke(id);

            // Show the default tab (Graph)
            SwitchTab(_activeTabIndex);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tab switching
        // ─────────────────────────────────────────────────────────────────────

        private void SwitchTab(int index)
        {
            if (index < 0 || index >= TabNames.Length) return;

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (i == index)
                    _tabButtons[i].AddToClassList("agis-panel-tab--active");
                else
                    _tabButtons[i].RemoveFromClassList("agis-panel-tab--active");
            }

            _contentArea.Clear();

            switch (index)
            {
                case TAB_NODE:       _contentArea.Add(_nodeTab);       break;
                case TAB_EDGE:       _contentArea.Add(_edgeTab);       break;
                case TAB_GRAPH:      _contentArea.Add(_graphTab);      break;
                case TAB_GROUPED:    _contentArea.Add(_groupedTab);    break;
                case TAB_BLACKBOARD: _contentArea.Add(_blackboardTab); break;
            }

            _activeTabIndex = index;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Collapse toggle
        // ─────────────────────────────────────────────────────────────────────

        private void ToggleCollapse()
        {
            _collapsed = !_collapsed;
            _panelBody.style.display = _collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _collapseBtn.text = _collapsed ? "\u25BA" : "\u25C4"; // ► / ◄
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Populate the node tab and switch to it.</summary>
        public void ShowNodeTab(
            AGISNodeInstanceDef def,
            IAGISNodeType type,
            AGISStateMachineGraph graph)
        {
            _nodeTab.Populate(def, type, graph, null);
            SwitchTab(TAB_NODE);
        }

        /// <summary>Populate the node tab with a validation report and switch to it.</summary>
        public void ShowNodeTabWithReport(
            AGISNodeInstanceDef def,
            IAGISNodeType type,
            AGISStateMachineGraph graph,
            AGISGraphValidationReport report)
        {
            _nodeTab.Populate(def, type, graph, report);
            SwitchTab(TAB_NODE);
        }

        /// <summary>Populate the edge tab and switch to it.</summary>
        public void ShowEdgeTab(
            AGISTransitionEdgeDef edge,
            AGISStateMachineGraph graph)
        {
            _edgeTab.Populate(edge, graph, _history, null);
            SwitchTab(TAB_EDGE);
        }

        /// <summary>Populate and show the graph properties tab.</summary>
        public void ShowGraphTab(AGISStateMachineGraph graph)
        {
            _graphTab.SetGraph(graph, _runner, _slotIndex);
            SwitchTab(TAB_GRAPH);
        }

        /// <summary>
        /// Wire the graph tab with runner context and save/revert callbacks.
        /// Call once after the runner is known (slot loading).
        /// </summary>
        public void SetGraph(
            AGISStateMachineGraph graph,
            AGISStateMachineRunner runner,
            int slotIndex,
            Action onSave,
            Action onRevert)
        {
            _runner    = runner;
            _slotIndex = slotIndex;

            _graphTab.OnSaveRequested   = onSave;
            _graphTab.OnRevertRequested = onRevert;
            _graphTab.SetGraph(graph, runner, slotIndex);
        }

        /// <summary>Push a validation report to the graph properties tab.</summary>
        public void SetValidationResults(AGISGraphValidationReport report)
        {
            _graphTab.SetValidationReport(report);
        }

        /// <summary>Refresh the blackboard tab with a live actor state.</summary>
        public void RefreshBlackboard(AGISActorState actorState)
        {
            _blackboardTab.SetActorState(actorState);
        }
    }
}
