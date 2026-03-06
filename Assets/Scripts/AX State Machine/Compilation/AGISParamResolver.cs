// File: AGISParamResolver.cs
// Folder: Assets/Scripts/AX State Machine/Compilation/
// Purpose: Resolve override-or-default for schema + table; validate tables against schema.

using System;
using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.UGC;

namespace AGIS.ESM.UGC.Params
{
    public enum AGISParamIssueSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    public readonly struct AGISParamIssue
    {
        public readonly AGISParamIssueSeverity Severity;
        public readonly string Key;
        public readonly string Message;

        public AGISParamIssue(AGISParamIssueSeverity severity, string key, string message)
        {
            Severity = severity;
            Key = key;
            Message = message;
        }

        public override string ToString() => $"{Severity}: {Key} - {Message}";
    }

    /// <summary>
    /// Read-only param accessor (typed reads). Built from schema+table.
    /// </summary>
    public interface IAGISParamAccessor
    {
        AGISValue Get(string key, AGISValue fallback = default);

        bool GetBool(string key, bool fallback = default);
        int GetInt(string key, int fallback = default);
        float GetFloat(string key, float fallback = default);
        string GetString(string key, string fallback = "");
        Vector2 GetVector2(string key, Vector2 fallback = default);
        Vector3 GetVector3(string key, Vector3 fallback = default);
        AGISGuid GetGuid(string key, AGISGuid fallback = default);
    }

    public static class AGISParamResolver
    {
        public static bool TryResolve(AGISParamSchema schema, AGISParamTable table, string key, out AGISValue value)
        {
            value = default;

            if (schema == null || string.IsNullOrEmpty(key))
                return false;

            if (table != null && table.TryGet(key, out var overrideVal))
            {
                if (schema.TryGetSpec(key, out var spec))
                {
                    if (spec.ValidateValue(overrideVal, out _))
                    {
                        value = overrideVal;
                        return true;
                    }

                    value = spec.defaultValue;
                    return true;
                }

                // Unknown key: still return override as-is.
                value = overrideVal;
                return true;
            }

            if (schema.TryGetSpec(key, out var s2))
            {
                value = s2.defaultValue;
                return true;
            }

            return false;
        }

        public static AGISValue ResolveOrFallback(AGISParamSchema schema, AGISParamTable table, string key, AGISValue fallback = default)
        {
            return TryResolve(schema, table, key, out var value) ? value : fallback;
        }

        public static AGISResolvedParams BuildAccessor(AGISParamSchema schema, AGISParamTable table)
        {
            return new AGISResolvedParams(schema, table);
        }

        public static void Validate(AGISParamSchema schema, AGISParamTable table, List<AGISParamIssue> issues, bool allowUnknownKeys = false)
        {
            if (issues == null)
                return;

            if (schema == null)
            {
                issues.Add(new AGISParamIssue(AGISParamIssueSeverity.Error, "<schema>", "Schema is null."));
                return;
            }

            // Required keys: policy = required means "must be valid effectively"; default counts.
            foreach (var spec in schema.Specs)
            {
                if (spec == null || string.IsNullOrEmpty(spec.key))
                    continue;

                if (!spec.required)
                    continue;

                // Validate default itself for required params
                if (!spec.ValidateValue(spec.defaultValue, out var errDefault))
                    issues.Add(new AGISParamIssue(AGISParamIssueSeverity.Error, spec.key, $"Required param has invalid default: {errDefault}"));
            }

            if (table == null || table.values == null)
                return;

            for (int i = 0; i < table.values.Count; i++)
            {
                var pv = table.values[i];
                if (pv == null || string.IsNullOrEmpty(pv.key))
                    continue;

                var key = pv.key;

                if (!schema.TryGetSpec(key, out var spec))
                {
                    if (!allowUnknownKeys)
                        issues.Add(new AGISParamIssue(AGISParamIssueSeverity.Warning, key, "Unknown param key (not in schema)."));
                    continue;
                }

                if (!spec.ValidateValue(pv.value, out var err))
                    issues.Add(new AGISParamIssue(AGISParamIssueSeverity.Error, key, err));
            }
        }
    }

    /// <summary>
    /// Lightweight accessor; resolves override-or-default on demand.
    /// </summary>
    public readonly struct AGISResolvedParams : IAGISParamAccessor
    {
        private readonly AGISParamSchema _schema;
        private readonly AGISParamTable _table;

        public AGISResolvedParams(AGISParamSchema schema, AGISParamTable table)
        {
            _schema = schema;
            _table = table;
        }

        public AGISValue Get(string key, AGISValue fallback = default)
        {
            return AGISParamResolver.ResolveOrFallback(_schema, _table, key, fallback);
        }

        public bool GetBool(string key, bool fallback = default) => Get(key, AGISValue.FromBool(fallback)).AsBool(fallback);
        public int GetInt(string key, int fallback = default) => Get(key, AGISValue.FromInt(fallback)).AsInt(fallback);
        public float GetFloat(string key, float fallback = default) => Get(key, AGISValue.FromFloat(fallback)).AsFloat(fallback);
        public string GetString(string key, string fallback = "") => Get(key, AGISValue.FromString(fallback)).AsString(fallback);
        public Vector2 GetVector2(string key, Vector2 fallback = default) => Get(key, AGISValue.FromVector2(fallback)).AsVector2(fallback);
        public Vector3 GetVector3(string key, Vector3 fallback = default) => Get(key, AGISValue.FromVector3(fallback)).AsVector3(fallback);
        public AGISGuid GetGuid(string key, AGISGuid fallback = default) => Get(key, AGISValue.FromGuid(fallback)).AsGuid(fallback);
    }
}
