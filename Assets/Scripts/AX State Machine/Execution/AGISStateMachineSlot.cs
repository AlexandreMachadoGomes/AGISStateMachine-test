// File: AGISStateMachineSlot.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Serializable per-actor slot configuration (multiple state machines per actor).

using System;
using UnityEngine;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Runtime
{
    [Serializable]
    public sealed class AGISStateMachineSlot
    {
        [SerializeField] public string slotName = "Default";
        [SerializeField] public bool enabled = true;

        [Header("Graph Asset")]
        [SerializeField] public AGISStateMachineGraphAsset graphAsset;

        [Header("Overrides (optional)")]
        [Tooltip("If > 0, overrides runner tickHz for this slot.")]
        [SerializeField] public float tickHzOverride = 0f;

        [Tooltip("If > 0, overrides runner max transitions per tick for this slot.")]
        [SerializeField] public int maxTransitionsPerTickOverride = 0;

        // Runtime
        [NonSerialized] internal AGISStateMachineInstance instance;
        [NonSerialized] internal AGISRuntimeGraph compiledGraph;
        [NonSerialized] internal AGISGraphValidationReport lastValidation;

        [NonSerialized] internal float tickAccumulator;

        public AGISStateMachineGraph GetGraphDef() => graphAsset != null ? graphAsset.graph : null;

        // Forwarding properties so the editor can read instance state without
        // a direct reference to AGISStateMachineInstance.
        public AGISStateMachineInstance Instance => instance;
        public AGISGuid CurrentNodeId => instance?.CurrentNodeId ?? AGISGuid.Empty;
        public AGISGuid LastTransitionEdgeId => instance?.LastTransitionEdgeId ?? AGISGuid.Empty;

        public float GetTickHz(float runnerTickHz) => tickHzOverride > 0f ? tickHzOverride : runnerTickHz;
        public int GetMaxTransitionsPerTick(int runnerMax) => maxTransitionsPerTickOverride > 0 ? maxTransitionsPerTickOverride : runnerMax;

        internal void ResetRuntimeAccumulators() => tickAccumulator = 0f;
    }
}
