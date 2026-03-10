// File: AGISGraphPropertiesPanel.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/Panels/
// Purpose: Graph properties tab — info, entry node, validation report, save/IO, runner refs.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using AGIS.ESM.UGC;
using AGIS.ESM.Runtime;

namespace AGIS.ESM.RuntimeEditor.Panels
{
    public sealed class AGISGraphPropertiesPanel : VisualElement
    {
        // ── Data ──────────────────────────────────────────────────────────────
        private AGISStateMachineGraph  _graph;
        private AGISStateMachineRunner _runner;
        private int                    _slotIndex;

        // ── Events ────────────────────────────────────────────────────────────
        public Action OnSaveRequested;
        public Action OnRevertRequested;
        public event Action<AGISGuid> OnGoToNode;
        public event Action<AGISGuid> OnGoToEdge;

        // ── UI refs ───────────────────────────────────────────────────────────
        private Label          _graphIdLabel;
        private Label          _versionLabel;
        private Label          _nodeCountLabel;
        private Label          _edgeCountLabel;
        private DropdownField  _entryNodeDropdown;
        private VisualElement  _validationContainer;
        private Label          _autoSaveStatus;
        private Toggle         _autoSaveToggle;
        private TextField      _exportFilename;
        private TextField      _importFilename;
        private Label          _runnerLabel;
        private bool           _autoSave;

