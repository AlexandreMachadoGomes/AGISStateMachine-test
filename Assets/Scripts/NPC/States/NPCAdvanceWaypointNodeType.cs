// File: NPCAdvanceWaypointNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: Transient action state — reads (SequenceIndex, WaypointIndex, SequenceDirection)
//          from AGISActorState and writes the NEXT position in the ping-pong traversal.
//
// Traversal rules:
//   • Waypoints advance one step in SequenceDirection (+1 or -1) within the current route.
//   • When a route is exhausted in the current direction, move to the next route in the
//     sequence (same direction) and start from the appropriate end (first wp if +1, last if -1).
//   • When the sequence boundary is hit (first or last route fully done), reverse direction
//     and stay on the same route, stepping one waypoint back from the boundary.
//
// Example with routes A=[p0,p1,p2] and B=[p3,p4,p5]:
//   Forward:  A.p0 → A.p1 → A.p2 → B.p3 → B.p4 → B.p5
//   Reverse:  B.p4 → B.p3 → A.p2 → A.p1 → A.p0
//   Forward:  A.p1 → A.p2 → B.p3 → B.p4 → B.p5  → ...
//
// The direction and indices all live in AGISActorState so they survive transitions to
// other states (e.g. Chase) and resume correctly when returning to patrol.

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.NPC.Routes;
using UnityEngine;

namespace AGIS.NPC.States
{
    public sealed class NPCAdvanceWaypointNodeType : IAGISNodeType
    {
        public string TypeId => "npc.advance_waypoint";
        public string DisplayName => "NPC Advance Waypoint";
        public AGISNodeKind Kind => AGISNodeKind.Normal;
        public AGISParamSchema Schema { get; } = new AGISParamSchema();

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
            => new Runtime(args.Ctx);

        private sealed class Runtime : IAGISNodeRuntime
        {
            private readonly AGISExecutionContext _ctx;
            private readonly NPCRouteDataHolder   _holder;
            private readonly AGISActorState       _actorState;

            public Runtime(AGISExecutionContext ctx)
            {
                _ctx        = ctx;
                _holder     = ctx.Actor?.GetComponent<NPCRouteDataHolder>();
                _actorState = ctx.Actor?.GetComponent<AGISActorState>();
            }

            public void Enter()
            {
                if (_holder == null || _actorState == null || !_holder.HasData) return;

                int seqIdx = _actorState.GetInt("npc.route.sequence_index");
                int wpIdx  = _actorState.GetInt("npc.route.waypoint_index");
                int seqDir = _actorState.GetInt("npc.route.sequence_direction", 1);

                // ── Step waypoint in the current direction ────────────────────────────
                wpIdx += seqDir;
                int waypointCount = _holder.GetWaypointCount(seqIdx);

                if (wpIdx < 0 || wpIdx >= waypointCount)
                {
                    // Current route exhausted — try the next route in the same direction.
                    int nextSeq = seqIdx + seqDir;

                    if (nextSeq < 0 || nextSeq >= _holder.routeData.SequenceLength)
                    {
                        // ── Sequence boundary hit: reverse direction ───────────────────
                        seqDir = -seqDir;
                        _actorState.Set("npc.route.sequence_direction", AGISValue.FromInt(seqDir));

                        // Step back from the boundary within the same route.
                        // wpIdx is currently out of bounds (e.g. -1 or waypointCount).
                        // Clamp it to the nearest valid index, then step one in new direction.
                        wpIdx = Mathf.Clamp(wpIdx, 0, waypointCount - 1);
                        wpIdx += seqDir;

                        // Guard: single-waypoint route edge case.
                        wpIdx = Mathf.Clamp(wpIdx, 0, waypointCount - 1);
                    }
                    else
                    {
                        // ── Move to next route, start from the correct end ────────────
                        seqIdx = nextSeq;
                        int nextCount = _holder.GetWaypointCount(seqIdx);
                        wpIdx = seqDir > 0 ? 0 : nextCount - 1;
                    }
                }

                _actorState.Set("npc.route.sequence_index", AGISValue.FromInt(seqIdx));
                _actorState.Set("npc.route.waypoint_index", AGISValue.FromInt(wpIdx));
                _actorState.Set("npc.route.route_name",
                    AGISValue.FromString(_holder.GetRouteName(seqIdx)));

                // Mirror to blackboard.
                _ctx.Blackboard.Set("npc.route.sequence_index", seqIdx);
                _ctx.Blackboard.Set("npc.route.waypoint_index", wpIdx);
            }

            public void Tick(float dt) { }

            public void Exit() { }
        }
    }
}
