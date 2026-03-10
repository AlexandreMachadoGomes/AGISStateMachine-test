// File: AGISBlackboardPanel.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/Panels/
// Purpose: Live viewer/editor for AGISActorState. Refreshes every 250ms.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.Runtime;

namespace AGIS.ESM.RuntimeEditor.Panels
{
    public sealed class AGISBlackboardPanel : VisualElement
    {
        // ── State ─────────────────────────────────────────────────────────────
        private AGISActorState _actorState;
        private readonly Dictionary<string, AGISValue> _lastValues = new Dictionary<string, AGISValue>();

        // ── UI refs ───────────────────────────────────────────────────────────
        private readonly Label        _statusLabel;
        private readonly TextField    _filterField;
        private readonly VisualElement _rowContainer;
        private string _filterText = "";

        // ─────────────────────────────────────────────────────────────────────
        public AGISBlackboardPanel()
        {
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            // Status row
            _statusLabel = new Label("No runner connected");
            _statusLabel.AddToClassList("agis-status-bar__text");
            _statusLabel.style.marginLeft   = 6f;
            _statusLabel.style.marginTop    = 4f;
            _statusLabel.style.marginBottom = 4f;
            _statusLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            Add(_statusLabel);

            // Filter field
            _filterField = new TextField();
            _filterField.style.marginLeft  = 4f;
            _filterField.style.marginRight = 4f;
            _filterField.style.marginBottom = 4f;
            _filterField.RegisterValueChangedCallback(evt =>
            {
                _filterText = evt.newValue ?? "";
                RebuildRows();
            });
            Add(_filterField);

            // Column header
            var colHeader = new VisualElement();
            colHeader.style.flexDirection = FlexDirection.Row;
            colHeader.style.paddingLeft   = 6f;
            colHeader.style.paddingRight  = 6f;
            colHeader.style.marginBottom  = 2f;

            var keyHdr  = new Label("Key");  keyHdr.style.width  = 160f; keyHdr.style.unityFontStyleAndWeight = FontStyle.Bold;
            var typeHdr = new Label("Type"); typeHdr.style.width = 60f;  typeHdr.style.unityFontStyleAndWeight = FontStyle.Bold;
            var valHdr  = new Label("Value"); valHdr.style.flexGrow = 1; valHdr.style.unityFontStyleAndWeight = FontStyle.Bold;
            colHeader.Add(keyHdr);
            colHeader.Add(typeHdr);
            colHeader.Add(valHdr);
            Add(colHeader);

            // Scrollable rows
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            Add(scroll);

            _rowContainer = new VisualElement();
            _rowContainer.style.flexDirection = FlexDirection.Column;
            scroll.Add(_rowContainer);

            // Schedule periodic refresh
            schedule.Execute(Refresh).Every(250);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetActorState(AGISActorState actorState)
        {
            _actorState = actorState;
            _lastValues.Clear();
            UpdateStatusLabel();
            RebuildRows();
        }

        public void Refresh()
        {
            if (_actorState == null) return;
            RebuildRows();
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void UpdateStatusLabel()
        {
            if (_actorState == null)
            {
                _statusLabel.text  = "No runner connected";
                _statusLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            }
            else
            {
                _statusLabel.text  = $"\u25cf LIVE  Actor: {_actorState.gameObject.name}";
                _statusLabel.style.color = new Color(0.2f, 0.9f, 0.2f);
            }
        }

        private void RebuildRows()
        {
            _rowContainer.Clear();
            UpdateStatusLabel();

            if (_actorState == null)
                return;

            // Read all values from the actor state's internal table via reflection-free API.
            // We iterate the public .values list on AGISParamTable (it's public [SerializeField]).
            var table = GetParamTable(_actorState);
            if (table?.values == null || table.values.Count == 0)
            {
                _rowContainer.Add(new Label("(empty)") { style = { color = new Color(0.5f, 0.5f, 0.5f), marginLeft = 6f } });
                return;
            }

            foreach (var pv in table.values)
            {
                if (pv == null || string.IsNullOrEmpty(pv.key)) continue;

                // Filter
                if (!string.IsNullOrEmpty(_filterText) &&
                    pv.key.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // Detect change
                bool changed = false;
                if (_lastValues.TryGetValue(pv.key, out var prev))
                {
                    changed = !ValuesEqual(prev, pv.value);
                }
                _lastValues[pv.key] = pv.value;

                var row = BuildRow(pv, _actorState != null);
                if (changed)
                {
                    row.AddToClassList("agis-bb-row--changed");
                    var captured = row;
                    schedule.Execute(() =>
                    {
                        captured.RemoveFromClassList("agis-bb-row--changed");
                    }).StartingIn(1000);
                }
                _rowContainer.Add(row);
            }
        }

        private VisualElement BuildRow(AGISParamValue pv, bool editable)
        {
            var row = new VisualElement();
            row.AddToClassList("agis-bb-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom  = 2f;
            row.style.paddingLeft   = 6f;
            row.style.paddingRight  = 6f;

            var keyLabel = new Label(pv.key);
            keyLabel.AddToClassList("agis-bb-row__key");
            keyLabel.style.width   = 160f;
            keyLabel.style.color   = new Color(0.8f, 0.9f, 1f);
            keyLabel.style.flexShrink = 0;
            row.Add(keyLabel);

            var typeLabel = new Label(pv.value.Type.ToString());
            typeLabel.AddToClassList("agis-bb-row__type");
            typeLabel.style.width   = 60f;
            typeLabel.style.color   = new Color(0.6f, 0.6f, 0.6f);
            typeLabel.style.flexShrink = 0;
            row.Add(typeLabel);

            // Value control — editable when connected
            VisualElement valueEl = BuildValueControl(pv, editable);
            valueEl.AddToClassList("agis-bb-row__value");
            valueEl.style.flexGrow = 1;
            row.Add(valueEl);

            return row;
        }

        private VisualElement BuildValueControl(AGISParamValue pv, bool editable)
        {
            if (!editable || _actorState == null)
            {
                return new Label(FormatValue(pv.value)) { style = { color = new Color(0.7f, 0.9f, 0.7f) } };
            }

            switch (pv.value.Type)
            {
                case AGISParamType.Bool:
                {
                    var toggle = new Toggle { value = pv.value.AsBool() };
                    var capturedKey = pv.key;
                    toggle.RegisterValueChangedCallback(evt =>
                        _actorState.Set(capturedKey, AGISValue.FromBool(evt.newValue)));
                    return toggle;
                }
                case AGISParamType.Int:
                {
                    var field = new IntegerField { value = pv.value.AsInt() };
                    var capturedKey = pv.key;
                    field.RegisterValueChangedCallback(evt =>
                        _actorState.Set(capturedKey, AGISValue.FromInt(evt.newValue)));
                    return field;
                }
                case AGISParamType.Float:
                {
                    var field = new FloatField { value = pv.value.AsFloat() };
                    var capturedKey = pv.key;
                    field.RegisterValueChangedCallback(evt =>
                        _actorState.Set(capturedKey, AGISValue.FromFloat(evt.newValue)));
                    return field;
                }
                case AGISParamType.String:
                {
                    var field = new TextField { value = pv.value.AsString() };
                    var capturedKey = pv.key;
                    field.RegisterValueChangedCallback(evt =>
                        _actorState.Set(capturedKey, AGISValue.FromString(evt.newValue)));
                    return field;
                }
                default:
                    return new Label(FormatValue(pv.value)) { style = { color = new Color(0.7f, 0.9f, 0.7f) } };
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Gets the underlying AGISParamTable from AGISActorState.
        /// AGISActorState exposes Set/Get but not the table directly, so we access it via
        /// the public values field on the internal _values via the typed API surface we do have.
        /// Since _values is private, we use an indirect approach: call GetBool on a key that
        /// doesn't exist returns the fallback (no mutation), but to iterate ALL keys we need the
        /// table. AGISActorState.LogState (private) uses _values.values.
        /// We expose a workaround by checking if the type exposes a public Values property,
        /// otherwise fall back to reflection for iteration only.
        /// In practice, AGISParamTable.values is public [SerializeField] — we can access it
        /// directly if we cast. But AGISActorState._values is private.
        /// Solution: use a cached shadow copy built from reading known keys (not feasible without
        /// knowing keys ahead of time). Instead we expose the table via a helper class-level field
        /// trick: mark values as public on AGISActorState, or expose a property.
        /// For this implementation we use a pragmatic approach: use GetType reflection to read
        /// the private _values field. This is safe in Unity runtime (no IL2CPP stripping concern
        /// for private fields accessed this way in editor-only runtime code).
        /// </summary>
        private static AGISParamTable GetParamTable(AGISActorState state)
        {
            if (state == null) return null;
            try
            {
                var field = typeof(AGISActorState).GetField(
                    "_values",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return field?.GetValue(state) as AGISParamTable;
            }
            catch
            {
                return null;
            }
        }

        private static bool ValuesEqual(AGISValue a, AGISValue b)
        {
            if (a.Type != b.Type) return false;
            switch (a.Type)
            {
                case AGISParamType.Bool:    return a.AsBool()   == b.AsBool();
                case AGISParamType.Int:     return a.AsInt()    == b.AsInt();
                case AGISParamType.Float:   return Mathf.Approximately(a.AsFloat(), b.AsFloat());
                case AGISParamType.String:  return a.AsString() == b.AsString();
                case AGISParamType.Vector2: return a.AsVector2() == b.AsVector2();
                case AGISParamType.Vector3: return a.AsVector3() == b.AsVector3();
                default: return true;
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
    }
}
