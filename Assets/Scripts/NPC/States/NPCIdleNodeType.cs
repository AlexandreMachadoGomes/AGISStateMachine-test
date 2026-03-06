// File: NPCIdleNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: NPC stands still with pathfinding fully disabled. Use as a rest, wait, or stunned state.

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using Pathfinding;
using UnityEngine;

namespace AGIS.NPC.States
{
    public sealed class NPCIdleNodeType : IAGISNodeType
    {
        public string TypeId => "npc.idle";
        public string DisplayName => "NPC Idle";
        public AGISNodeKind Kind => AGISNodeKind.Normal;
        public AGISParamSchema Schema { get; } = new AGISParamSchema();

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
            => new Runtime(args.Ctx.Actor);

        private sealed class Runtime : IAGISNodeRuntime
        {
            private readonly AIPath              _aiPath;
            private readonly Seeker              _seeker;
            private readonly AIDestinationSetter _destSetter;

            public Runtime(GameObject actor)
            {
                _aiPath     = actor != null ? actor.GetComponent<AIPath>()              : null;
                _seeker     = actor != null ? actor.GetComponent<Seeker>()              : null;
                _destSetter = actor != null ? actor.GetComponent<AIDestinationSetter>() : null;
            }

            public void Enter()
            {
                if (_aiPath     != null) _aiPath.enabled      = false;
                if (_seeker     != null) _seeker.enabled      = false;
                if (_destSetter != null) _destSetter.enabled  = false;
            }

            public void Tick(float dt) { }
            public void Exit()         { }
        }
    }
}
