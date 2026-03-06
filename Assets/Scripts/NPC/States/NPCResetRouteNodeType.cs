// File: NPCResetRouteNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: Transient action state — explicitly resets the route indices in AGISActorState
//          back to (0, 0). Place this as the entry node of the Routed Movement grouped
//          state when you want the patrol to always restart from the beginning on entry.
//
// For resume-from-checkpoint behaviour, remove this node from the internal graph and set
// npc.move_to_waypoint as the entry instead — AGISActorState will already hold the last
// saved indices so the NPC picks up exactly where it left off.

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.NPC.Routes;

namespace AGIS.NPC.States
{
    public sealed class NPCResetRouteNodeType : IAGISNodeType
    {
        public string TypeId => "npc.reset_route";
        public string DisplayName => "NPC Reset Route";
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
                if (_actorState == null) return;

                _actorState.Set("npc.route.sequence_index",     AGISValue.FromInt(0));
                _actorState.Set("npc.route.waypoint_index",     AGISValue.FromInt(0));
                _actorState.Set("npc.route.sequence_direction", AGISValue.FromInt(1));
                _actorState.Set("npc.route.route_name",
                    AGISValue.FromString(_holder?.GetRouteName(0) ?? ""));

                // Mirror to blackboard.
                _ctx.Blackboard.Set("npc.route.sequence_index", 0);
                _ctx.Blackboard.Set("npc.route.waypoint_index", 0);
            }

            public void Tick(float dt) { }

            public void Exit() { }
        }
    }
}
