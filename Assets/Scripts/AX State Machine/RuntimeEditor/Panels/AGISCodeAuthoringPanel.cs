// File: AGISCodeAuthoringPanel.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/Panels/
// Purpose: UIToolkit panel for the "Author" tab in the right panel.
//          Lets designers and AI systems define new state and condition types
//          by filling in display name, type ID, schema params, and code bodies,
//          then clicking Compile.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.Authoring;

namespace AGIS.ESM.RuntimeEditor.Panels
{
    public sealed class AGISCodeAuthoringPanel : VisualElement
    {
        // ── Dependencies ──────────────────────────────────────────────────────
        private AGISNodeTypeRegistry      _nodeRegistry;
        private AGISConditionTypeRegistry _condRegistry;
        private AGISAuthoredTypeStore     _store;

        // ── Kind selector ─────────────────────────────────────────────────────
        private readonly DropdownField _kindDropdown;

        // ── Identity ──────────────────────────────────────────────────────────
        private readonly TextField _displayNameField;
        private readonly TextField _typeIdField;

        // ── Schema params list ────────────────────────────────────────────────
        private readonly VisualElement _schemaList;
        private readonly List<SchemaParamRow> _schemaRows = new List<SchemaParamRow>();

        // ── Code bodies ───────────────────────────────────────────────────────
        private readonly TextField _privateFieldsArea;
        private readonly TextField _ctorBodyArea;
        private readonly TextField _enterBodyArea;
        private readonly TextField _tickBodyArea;
        private readonly TextField _exitBodyArea;
        private readonly TextField _evaluateBodyArea;
        private readonly TextField _helperMethodsArea;

        // ── Node-only container (hidden for conditions) ────────────────────────
        private readonly VisualElement _nodeBodySection;

        // ── Condition-only container (hidden for nodes) ────────────────────────
        private readonly VisualElement _condBodySection;

        // ── Output ────────────────────────────────────────────────────────────
        private readonly Label         _outputLabel;
        private readonly Button        _compileBtn;
        private readonly Button        _saveBtn;

        // ── State ─────────────────────────────────────────────────────────────
        private AGISDefineTypeResult _lastResult;

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<string> OnTypeRegistered; // typeId

        // ── Constructor ───────────────────────────────────────────────────────

