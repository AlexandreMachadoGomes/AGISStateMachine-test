// File: AGISGroupedStateAsset.cs
// Folder: Assets/Scripts/AX State Machine/Definitions/

using System;
using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.UGC
{
    /// <summary>
    /// Reusable macro asset: internal graph + scopes + exposed params + bindings.
    /// (This is UGC data; runtime uses it via Grouped node instances.)
    /// </summary>
    [CreateAssetMenu(menuName = "AGIS/ESM/Grouped State Asset", fileName = "AGIS_GroupedStateAsset")]
    public class AGISGroupedStateAsset : ScriptableObject
    {
        [SerializeField] public AGISGuid groupAssetId = AGISGuid.New();
        [SerializeField] public string displayName;

        [SerializeField] public AGISStateMachineGraph internalGraph = new AGISStateMachineGraph();

        /// <summary>
        /// LEGACY mirror of internalGraph.entryNodeId.
        /// Kept for backward compatibility with older assets.
        /// New code should treat internalGraph.entryNodeId as the single source of truth.
        /// </summary>
        [SerializeField] public AGISGuid internalEntryNodeId = AGISGuid.Empty;

        [SerializeField] public List<AGISInternalScopeDef> scopes = new List<AGISInternalScopeDef>();

        [SerializeField] public List<AGISExposedParamDef> exposedParams = new List<AGISExposedParamDef>();
        [SerializeField] public List<AGISExposedParamBindingDef> bindings = new List<AGISExposedParamBindingDef>();

        /// <summary>
        /// The internal graph entry node id (single source of truth).
        /// </summary>
        public AGISGuid EntryNodeId => internalGraph != null ? internalGraph.entryNodeId : AGISGuid.Empty;

        private void OnValidate()
        {
            if (internalGraph == null)
                internalGraph = new AGISStateMachineGraph();

            // Migrate legacy internalEntryNodeId -> internalGraph.entryNodeId
            if (!internalGraph.entryNodeId.IsValid && internalEntryNodeId.IsValid)
                internalGraph.entryNodeId = internalEntryNodeId;

            // Keep legacy mirror in sync (prevents drift).
            if (internalGraph.entryNodeId.IsValid && internalEntryNodeId != internalGraph.entryNodeId)
                internalEntryNodeId = internalGraph.entryNodeId;
        }
    }

    [Serializable]
    public class AGISInternalScopeDef
    {
        [SerializeField] public string scopeId = "Any";
        [SerializeField] public string displayName = "Any";

        [SerializeField] public List<AGISGuid> internalNodeIds = new List<AGISGuid>();
    }

    [Serializable]
    public class AGISExposedParamDef
    {
        [SerializeField] public AGISGuid exposedId = AGISGuid.New();

        /// <summary>
        /// Stable key used for overrides in node.exposedOverrides.
        /// </summary>
        [SerializeField] public string publicKey;

        [SerializeField] public string displayName;

        [SerializeField] public AGISParamType type = AGISParamType.Float;

        // Optional UI hints (kept with UGC; editor will render later).
        [SerializeField] public AGISValue defaultValue;
        [SerializeField] public bool hasMin;
        [SerializeField] public bool hasMax;
        [SerializeField] public float min;
        [SerializeField] public float max;
        [SerializeField] public float step;
        [SerializeField] public string tooltip;
        [SerializeField] public string category;
    }

    [Serializable]
    public class AGISExposedParamBindingDef
    {
        [SerializeField] public AGISGuid exposedId;
        [SerializeField] public List<AGISParamTarget> targets = new List<AGISParamTarget>();
    }

    [Serializable]
    public class AGISParamTarget
    {
        public enum TargetKind
        {
            InternalNodeParam = 0,
            InternalEdgeConditionParam = 1
        }

        [SerializeField] public TargetKind kind;

        // Node param target
        [SerializeField] public AGISGuid internalNodeId;
        [SerializeField] public string paramKey;

        // Optional edge/condition param target (advanced)
        [SerializeField] public AGISGuid internalEdgeId;

        /// <summary>
        /// Stable id for the specific leaf condition instance inside the edge's condition expression tree.
        /// If empty (legacy), runtime/editor may interpret this as "first leaf" (not recommended).
        /// </summary>
        [SerializeField] public AGISGuid internalConditionId;

        [SerializeField] public string conditionParamKey;
    }
}
