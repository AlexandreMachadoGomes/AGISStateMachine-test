// File: NPCMoveToWaypointNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: Points the NPC toward the current waypoint from NPCRouteDataHolder and enables
//          pathfinding. Current position in the sequence is read from AGISActorState.
//
// Implements IAGISPersistentNodeType so the runner pre-populates AGISActorState with the
// route index keys on startup (EnsureKey — does NOT overwrite existing values, enabling resume).

using System.Collections.Generic;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.NPC.Routes;
using UnityEngine;

namespace AGIS.NPC.States
{
    public sealed class NPCMoveToWaypointNodeType : IAGISNodeType, IAGISPersistentNodeType, IAGISNodeComponentRequirements
    {
        public string TypeId => "npc.move_to_waypoint";
        public string DisplayName => "NPC Move To Waypoint";
        public AGISNodeKind Kind => AGISNodeKind.Normal;
        public AGISParamSchema Schema { get; } = new AGISParamSchema();

        public IReadOnlyList<AGISParamSpec> PersistentParams { get; } = new AGISParamSpec[]
        {
            new AGISParamSpec("npc.route.sequence_index",     AGISParamType.Int,    AGISValue.FromInt(0))
                { displayName = "Route Sequence Index" },
            new AGISParamSpec("npc.route.waypoint_index",     AGISParamType.Int,    AGISValue.FromInt(0))
                { displayName = "Waypoint Index" },
            new AGISParamSpec("npc.route.route_name",         AGISParamType.String, AGISValue.FromString(""))
                { displayName = "Current Route Name" },
            new AGISParamSpec("npc.route.sequence_direction", AGISParamType.Int,    AGISValue.FromInt(1))
                { displayName = "Sequence Direction",
                  tooltip     = "+1 = traversing routes forward, -1 = traversing in reverse (ping-pong)." },
        };

        public IReadOnlyList<System.Type> GetRequiredComponents(IAGISParamAccessor resolvedParams)
            => new[] { typeof(IAGISNPCPathFinder) };

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
            => new Runtime(args.Ctx);

        private sealed class Runtime : IAGISNodeRuntime
        {
            private readonly AGISExecutionContext _ctx;
            private readonly IAGISNPCPathFinder   _pathFinder;
            private readonly NPCRouteDataHolder   _holder;
            private readonly AGISActorState       _actorState;

            public Runtime(AGISExecutionContext ctx)
            {
                _ctx        = ctx;
                _pathFinder = ctx.Actor?.GetComponent<IAGISNPCPathFinder>();
                _holder     = ctx.Actor?.GetComponent<NPCRouteDataHolder>();
                _actorState = ctx.Actor?.GetComponent<AGISActorState>();
            }

            public void Enter()
            {
                if (_pathFinder == null || _holder == null) return;

                _pathFinder.EnablePathfinding();

                int seqIdx = _actorState?.GetInt("npc.route.sequence_index") ?? 0;
                int wpIdx  = _actorState?.GetInt("npc.route.waypoint_index") ?? 0;

                if (_holder.TryGetWaypoint(seqIdx, wpIdx, out var waypoint))
                    _pathFinder.SetWalkTarget(waypoint);

                _ctx.Blackboard.Set("npc.route.sequence_index", seqIdx);
                _ctx.Blackboard.Set("npc.route.waypoint_index", wpIdx);
                _ctx.Blackboard.Set("npc.route.route_name",     _holder.GetRouteName(seqIdx));
            }

            public void Tick(float dt) { }

            public void Exit() => _pathFinder?.DisablePathfinding();
        }
    }
}