        public AGISCodeAuthoringPanel()
        {
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            Add(scroll);

            var inner = new VisualElement();
            inner.style.paddingLeft = inner.style.paddingRight = 8f;
            inner.style.paddingTop  = inner.style.paddingBottom = 6f;
            inner.style.flexDirection = FlexDirection.Column;
            scroll.Add(inner);

            // ── Header ────────────────────────────────────────────────────────
            inner.Add(MakeHeader("Type Authoring"));

            // Kind selector
            _kindDropdown = new DropdownField("Kind", new List<string> { "Node Type", "Condition Type" }, 0);
            _kindDropdown.RegisterValueChangedCallback(_ => UpdateBodySectionVisibility());
            inner.Add(_kindDropdown);

            // Identity
            inner.Add(MakeSectionHeader("Identity"));
            _displayNameField = new TextField("Display Name") { value = "My Custom State" };
            _typeIdField      = new TextField("Type ID")      { value = "custom.my_state" };
            inner.Add(_displayNameField);
            inner.Add(_typeIdField);

            // Schema params
            inner.Add(MakeSectionHeader("Schema Params"));
            _schemaList = new VisualElement();
            _schemaList.style.flexDirection = FlexDirection.Column;
            inner.Add(_schemaList);

            var addParamBtn = new Button(() => AddSchemaRow()) { text = "+ Add Param" };
            addParamBtn.AddToClassList("agis-toolbar__button");
            addParamBtn.style.marginTop = 4f;
            inner.Add(addParamBtn);

            // ── Node body section ─────────────────────────────────────────────
            _nodeBodySection = new VisualElement();
            _nodeBodySection.style.flexDirection = FlexDirection.Column;

            _nodeBodySection.Add(MakeSectionHeader("Private Fields"));
            _privateFieldsArea = MakeCodeArea("private float _timer;\nprivate string _cachedKey;");
            _nodeBodySection.Add(_privateFieldsArea);

            _nodeBodySection.Add(MakeSectionHeader("Constructor Body"));
            _ctorBodyArea = MakeCodeArea("// _timer = p.GetFloat(\"cooldown\", 1f);");
            _nodeBodySection.Add(_ctorBodyArea);

            _nodeBodySection.Add(MakeSectionHeader("Enter()"));
            _enterBodyArea = MakeCodeArea("// Called when this state is activated.");
            _nodeBodySection.Add(_enterBodyArea);

            _nodeBodySection.Add(MakeSectionHeader("Tick(float dt)"));
            _tickBodyArea = MakeCodeArea("// Called every frame while this state is active.");
            _nodeBodySection.Add(_tickBodyArea);

            _nodeBodySection.Add(MakeSectionHeader("Exit()"));
            _exitBodyArea = MakeCodeArea("// Called when leaving this state.");
            _nodeBodySection.Add(_exitBodyArea);

            inner.Add(_nodeBodySection);

            // ── Condition body section ────────────────────────────────────────
            _condBodySection = new VisualElement();
            _condBodySection.style.flexDirection = FlexDirection.Column;

            _condBodySection.Add(MakeSectionHeader("Evaluate() Body"));
            _evaluateBodyArea = MakeCodeArea("// Access args.Ctx.Actor, args.Params, etc.\nreturn false;");
            _condBodySection.Add(_evaluateBodyArea);

            inner.Add(_condBodySection);

            // ── Shared helpers ────────────────────────────────────────────────
            inner.Add(MakeSectionHeader("Helper Methods"));
            _helperMethodsArea = MakeCodeArea("// private bool IsClose(float threshold) { ... }");
            inner.Add(_helperMethodsArea);

            // ── Compile + output ──────────────────────────────────────────────
            inner.Add(MakeSectionHeader("Compile"));

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginBottom = 4f;

            _compileBtn = new Button(OnCompileClicked) { text = "Compile & Register" };
            _compileBtn.AddToClassList("agis-toolbar__button");
            _compileBtn.style.backgroundColor = new StyleColor(new Color(0.15f, 0.4f, 0.15f));
            _compileBtn.style.flexGrow = 1;

            _saveBtn = new Button(OnSaveClicked) { text = "Save to Store" };
            _saveBtn.AddToClassList("agis-toolbar__button");
            _saveBtn.SetEnabled(false);

            btnRow.Add(_compileBtn);
            btnRow.Add(_saveBtn);
            inner.Add(btnRow);

            _outputLabel = new Label("Ready.");
            _outputLabel.style.whiteSpace = WhiteSpace.Normal;
            _outputLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            _outputLabel.style.fontSize = 11;
            _outputLabel.style.marginTop = 4f;
            inner.Add(_outputLabel);

            UpdateBodySectionVisibility();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Configure(
            AGISNodeTypeRegistry      nodeRegistry,
            AGISConditionTypeRegistry condRegistry,
            AGISAuthoredTypeStore     store = null)
        {
            _nodeRegistry = nodeRegistry;
            _condRegistry = condRegistry;
            _store        = store;
        }

        /// <summary>
        /// Populate the panel from an existing record for editing.
        /// </summary>
        public void LoadRecord(AGISAuthoredTypeRecord record)
        {
            if (record == null) return;

            _kindDropdown.index   = record.kind == AGISAuthoredTypeKind.NodeType ? 0 : 1;
            _displayNameField.value = record.displayName ?? "";
            _typeIdField.value      = record.typeId      ?? "";

            // Schema rows
            _schemaRows.Clear();
            _schemaList.Clear();
            if (record.schemaParams != null)
            {
                foreach (var p in record.schemaParams)
                {
                    if (p == null) continue;
                    var row = AddSchemaRow();
                    row.SetValues(p.key, p.type, p.defaultValueLiteral, p.displayName, p.tooltip);
                }
            }

            _privateFieldsArea.value  = record.privateFields  ?? "";
            _ctorBodyArea.value       = record.ctorBody        ?? "";
            _enterBodyArea.value      = record.enterBody       ?? "";
            _tickBodyArea.value       = record.tickBody        ?? "";
            _exitBodyArea.value       = record.exitBody        ?? "";
            _evaluateBodyArea.value   = record.evaluateBody    ?? "";
            _helperMethodsArea.value  = record.helperMethods   ?? "";

            UpdateBodySectionVisibility();
            _outputLabel.text  = $"Loaded: {record.displayName}";
            _outputLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            _saveBtn.SetEnabled(false);
        }

        // ── Compile / Save ────────────────────────────────────────────────────

        private void OnCompileClicked()
        {
            _lastResult = null;
            _saveBtn.SetEnabled(false);

            bool isNode = _kindDropdown.index == 0;

            AGISAIAuthoringService.Configure(_nodeRegistry, _condRegistry, _store);

            if (isNode)
            {
                _lastResult = AGISAIAuthoringService.Instance.DefineNodeType(
                    displayName:   _displayNameField.value,
                    typeId:        _typeIdField.value,
                    schemaParams:  CollectSchemaParams(),
                    privateFields: _privateFieldsArea.value,
                    ctorBody:      _ctorBodyArea.value,
                    enterBody:     _enterBodyArea.value,
                    tickBody:      _tickBodyArea.value,
                    exitBody:      _exitBodyArea.value,
                    helperMethods: _helperMethodsArea.value);
            }
            else
            {
                _lastResult = AGISAIAuthoringService.Instance.DefineConditionType(
                    displayName:   _displayNameField.value,
                    typeId:        _typeIdField.value,
                    schemaParams:  CollectSchemaParams(),
                    evaluateBody:  _evaluateBodyArea.value,
                    helperMethods: _helperMethodsArea.value);
            }

            if (_lastResult.Success)
            {
                _outputLabel.text  = $"\u2713 Registered: {_lastResult.TypeId}";
                _outputLabel.style.color = new Color(0.2f, 0.8f, 0.2f);
                _saveBtn.SetEnabled(_store != null);
                OnTypeRegistered?.Invoke(_lastResult.TypeId);
            }
            else
            {
                _outputLabel.text  = "\u274c " + string.Join("\n\u274c ", _lastResult.Errors);
                _outputLabel.style.color = new Color(0.9f, 0.3f, 0.3f);
            }

            if (_lastResult.Warnings.Length > 0)
                _outputLabel.text += "\n\u26a0\ufe0f " + string.Join("\n\u26a0\ufe0f ", _lastResult.Warnings);
        }

        private void OnSaveClicked()
        {
            if (_store == null || _lastResult == null || !_lastResult.Success)
            {
                _outputLabel.text = "Nothing to save (compile first).";
                return;
            }
            _outputLabel.text  = $"\u2713 Saved to store: {_lastResult.TypeId}";
            _outputLabel.style.color = new Color(0.2f, 0.8f, 0.2f);
        }

        // ── Schema rows ───────────────────────────────────────────────────────

        private SchemaParamRow AddSchemaRow()
        {
            SchemaParamRow row = null;
            row = new SchemaParamRow(() =>
            {
                _schemaRows.Remove(row);
                _schemaList.Remove(row);
            });
            _schemaRows.Add(row);
            _schemaList.Add(row);
            return row;
        }

        private List<AGISSchemaParamDef> CollectSchemaParams()
        {
            var list = new List<AGISSchemaParamDef>(_schemaRows.Count);
            foreach (var row in _schemaRows)
            {
                var def = row.ToDef();
                if (def != null) list.Add(def);
            }
            return list;
        }

        // ── Visibility helpers ────────────────────────────────────────────────

        private void UpdateBodySectionVisibility()
        {
            bool isNode = _kindDropdown.index == 0;
            _nodeBodySection.style.display = isNode ? DisplayStyle.Flex : DisplayStyle.None;
            _condBodySection.style.display = isNode ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        private static VisualElement MakeHeader(string title)
        {
            var label = new Label(title);
            label.style.fontSize = 13;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop    = 4f;
            label.style.marginBottom = 4f;
            return label;
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

        private static TextField MakeCodeArea(string placeholder)
        {
            var field = new TextField { multiline = true, value = placeholder };
            field.style.minHeight = 60f;
            field.style.unityFontDefinition = StyleKeyword.None; // use monospace if USS provides it
            field.style.fontSize = 11;
            field.style.whiteSpace = WhiteSpace.Normal;
            return field;
        }

        // ── SchemaParamRow ────────────────────────────────────────────────────

        private sealed class SchemaParamRow : VisualElement
        {
            private readonly TextField     _keyField;
            private readonly DropdownField _typeDropdown;
            private readonly TextField     _defaultField;
            private readonly TextField     _displayNameField;

            private static readonly List<string> TypeNames = new List<string>
                { "Bool", "Int", "Float", "String" };
            private static readonly AGISParamType[] TypeValues =
                { AGISParamType.Bool, AGISParamType.Int, AGISParamType.Float, AGISParamType.String };

            public SchemaParamRow(Action onRemove)
            {
                style.flexDirection  = FlexDirection.Row;
                style.flexWrap       = Wrap.Wrap;
                style.marginBottom   = 2f;
                style.paddingLeft    = 4f;
                style.borderBottomWidth = 1f;
                style.borderBottomColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));

                _keyField = new TextField { style = { flexGrow = 1, minWidth = 80 } };
                _keyField.tooltip = "Param key";
                _keyField.value   = "param_key";

                _typeDropdown = new DropdownField(TypeNames, 2 /* Float */);
                _typeDropdown.style.minWidth = 60;

                _defaultField = new TextField { style = { flexGrow = 1, minWidth = 50 } };
                _defaultField.tooltip = "Default value literal (e.g. 1.0f, true, \"hello\")";

                _displayNameField = new TextField { style = { flexGrow = 1, minWidth = 60 } };
                _displayNameField.tooltip = "Display name";

                var removeBtn = new Button(onRemove) { text = "\u00d7" };
                removeBtn.AddToClassList("agis-toolbar__button");
                removeBtn.style.color = new StyleColor(new Color(0.9f, 0.3f, 0.3f));

                Add(new Label("Key") { style = { width = 28, alignSelf = Align.Center } });
                Add(_keyField);
                Add(new Label("Type") { style = { width = 30, alignSelf = Align.Center, marginLeft = 4 } });
                Add(_typeDropdown);
                Add(new Label("Default") { style = { width = 44, alignSelf = Align.Center, marginLeft = 4 } });
                Add(_defaultField);
                Add(new Label("Name") { style = { width = 32, alignSelf = Align.Center, marginLeft = 4 } });
                Add(_displayNameField);
                Add(removeBtn);
            }

            public void SetValues(string key, AGISParamType type, string defaultLiteral, string displayName, string tooltip)
            {
                _keyField.value         = key         ?? "";
                _defaultField.value     = defaultLiteral ?? "";
                _displayNameField.value = displayName  ?? "";

                for (int i = 0; i < TypeValues.Length; i++)
                {
                    if (TypeValues[i] == type) { _typeDropdown.index = i; break; }
                }
            }

            public AGISSchemaParamDef ToDef()
            {
                string key = _keyField.value?.Trim();
                if (string.IsNullOrEmpty(key)) return null;

                int ti = Mathf.Clamp(_typeDropdown.index, 0, TypeValues.Length - 1);
                return new AGISSchemaParamDef
                {
                    Key                 = key,
                    Type                = TypeValues[ti],
                    DefaultValueLiteral = _defaultField.value?.Trim(),
                    DisplayName         = _displayNameField.value?.Trim(),
                };
            }
        }
    }
}
