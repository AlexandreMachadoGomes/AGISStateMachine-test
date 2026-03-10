// File: AGISGroupedAssetPanel.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/Panels/
// Purpose: Grouped asset inspector tab (Tab D).
//          Shows sub-graph info, exposed param bindings, and a drill-in button.

using System;
using UnityEngine;
using UnityEngine.UIElements;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.Runtime;

namespace AGIS.ESM.RuntimeEditor.Panels
{
    /// <summary>
    /// Placeholder Grouped Asset inspector panel.
    /// Populated when the user selects a Grouped node and navigates to this tab,
    /// or opens via OnDrillIntoGrouped from the Node inspector.
    /// </summary>
    public sealed class AGISGroupedAssetPanel : VisualElement
    {
        // ── Events ────────────────────────────────────────────────────────────
        public event Action<AGISGuid> OnDrillIn;

        // ── State ─────────────────────────────────────────────────────────────
        private AGISNodeInstanceDef   _def;
        private AGISStateMachineGraph _graph;

        // ── UI refs ───────────────────────────────────────────────────────────
        private readonly Label        _statusLabel;
        private readonly Label        _assetIdLabel;
        private readonly VisualElement _overridesContainer;
        private readonly Button       _drillInBtn;

        // ─────────────────────────────────────────────────────────────────────
        public AGISGroupedAssetPanel()
        {
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

            inner.Add(MakeSectionHeader("Grouped Asset Inspector"));

            _statusLabel = new Label("Select a Grouped node in the canvas to inspect it here.");
            _statusLabel.style.color      = new Color(0.6f, 0.6f, 0.6f);
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            inner.Add(_statusLabel);

            inner.Add(MakeSectionHeader("Asset ID"));
            _assetIdLabel = new Label("—");
            _assetIdLabel.style.color = new Color(0.7f, 0.9f, 0.7f);
            inner.Add(_assetIdLabel);

            inner.Add(MakeSectionHeader("Exposed Overrides"));
            _overridesContainer = new VisualElement();
            _overridesContainer.style.flexDirection = FlexDirection.Column;
            inner.Add(_overridesContainer);

            inner.Add(MakeSectionHeader("Actions"));
            _drillInBtn = new Button(OnDrillInClicked) { text = "Open Sub-Graph" };
            _drillInBtn.AddToClassList("agis-toolbar__button");
            _drillInBtn.SetEnabled(false);
            inner.Add(_drillInBtn);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetGroupedNode(AGISNodeInstanceDef def, AGISStateMachineGraph graph)
        {
            _def   = def;
            _graph = graph;
            Rebuild();
        }

        // ── Rebuild ───────────────────────────────────────────────────────────

        private void Rebuild()
        {
            _overridesContainer.Clear();

            if (_def == null)
            {
                _statusLabel.text  = "Select a Grouped node in the canvas to inspect it here.";
                _assetIdLabel.text = "—";
                _drillInBtn.SetEnabled(false);
                return;
            }

            _statusLabel.text  = $"Grouped node: {_def.nodeTypeId}";
            _assetIdLabel.text = _def.groupAssetId.IsValid
                ? (string)_def.groupAssetId
                : "(no asset assigned)";

            _drillInBtn.SetEnabled(_def.nodeId.IsValid);

            // Exposed overrides
            if (_def.exposedOverrides?.values != null && _def.exposedOverrides.values.Count > 0)
            {
                foreach (var pv in _def.exposedOverrides.values)
                {
                    if (pv == null) continue;
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.marginBottom  = 2f;

                    var key = new Label(pv.key);
                    key.style.flexGrow = 1;
                    key.style.color    = new Color(0.8f, 0.8f, 1f);

                    var val = new Label($"= {FormatValue(pv.value)}");
                    val.style.color = new Color(0.7f, 0.9f, 0.7f);

                    row.Add(key);
                    row.Add(val);
                    _overridesContainer.Add(row);
                }
            }
            else
            {
                _overridesContainer.Add(new Label("(no overrides)") { style = { color = new Color(0.5f, 0.5f, 0.5f) } });
            }
        }

        // ── Handlers ─────────────────────────────────────────────────────────

        private void OnDrillInClicked()
        {
            if (_def != null)
                OnDrillIn?.Invoke(_def.nodeId);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

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
