// File: AGISAuthoredTypeRecord.cs
// Folder: Assets/Scripts/AX State Machine/Authoring/
// Purpose: Serializable record that fully describes one AI/designer-authored type.
//          Stored in AGISAuthoredTypeStore so the type can be recompiled on startup
//          and inspected / edited at any time.

using System;
using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Authoring
{
    public enum AGISAuthoredTypeKind
    {
        NodeType      = 0,
        ConditionType = 1,
    }

    /// <summary>
    /// All information needed to regenerate and recompile one authored type.
    /// JsonUtility-serializable (no dictionaries; Lists only).
    /// </summary>
    [Serializable]
    public sealed class AGISAuthoredTypeRecord
    {
        // ── Identity ──────────────────────────────────────────────────────────
        [SerializeField] public string               guid;         // AGISGuid as string
        [SerializeField] public string               typeId;       // e.g. "my.custom_state"
        [SerializeField] public string               displayName;
        [SerializeField] public AGISAuthoredTypeKind kind;

        // ── Schema params ─────────────────────────────────────────────────────
        [SerializeField] public List<AGISAuthoredParamRecord> schemaParams = new List<AGISAuthoredParamRecord>();

        // ── Code bodies (node types) ──────────────────────────────────────────
        [SerializeField] public string privateFields;
        [SerializeField] public string ctorBody;
        [SerializeField] public string enterBody;
        [SerializeField] public string tickBody;
        [SerializeField] public string exitBody;
        [SerializeField] public string helperMethods;

        // ── Code body (condition types) ───────────────────────────────────────
        [SerializeField] public string evaluateBody;

        // ── Cached full source ────────────────────────────────────────────────
        // Regenerated on Save. Used directly by the recompiler at startup so
        // that template changes don't retroactively alter shipped types.
        [SerializeField] public string fullSource;

        // ── Timestamp ────────────────────────────────────────────────────────
        [SerializeField] public string lastModifiedUtc; // ISO 8601

        // ── Helpers ───────────────────────────────────────────────────────────

        public void RegenerateSource()
        {
            var paramDefs = BuildSchemaParamDefs();

            fullSource = kind == AGISAuthoredTypeKind.NodeType
                ? AGISNodeTypeCodeTemplate.Generate(
                    displayName, typeId, paramDefs,
                    privateFields, ctorBody,
                    enterBody, tickBody, exitBody,
                    helperMethods)
                : AGISConditionTypeCodeTemplate.Generate(
                    displayName, typeId, paramDefs,
                    evaluateBody, helperMethods);

            lastModifiedUtc = DateTime.UtcNow.ToString("o");
        }

        private List<AGISSchemaParamDef> BuildSchemaParamDefs()
        {
            var list = new List<AGISSchemaParamDef>(schemaParams?.Count ?? 0);
            if (schemaParams == null) return list;
            foreach (var r in schemaParams)
            {
                if (r == null || string.IsNullOrEmpty(r.key)) continue;
                list.Add(new AGISSchemaParamDef
                {
                    Key                 = r.key,
                    Type                = r.type,
                    DefaultValueLiteral = r.defaultValueLiteral,
                    DisplayName         = r.displayName,
                    Tooltip             = r.tooltip,
                });
            }
            return list;
        }
    }

    /// <summary>Serializable proxy for AGISSchemaParamDef (avoids non-serializable fields).</summary>
    [Serializable]
    public sealed class AGISAuthoredParamRecord
    {
        [SerializeField] public string        key;
        [SerializeField] public AGISParamType type;
        [SerializeField] public string        defaultValueLiteral;
        [SerializeField] public string        displayName;
        [SerializeField] public string        tooltip;
    }
}
