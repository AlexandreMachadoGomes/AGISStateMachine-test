// File: NPCHasReachedDestinationConditionType.cs
// Folder: Assets/Scripts/NPC/Conditions/
// Purpose: True when the A* AIPath agent has reached its current destination.

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using Pathfinding;

namespace AGIS.NPC.Conditions
{
    public sealed class NPCHasReachedDestinationConditionType : IAGISConditionType
    {
        public string TypeId => "npc.has_reached_destination";
        public string DisplayName => "NPC Has Reached Destination";
        public AGISParamSchema Schema { get; } = new AGISParamSchema();

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            var aiPath = args.Ctx.Actor != null
                ? args.Ctx.Actor.GetComponent<AIPath>()
                : null;
            return aiPath != null && aiPath.reachedDestination;
        }
    }
}
