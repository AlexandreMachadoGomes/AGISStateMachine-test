// File: NPCHasReachedDestinationConditionType.cs
// Folder: Assets/Scripts/NPC/Conditions/
// Purpose: True when the pathfinder has reached its current destination.

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.NPC.Conditions
{
    public sealed class NPCHasReachedDestinationConditionType : IAGISConditionType
    {
        public string TypeId => "npc.has_reached_destination";
        public string DisplayName => "NPC Has Reached Destination";
        public AGISParamSchema Schema { get; } = new AGISParamSchema();

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            var pathFinder = args.Ctx.Actor != null
                ? args.Ctx.Actor.GetComponent<IAGISNPCPathFinder>()
                : null;
            return pathFinder != null && pathFinder.ReachedDestination;
        }
    }
}
