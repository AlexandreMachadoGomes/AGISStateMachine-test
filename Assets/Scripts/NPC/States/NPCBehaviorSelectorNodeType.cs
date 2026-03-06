// File: NPCBehaviorSelectorNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: Transient dispatch node that sits at the entry of the template enemy graph.
//          On each Tick it does nothing — the outgoing edges decide where to go:
//            priority 1 → Routed Movement  (when npc.use_routes = true)
//            priority 0 → Wander           (ConstBool = true, fallback)
//
// npc.use_routes is declared as a persistent key by NPCRouteDataHolder (IAGISPersistentComponent),
// so it is auto-populated in AGISActorState at startup without depending on this node being
// in the active graph.

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.NPC.States
{
    public sealed class NPCBehaviorSelectorNodeType : IAGISNodeType
    {
        public string TypeId     => "npc.behavior_selector";
        public string DisplayName => "NPC Behavior Selector";
        public AGISNodeKind Kind  => AGISNodeKind.Normal;
        public AGISParamSchema Schema { get; } = new AGISParamSchema();

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
            => new Runtime();

        private sealed class Runtime : IAGISNodeRuntime
        {
            // Transient: nothing to do — outgoing edge conditions handle dispatch.
            public void Enter() { }
            public void Tick(float dt) { }
            public void Exit()  { }
        }
    }
}
