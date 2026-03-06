// File: AGISParamSpec.cs
// Folder: Assets/Scripts/AX State Machine/Compilation/
// Purpose: Universal param schema spec (code-provided). Instances store overrides in AGISParamTable.

using System;
using UnityEngine;

namespace AGIS.ESM.UGC.Params
{
    [Serializable]
    public sealed class AGISParamSpec
    {
        [SerializeField] public string key;

        [SerializeField] public AGISParamType type;

        [SerializeField] public AGISValue defaultValue;

        [Header("Validation (optional)")]
        [SerializeField] public bool required;
        [SerializeField] public bool hasMin;
        [SerializeField] public bool hasMax;

        // Interpreted based on type:
        // - Float: uses floatMin/floatMax/step
        // - Int: uses intMin/intMax/step (step interpreted as int increment)
        [SerializeField] public float floatMin;
        [SerializeField] public float floatMax;

        [SerializeField] public int intMin;
        [SerializeField] public int intMax;

        [SerializeField] public float step;

        [Header("UI (optional)")]
        [SerializeField] public string displayName;
        [SerializeField] public string tooltip;
        [SerializeField] public string category;

        public AGISParamSpec() { }

        public AGISParamSpec(string key, AGISParamType type, AGISValue defaultValue)
        {
            this.key = key;
            this.type = type;
            this.defaultValue = defaultValue;
        }

        public bool IsKeyValid => !string.IsNullOrEmpty(key);

        public bool ValidateValue(in AGISValue value, out string error)
        {
            error = null;

            if (value.Type != type)
            {
                error = $"Type mismatch for '{key}'. Expected {type}, got {value.Type}.";
                return false;
            }

            switch (type)
            {
                case AGISParamType.Float:
                    {
                        var v = value.AsFloat();
                        if (hasMin && v < floatMin) { error = $"'{key}' below min ({floatMin})."; return false; }
                        if (hasMax && v > floatMax) { error = $"'{key}' above max ({floatMax})."; return false; }
                        return true;
                    }
                case AGISParamType.Int:
                    {
                        var v = value.AsInt();
                        if (hasMin && v < intMin) { error = $"'{key}' below min ({intMin})."; return false; }
                        if (hasMax && v > intMax) { error = $"'{key}' above max ({intMax})."; return false; }
                        return true;
                    }
                default:
                    return true;
            }
        }
    }
}
