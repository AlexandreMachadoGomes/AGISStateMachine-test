// File: NPCIdleNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: NPC stands still with pathfinding fully disabled. Use as a rest, wait, or stunned state.

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
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
            private readonly IAGISNPCPathFinder _pathFinder;

            public Runtime(GameObject actor)
            {
                _pathFinder = actor != null ? actor.GetComponent<IAGISNPCPathFinder>() : null;
            }

            public void Enter()  => _pathFinder?.DisablePathfinding();
            public void Tick(float dt) { }
            public void Exit()   { }
        }
    }
}
