// File: AGISGraphDefs.cs
// Folder: Assets/Scripts/AX State Machine/Definitions/

using System;
using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.UGC
{
    [Serializable]
    public class AGISStateMachineGraph
    {
        [SerializeField] public AGISGuid graphId = AGISGuid.New();
        [SerializeField] public int version = 1;

        [SerializeField] public AGISGuid entryNodeId = AGISGuid.Empty;

        [SerializeField] public List<AGISNodeInstanceDef> nodes = new List<AGISNodeInstanceDef>();
        [SerializeField] public List<AGISTransitionEdgeDef> edges = new List<AGISTransitionEdgeDef>();
    }

    [Serializable]
    public class AGISNodeVisualDef
    {
        [SerializeField] public Vector2 position;
        [SerializeField] public bool collapsed;
    }

    [Serializable]
    public class AGISNodeInstanceDef
    {
        [SerializeField] public AGISGuid nodeId = AGISGuid.New();
        [SerializeField] public string nodeTypeId;

        [SerializeField] public AGISParamTable @params = new AGISParamTable();

        // Editor-only visual fields (safe to keep in UGC; editor will use it later).
        [SerializeField] public AGISNodeVisualDef visual = new AGISNodeVisualDef();

        // Grouped node usage (only meaningful if nodeTypeId == "Grouped")
        [SerializeField] public AGISGuid groupAssetId = AGISGuid.Empty;
        [SerializeField] public AGISParamTable exposedOverrides = new AGISParamTable();

        // Parallel node usage (only meaningful if nodeTypeId == "Parallel")
        [SerializeField] public List<AGISNodeInstanceDef> parallelChildren = new List<AGISNodeInstanceDef>();
    }

    [Serializable]
    public class AGISTransitionPolicy
    {
        // Keep minimal for now; engine can interpret later.
        [SerializeField] public bool interruptible = true;

        [Tooltip("Optional cooldown before this edge can fire again.")]
        [SerializeField] public float cooldownSeconds = 0f;
    }

    [Serializable]
    public class AGISTransitionEdgeDef
    {
        [SerializeField] public AGISGuid edgeId = AGISGuid.New();

        [SerializeField] public AGISGuid fromNodeId;
        [SerializeField] public AGISGuid toNodeId;

        /// <summary>
        /// ALL conditions live on edges only.
        /// </summary>
        [SerializeField] public AGISConditionExprDef condition = AGISConditionExprDef.False();

        [SerializeField] public int priority = 0;

        [SerializeField] public AGISTransitionPolicy policy = new AGISTransitionPolicy();

        /// <summary>
        /// If this edge exits a Grouped/Macro node, scopeId gates eligibility:
        /// edge eligible only if grouped runtime CurrentInternalNodeId is inside that scope.
        /// </summary>
        [SerializeField] public string scopeId = "Any";
    }
}
