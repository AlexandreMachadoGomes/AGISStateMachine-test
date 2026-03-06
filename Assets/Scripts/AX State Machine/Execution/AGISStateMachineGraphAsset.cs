// File: AGISStateMachineGraphAsset.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: ScriptableObject wrapper for AGISStateMachineGraph so graphs can be authored/stored as assets.
// Notes: This is UGC data (no runtime logic).

using UnityEngine;
using AGIS.Dialogue;

namespace AGIS.ESM.UGC
{
    [CreateAssetMenu(menuName = "AGIS/ESM/State Machine Graph", fileName = "AGIS_StateMachineGraph")]
    public sealed class AGISStateMachineGraphAsset : ScriptableObject
    {
        [SerializeField] public AGISStateMachineGraph graph = new AGISStateMachineGraph();

        private void OnValidate()
        {
            if (graph == null)
                graph = new AGISStateMachineGraph();

            if (!graph.graphId.IsValid)
                graph.graphId = AGISGuid.New();

#if UNITY_EDITOR
            // Auto-heal: any dialogue node with no managed transitions gets an Ended edge.
            if (graph.nodes != null && graph.edges != null)
            {
                foreach (var node in graph.nodes)
                {
                    if (node?.nodeTypeId != "agis.dialogue") continue;

                    string choiceKey = node.@params.TryGet("choice_key", out var ck)
                        ? ck.AsString()
                        : AGISDialogueConstants.DefaultChoiceKey;

                    AGISDialogueEdgeSync.EnsureEndedEdge(graph, node.nodeId, choiceKey);
                }
            }
#endif
        }
    }
}
