// File: AGISParamFieldDrawer.cs
// Folder: Assets/Scripts/AX State Machine/RuntimeEditor/Nodes/
// Purpose: Static factory that builds a UIToolkit VisualElement for each param spec.
//          No UnityEditor dependencies — uses only UnityEngine.UIElements.

using System;
using UnityEngine;
using UnityEngine.UIElements;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.RuntimeEditor
{
    public static class AGISParamFieldDrawer
    {
        /// <summary>
        /// Creates a .agis-field-row VisualElement for the given param spec.
        /// The element contains a label, an appropriate control, and a [R] reset button.
        /// </summary>
        /// <param name="spec">The parameter specification (schema-defined).</param>
        /// <param name="instanceParams">The instance param table (overrides only).</param>
        /// <param name="onChange">Callback: (key, newValue). Called on every committed change.</param>
        public static VisualElement CreateField(
            AGISParamSpec spec,
            AGISParamTable instanceParams,
            Action<string, AGISValue> onChange)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (instanceParams == null) throw new ArgumentNullException(nameof(instanceParams));

            bool isOverridden = instanceParams.TryGet(spec.key, out AGISValue overriddenValue);
            AGISValue currentValue = isOverridden ? overriddenValue : spec.defaultValue;

            var row = new VisualElement();
            row.AddToClassList("agis-field-row");

            // ── Label ────────────────────────────────────────────────────────────
            string labelText = !string.IsNullOrEmpty(spec.displayName) ? spec.displayName : spec.key;

            var label = new Label(labelText);
            label.AddToClassList("agis-field-row__label");
            label.AddToClassList(isOverridden
                ? "agis-field-row__label--overridden"
                : "agis-field-row__label--default");
            label.tooltip = !string.IsNullOrEmpty(spec.tooltip) ? spec.tooltip : spec.key;
            row.Add(label);

            // ── Control container ─────────────────────────────────────────────
            var controlContainer = new VisualElement();
            controlContainer.AddToClassList("agis-field-row__control");
            row.Add(controlContainer);

            // ── Reset button ──────────────────────────────────────────────────
            var resetBtn = new Button();
            resetBtn.text = "R";
            resetBtn.AddToClassList("agis-field-row__reset");
            if (isOverridden)
                resetBtn.AddToClassList("agis-field-row__reset--visible");
            row.Add(resetBtn);

            // Helper: update label and reset-btn visibility when override state changes
            void RefreshOverrideIndicators(bool nowOverridden)
            {
                label.RemoveFromClassList("agis-field-row__label--overridden");
                label.RemoveFromClassList("agis-field-row__label--default");
                label.AddToClassList(nowOverridden
                    ? "agis-field-row__label--overridden"
                    : "agis-field-row__label--default");

                resetBtn.RemoveFromClassList("agis-field-row__reset--visible");
                if (nowOverridden)
                    resetBtn.AddToClassList("agis-field-row__reset--visible");
            }

            // ── Build the control per type ────────────────────────────────────
            switch (spec.type)
            {
                case AGISParamType.Bool:
                {
                    var toggle = new Toggle();
                    toggle.value = currentValue.AsBool(spec.defaultValue.AsBool());
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        var val = AGISValue.FromBool(evt.newValue);
                        instanceParams.Set(spec.key, val);
                        RefreshOverrideIndicators(true);
                        onChange?.Invoke(spec.key, val);
                    });
                    controlContainer.Add(toggle);
                    break;
                }

                case AGISParamType.Int:
                {
                    var field = new IntegerField();
                    field.value = currentValue.AsInt(spec.defaultValue.AsInt());
                    field.RegisterValueChangedCallback(evt =>
                    {
                        int clamped = evt.newValue;
                        if (spec.hasMin && clamped < spec.intMin) clamped = spec.intMin;
                        if (spec.hasMax && clamped > spec.intMax) clamped = spec.intMax;
                        if (clamped != evt.newValue) field.SetValueWithoutNotify(clamped);

                        var val = AGISValue.FromInt(clamped);
                        instanceParams.Set(spec.key, val);
                        RefreshOverrideIndicators(true);
                        onChange?.Invoke(spec.key, val);
                    });
                    controlContainer.Add(field);
                    break;
                }

                case AGISParamType.Float:
                {
                    var field = new FloatField();
                    field.value = currentValue.AsFloat(spec.defaultValue.AsFloat());
                    field.RegisterValueChangedCallback(evt =>
                    {
                        float clamped = evt.newValue;
                        if (spec.hasMin && clamped < spec.floatMin) clamped = spec.floatMin;
                        if (spec.hasMax && clamped > spec.floatMax) clamped = spec.floatMax;
                        if (!Mathf.Approximately(clamped, evt.newValue)) field.SetValueWithoutNotify(clamped);

                        var val = AGISValue.FromFloat(clamped);
                        instanceParams.Set(spec.key, val);
                        RefreshOverrideIndicators(true);
                        onChange?.Invoke(spec.key, val);
                    });
                    controlContainer.Add(field);
                    break;
                }

                case AGISParamType.String:
                {
                    var field = new TextField();
                    field.value = currentValue.AsString(spec.defaultValue.AsString());
                    field.RegisterValueChangedCallback(evt =>
                    {
                        var val = AGISValue.FromString(evt.newValue ?? string.Empty);
                        instanceParams.Set(spec.key, val);
                        RefreshOverrideIndicators(true);
                        onChange?.Invoke(spec.key, val);
                    });
                    controlContainer.Add(field);
                    break;
                }

                case AGISParamType.Vector2:
                {
                    var vec2 = currentValue.AsVector2(spec.defaultValue.AsVector2());

                    var hRow = new VisualElement();
                    hRow.style.flexDirection = FlexDirection.Row;

                    var xField = new FloatField("X");
                    xField.value = vec2.x;
                    xField.style.flexGrow = 1;

                    var yField = new FloatField("Y");
                    yField.value = vec2.y;
                    yField.style.flexGrow = 1;

                    Action commitVec2 = () =>
                    {
                        var val = AGISValue.FromVector2(new Vector2(xField.value, yField.value));
                        instanceParams.Set(spec.key, val);
                        RefreshOverrideIndicators(true);
                        onChange?.Invoke(spec.key, val);
                    };

                    xField.RegisterValueChangedCallback(_ => commitVec2());
                    yField.RegisterValueChangedCallback(_ => commitVec2());

                    hRow.Add(xField);
                    hRow.Add(yField);
                    controlContainer.Add(hRow);
                    break;
                }

                case AGISParamType.Vector3:
                {
                    var vec3 = currentValue.AsVector3(spec.defaultValue.AsVector3());

                    var hRow = new VisualElement();
                    hRow.style.flexDirection = FlexDirection.Row;

                    var xField = new FloatField("X");
                    xField.value = vec3.x;
                    xField.style.flexGrow = 1;

                    var yField = new FloatField("Y");
                    yField.value = vec3.y;
                    yField.style.flexGrow = 1;

                    var zField = new FloatField("Z");
                    zField.value = vec3.z;
                    zField.style.flexGrow = 1;

                    Action commitVec3 = () =>
                    {
                        var val = AGISValue.FromVector3(new Vector3(xField.value, yField.value, zField.value));
                        instanceParams.Set(spec.key, val);
                        RefreshOverrideIndicators(true);
                        onChange?.Invoke(spec.key, val);
                    };

                    xField.RegisterValueChangedCallback(_ => commitVec3());
                    yField.RegisterValueChangedCallback(_ => commitVec3());
                    zField.RegisterValueChangedCallback(_ => commitVec3());

                    hRow.Add(xField);
                    hRow.Add(yField);
                    hRow.Add(zField);
                    controlContainer.Add(hRow);
                    break;
                }

                case AGISParamType.Guid:
                {
                    var guidVal = currentValue.AsGuid(spec.defaultValue.AsGuid());
                    var guidStr = guidVal.IsValid ? guidVal.Value : "(none)";

                    var guidRow = new VisualElement();
                    guidRow.style.flexDirection = FlexDirection.Row;
                    guidRow.style.alignItems = Align.Center;

                    var guidLabel = new Label(guidStr);
                    guidLabel.style.flexGrow = 1;
                    guidLabel.style.fontSize = 10;
                    guidLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                    guidLabel.style.overflow = Overflow.Hidden;

                    var copyBtn = new Button(() =>
                    {
                        GUIUtility.systemCopyBuffer = guidStr;
                    });
                    copyBtn.text = "\u29c9"; // copy icon substitute
                    copyBtn.tooltip = "Copy GUID to clipboard";
                    copyBtn.style.width = 20;
                    copyBtn.style.height = 18;
                    copyBtn.style.fontSize = 11;
                    copyBtn.style.marginLeft = 2;
                    copyBtn.style.paddingLeft = 0;
                    copyBtn.style.paddingRight = 0;

                    guidRow.Add(guidLabel);
                    guidRow.Add(copyBtn);
                    controlContainer.Add(guidRow);
                    break;
                }

                default:
                {
                    var unknownLabel = new Label($"(unsupported: {spec.type})");
                    unknownLabel.style.fontSize = 10;
                    unknownLabel.style.color = new StyleColor(new Color(0.6f, 0.4f, 0.4f));
                    controlContainer.Add(unknownLabel);
                    break;
                }
            }

            // ── Wire reset button ─────────────────────────────────────────────
            resetBtn.clicked += () =>
            {
                instanceParams.Remove(spec.key);
                RefreshOverrideIndicators(false);

                // Restore control to default value without re-triggering onChange
                // (onChange is only for user-committed changes)
                onChange?.Invoke(spec.key, spec.defaultValue);

                // Rebuild the row to refresh the control's displayed value
                // We raise an event to let the card re-draw (simpler than surgical field update)
                var resetEvt = ResetToDefaultEvent.GetPooled(spec.key, spec.defaultValue);
                row.SendEvent(resetEvt);
            };

            // ── Tooltip (?) suffix ────────────────────────────────────────────
            if (!string.IsNullOrEmpty(spec.tooltip))
            {
                label.tooltip = spec.tooltip;
                controlContainer.tooltip = spec.tooltip;
            }

            return row;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Custom event so the node card can listen and rebuild a field when reset
    // ─────────────────────────────────────────────────────────────────────────────

    public sealed class ResetToDefaultEvent : EventBase<ResetToDefaultEvent>
    {
        public string Key { get; private set; }
        public AGISValue DefaultValue { get; private set; }

        public static ResetToDefaultEvent GetPooled(string key, AGISValue defaultValue)
        {
            var evt = GetPooled();
            evt.Key = key;
            evt.DefaultValue = defaultValue;
            return evt;
        }

        public ResetToDefaultEvent() { }

        private ResetToDefaultEvent(string key, AGISValue defaultValue)
        {
            Key = key;
            DefaultValue = defaultValue;
        }
    }
}