        // ─────────────────────────────────────────────────────────────────────
        public AGISGraphPropertiesPanel()
        {
            AddToClassList("agis-panel-scroll");
            style.flexDirection = FlexDirection.Column;
            style.flexGrow = 1;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            Add(scroll);

            var inner = new VisualElement();
            inner.style.flexDirection = FlexDirection.Column;
            inner.style.paddingLeft = inner.style.paddingRight = 8f;
            inner.style.paddingTop  = inner.style.paddingBottom = 6f;
            scroll.Add(inner);

            // ── Section: Info ─────────────────────────────────────────────────
            inner.Add(MakeSectionHeader("Graph Info"));

            var idRow = MakeFieldRow("Graph ID");
            _graphIdLabel = new Label("—");
            _graphIdLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _graphIdLabel.style.flexGrow = 1;
            var copyIdBtn = new Button(() =>
            {
                if (_graph != null)
                    GUIUtility.systemCopyBuffer = ((string)_graph.graphId);
            }) { text = "Copy" };
            copyIdBtn.AddToClassList("agis-toolbar__button");
            idRow.Add(_graphIdLabel);
            idRow.Add(copyIdBtn);
            inner.Add(idRow);

            _versionLabel    = AddLabelRow(inner, "Version",    "—");
            _nodeCountLabel  = AddLabelRow(inner, "Nodes",      "0");
            _edgeCountLabel  = AddLabelRow(inner, "Edges",      "0");

            // ── Section: Entry Node ───────────────────────────────────────────
            inner.Add(MakeSectionHeader("Entry Node"));
            _entryNodeDropdown = new DropdownField("Entry Node", new List<string> { "(none)" }, 0);
            _entryNodeDropdown.RegisterValueChangedCallback(OnEntryNodeDropdownChanged);
            inner.Add(_entryNodeDropdown);

            // ── Section: Validation ───────────────────────────────────────────
            inner.Add(MakeSectionHeader("Validation"));
            _validationContainer = new VisualElement();
            _validationContainer.style.flexDirection = FlexDirection.Column;
            inner.Add(_validationContainer);

            // ── Section: Save / IO ────────────────────────────────────────────
            inner.Add(MakeSectionHeader("Save / IO"));

            var saveRow = new VisualElement();
            saveRow.style.flexDirection = FlexDirection.Row;
            saveRow.style.marginBottom = 4f;

            var saveBtn = new Button(() => OnSaveRequested?.Invoke()) { text = "Save Graph" };
            saveBtn.AddToClassList("agis-toolbar__button");
            saveBtn.AddToClassList("agis-toolbar__button--primary");

            var revertBtn = new Button(() => OnRevertRequested?.Invoke()) { text = "Revert" };
            revertBtn.AddToClassList("agis-toolbar__button");

            saveRow.Add(saveBtn);
            saveRow.Add(revertBtn);
            inner.Add(saveRow);

            _autoSaveToggle = new Toggle("Auto-save");
            _autoSaveToggle.RegisterValueChangedCallback(evt => _autoSave = evt.newValue);
            inner.Add(_autoSaveToggle);

            // Export JSON
            inner.Add(MakeSectionHeader("Export JSON"));
            _exportFilename = new TextField("Filename");
            _exportFilename.value = "graph_export";
            inner.Add(_exportFilename);

            var exportBtn = new Button(DoExport) { text = "Export JSON" };
            exportBtn.AddToClassList("agis-toolbar__button");
            inner.Add(exportBtn);

            // Import JSON
            inner.Add(MakeSectionHeader("Import JSON"));
            _importFilename = new TextField("Filename");
            _importFilename.value = "graph_export";
            inner.Add(_importFilename);

            var importBtn = new Button(DoImport) { text = "Import JSON" };
            importBtn.AddToClassList("agis-toolbar__button");
            inner.Add(importBtn);

            // ── Section: Runner References ────────────────────────────────────
            inner.Add(MakeSectionHeader("Runner References"));
            _runnerLabel = new Label("No runner connected");
            _runnerLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            inner.Add(_runnerLabel);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetGraph(AGISStateMachineGraph graph, AGISStateMachineRunner runner, int slotIndex)
        {
            _graph      = graph;
            _runner     = runner;
            _slotIndex  = slotIndex;

            RefreshAll();
        }

        public void SetValidationReport(AGISGraphValidationReport report)
        {
            RebuildValidationSection(report);
        }

        public void RefreshStats()
        {
            if (_graph == null)
            {
                _nodeCountLabel.text = "0";
                _edgeCountLabel.text = "0";
                return;
            }
            _nodeCountLabel.text = (_graph.nodes?.Count ?? 0).ToString();
            _edgeCountLabel.text = (_graph.edges?.Count ?? 0).ToString();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (_graph == null)
            {
                _graphIdLabel.text  = "—";
                _versionLabel.text  = "—";
                _nodeCountLabel.text = "0";
                _edgeCountLabel.text = "0";
                _entryNodeDropdown.choices = new List<string> { "(none)" };
                _entryNodeDropdown.index   = 0;
                _runnerLabel.text = "No runner connected";
                return;
            }

            _graphIdLabel.text  = (string)_graph.graphId;
            _versionLabel.text  = _graph.version.ToString();
            RefreshStats();

            // Entry node dropdown
            RebuildEntryNodeDropdown();

            // Runner label
            if (_runner != null && _slotIndex >= 0 && _slotIndex < _runner.Slots.Count)
            {
                var slot = _runner.Slots[_slotIndex];
                _runnerLabel.text = $"Runner: {_runner.gameObject.name}  Slot [{_slotIndex}] \"{slot.slotName}\"";
            }
            else
            {
                _runnerLabel.text = "No runner connected";
            }
        }

        private void RebuildEntryNodeDropdown()
        {
            if (_graph == null) return;

            var choices    = new List<string>();
            var nodeIds    = new List<AGISGuid>();

            choices.Add("(none)");
            nodeIds.Add(AGISGuid.Empty);

            int selectedIndex = 0;

            if (_graph.nodes != null)
            {
                for (int i = 0; i < _graph.nodes.Count; i++)
                {
                    var n = _graph.nodes[i];
                    if (n == null) continue;
                    choices.Add($"{n.nodeTypeId}  [{(string)n.nodeId}]");
                    nodeIds.Add(n.nodeId);
                    if (n.nodeId == _graph.entryNodeId)
                        selectedIndex = choices.Count - 1;
                }
            }

            // Temporarily remove callback to avoid feedback loop during rebuild
            _entryNodeDropdown.UnregisterValueChangedCallback(OnEntryNodeDropdownChanged);
            _entryNodeDropdown.choices = choices;
            _entryNodeDropdown.index   = selectedIndex;
            _entryNodeDropdown.userData = nodeIds;
            _entryNodeDropdown.RegisterValueChangedCallback(OnEntryNodeDropdownChanged);
        }

        private void OnEntryNodeDropdownChanged(ChangeEvent<string> evt)
        {
            if (_graph == null) return;
            var nodeIds = _entryNodeDropdown.userData as List<AGISGuid>;
            if (nodeIds == null) return;
            int idx = _entryNodeDropdown.index;
            if (idx < 0 || idx >= nodeIds.Count) return;
            _graph.entryNodeId = nodeIds[idx];
        }

        private void RebuildValidationSection(AGISGraphValidationReport report)
        {
            _validationContainer.Clear();

            if (report == null || report.Issues == null || report.Issues.Count == 0)
            {
                var okLabel = new Label("\u2713 No issues");
                okLabel.style.color = new Color(0.2f, 0.8f, 0.2f);
                _validationContainer.Add(okLabel);
                return;
            }

            foreach (var issue in report.Issues)
            {
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
                row.Add(icon);

                var msg = new Label($"[{issue.Code}] {issue.Message}");
                msg.AddToClassList("agis-validation-row__msg");
                msg.style.flexGrow = 1;
                msg.style.whiteSpace = WhiteSpace.Normal;
                row.Add(msg);

                if (issue.NodeId.IsValid)
                {
                    var capturedId = issue.NodeId;
                    var gotoBtn = new Button(() => OnGoToNode?.Invoke(capturedId)) { text = "Go To" };
                    gotoBtn.AddToClassList("agis-validation-row__goto");
                    row.Add(gotoBtn);
                }
                else if (issue.EdgeId.IsValid)
                {
                    var capturedId = issue.EdgeId;
                    var gotoBtn = new Button(() => OnGoToEdge?.Invoke(capturedId)) { text = "Go To" };
                    gotoBtn.AddToClassList("agis-validation-row__goto");
                    row.Add(gotoBtn);
                }

                _validationContainer.Add(row);
            }
        }

#if !UNITY_WEBGL
        private void DoExport()
        {
            if (_graph == null)
            {
                Debug.LogWarning("[AGIS Editor] No graph to export.");
                return;
            }

            try
            {
                var json = AGISGraphSerializer.ToJson(_graph);
                var filename = string.IsNullOrWhiteSpace(_exportFilename.value)
                    ? "graph_export"
                    : _exportFilename.value.Trim();
                var path = System.IO.Path.Combine(Application.persistentDataPath, filename + ".json");
                System.IO.File.WriteAllText(path, json);
                Debug.Log($"[AGIS Editor] Exported graph to: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AGIS Editor] Export failed: {ex.Message}");
            }
        }

        private void DoImport()
        {
            try
            {
                var filename = string.IsNullOrWhiteSpace(_importFilename.value)
                    ? "graph_export"
                    : _importFilename.value.Trim();
                var path = System.IO.Path.Combine(Application.persistentDataPath, filename + ".json");

                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning($"[AGIS Editor] Import file not found: {path}");
                    return;
                }

                var json  = System.IO.File.ReadAllText(path);
                var asset = AGISGraphSerializer.GraphFromJson(json);
                if (asset == null)
                {
                    Debug.LogWarning("[AGIS Editor] Import produced a null graph asset.");
                    return;
                }
                _graph = asset.graph;
                RefreshAll();
                Debug.Log($"[AGIS Editor] Imported graph from: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AGIS Editor] Import failed: {ex.Message}");
            }
        }
#else
        private void DoExport()
        {
            Debug.LogWarning("[AGIS Editor] File IO not supported on WebGL.");
        }

        private void DoImport()
        {
            Debug.LogWarning("[AGIS Editor] File IO not supported on WebGL.");
        }
#endif

        // ── Layout helpers ────────────────────────────────────────────────────

        private static VisualElement MakeSectionHeader(string title)
        {
            var header = new Label(title);
            header.AddToClassList("agis-panel-section__header");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop  = 8f;
            header.style.marginBottom = 2f;
            return header;
        }

        private static VisualElement MakeFieldRow(string labelText)
        {
            var row = new VisualElement();
            row.AddToClassList("agis-field-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom  = 2f;
            var lbl = new Label(labelText);
            lbl.AddToClassList("agis-field-row__label");
            lbl.style.width = 80f;
            lbl.style.flexShrink = 0;
            row.Add(lbl);
            return row;
        }

        private static Label AddLabelRow(VisualElement parent, string fieldLabel, string initial)
        {
            var row = MakeFieldRow(fieldLabel);
            var val = new Label(initial);
            val.style.flexGrow = 1;
            row.Add(val);
            parent.Add(row);
            return val;
        }
    }
}
