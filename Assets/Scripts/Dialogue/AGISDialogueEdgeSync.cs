// File: AGISDialogueEdgeSync.cs
// Folder: Assets/Scripts/Dialogue/
// Purpose: Static utility for auto-managing the transition edges that belong to
//          a dialogue node in a state machine graph.
//
// Called by:
//   • AGISStateMachineGraphAsset.OnValidate  (auto-heal on asset change)
//   • The future graph editor UI             (Add Choice / Remove Last buttons)
//
// Rules enforced:
//   • 0 choices → one "Dialogue Ended" edge  (agis.has_dialogue_choice, toNodeId = Empty)
//   • First choice added → Ended edge removed, Choice-0 edge added
//   • Each further choice → next option index edge added
//   • Last choice removed → Ended edge restored
//
// Unconnected edges (toNodeId = AGISGuid.Empty):
//   AGISGraphCompiler silently skips them at compile time.
//   The future visual editor should draw !toNodeId.IsValid edges as dangling arrows.

using System.Collections.Generic;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.Dialogue
{
    public static class AGISDialogueEdgeSync
    {
        private const string EndedConditionTypeId  = "agis.has_dialogue_choice";
        private const string ChoiceConditionTypeId = "agis.dialogue_option";

        // ─── Query ────────────────────────────────────────────────────────────

        public static AGISTransitionEdgeDef FindEndedEdge(AGISStateMachineGraph graph, AGISGuid nodeId)
        {
            foreach (var edge in graph.edges)
            {
                if (edge == null || edge.fromNodeId != nodeId) continue;
                if (IsEndedEdge(edge)) return edge;
            }
            return null;
        }

        public static List<(AGISTransitionEdgeDef edge, int option)> FindChoiceEdges(
            AGISStateMachineGraph graph, AGISGuid nodeId)
        {
            var result = new List<(AGISTransitionEdgeDef, int)>();
            foreach (var edge in graph.edges)
            {
                if (edge == null || edge.fromNodeId != nodeId) continue;
                if (!TryGetChoiceOption(edge, out int option)) continue;
                result.Add((edge, option));
            }
            result.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            return result;
        }

        // ─── Mutations ────────────────────────────────────────────────────────

        /// <summary>
        /// Adds the Ended edge if the node has no managed edges yet. No-op otherwise.
        /// </summary>
        public static void EnsureEndedEdge(AGISStateMachineGraph graph, AGISGuid nodeId, string choiceKey)
        {
            if (FindChoiceEdges(graph, nodeId).Count > 0) return;
            if (FindEndedEdge(graph, nodeId) != null) return;

            graph.edges.Add(BuildEndedEdge(nodeId, choiceKey));
        }

        /// <summary>
        /// Appends a new choice transition. Removes the Ended edge on the first choice.
        /// </summary>
        public static void AddChoice(AGISStateMachineGraph graph, AGISGuid nodeId, string choiceKey)
        {
            var choices = FindChoiceEdges(graph, nodeId);
            int newOption = choices.Count;

            if (newOption == 0)
            {
                var ended = FindEndedEdge(graph, nodeId);
                if (ended != null) graph.edges.Remove(ended);
            }

            graph.edges.Add(BuildChoiceEdge(nodeId, choiceKey, newOption));
        }

        /// <summary>
        /// Removes the highest-indexed choice transition. Restores the Ended edge at 0.
        /// </summary>
        public static void RemoveLastChoice(AGISStateMachineGraph graph, AGISGuid nodeId, string choiceKey)
        {
            var choices = FindChoiceEdges(graph, nodeId);
            if (choices.Count == 0) return;

            graph.edges.Remove(choices[choices.Count - 1].edge);

            if (choices.Count - 1 == 0)
                EnsureEndedEdge(graph, nodeId, choiceKey);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        public static bool IsEndedEdge(AGISTransitionEdgeDef edge)
        {
            return edge.condition?.kind == AGISConditionExprDef.ExprKind.Leaf
                && edge.condition.leaf?.conditionTypeId == EndedConditionTypeId;
        }

        public static bool TryGetChoiceOption(AGISTransitionEdgeDef edge, out int option)
        {
            option = -1;
            if (edge.condition?.kind != AGISConditionExprDef.ExprKind.Leaf) return false;
            if (edge.condition.leaf?.conditionTypeId != ChoiceConditionTypeId) return false;

            option = edge.condition.leaf.@params.TryGet("option", out var val)
                ? val.AsInt()
                : 0;
            return true;
        }

        private static AGISTransitionEdgeDef BuildEndedEdge(AGISGuid fromNodeId, string choiceKey)
        {
            var condParams = new AGISParamTable();
            condParams.Set("choice_key", AGISValue.FromString(choiceKey));

            return new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = fromNodeId,
                toNodeId   = AGISGuid.Empty,
                condition  = AGISConditionExprDef.Leaf(new AGISConditionInstanceDef
                {
                    conditionId     = AGISGuid.New(),
                    conditionTypeId = EndedConditionTypeId,
                    @params         = condParams,
                }),
                priority = 0,
            };
        }

        private static AGISTransitionEdgeDef BuildChoiceEdge(AGISGuid fromNodeId, string choiceKey, int option)
        {
            var condParams = new AGISParamTable();
            condParams.Set("option",     AGISValue.FromInt(option));
            condParams.Set("choice_key", AGISValue.FromString(choiceKey));

            return new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = fromNodeId,
                toNodeId   = AGISGuid.Empty,
                condition  = AGISConditionExprDef.Leaf(new AGISConditionInstanceDef
                {
                    conditionId     = AGISGuid.New(),
                    conditionTypeId = ChoiceConditionTypeId,
                    @params         = condParams,
                }),
                priority = 0,
            };
        }
    }
}
