// File: NPCHasArrivedAtWaypointConditionType.cs
// Folder: Assets/Scripts/NPC/Conditions/
// Purpose: True when the NPC is within arrival_distance of the waypoint identified by the
//          current (SequenceIndex, WaypointIndex) stored in AGISActorState.
//          Falls back to AIPath.reachedDestination when AGISActorState or the holder is absent.
//
// Params:
//   arrival_distance  (Float, default 0.5) — radius in world units around the waypoint.

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.NPC.Routes;
using Pathfinding;
using UnityEngine;

namespace AGIS.NPC.Conditions
{
    public sealed class NPCHasArrivedAtWaypointConditionType : IAGISConditionType
    {
        public string TypeId => "npc.has_arrived_at_waypoint";
        public string DisplayName => "NPC Has Arrived At Waypoint";

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("arrival_distance", AGISParamType.Float, AGISValue.FromFloat(0.5f))
                    { displayName = "Arrival Distance", hasMin = true, floatMin = 0.01f },
            }
        };

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            var actor = args.Ctx.Actor;
            if (actor == null) return false;

            var holder     = actor.GetComponent<NPCRouteDataHolder>();
            var actorState = actor.GetComponent<AGISActorState>();

            if (holder == null || actorState == null)
            {
                var aiPath = actor.GetComponent<AIPath>();
                return aiPath != null && aiPath.reachedDestination;
            }

            int seqIdx = actorState.GetInt("npc.route.sequence_index");
            int wpIdx  = actorState.GetInt("npc.route.waypoint_index");

            if (!holder.TryGetWaypoint(seqIdx, wpIdx, out var waypoint))
                return false;

            float threshold = args.Params.GetFloat("arrival_distance", 0.5f);
            return (actor.transform.position - waypoint).sqrMagnitude <= threshold * threshold;
        }
    }
}
