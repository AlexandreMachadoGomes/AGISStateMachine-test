// File: NPCOnSequenceIndexConditionType.cs
// Folder: Assets/Scripts/NPC/Conditions/
// Purpose: True when the NPCRoutedMovement state is currently at a specific step in the
//          traversal sequence. Use this to trigger contextual behaviour on particular
//          legs of a patrol (e.g. "alert guards at the door when on sequence step 2").
//
// Reads "npc.route.sequence_index" from the blackboard (written each Tick by NPCRoutedMovementNodeType).
//
// Params:
//   sequence_index  (Int, default 0)    — sequence step to match
//   comparison      (Int, default 0)    — 0 = Equal, 1 = GreaterThan, 2 = LessThan

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.NPC.Conditions
{
    public sealed class NPCOnSequenceIndexConditionType : IAGISConditionType
    {
        public string TypeId => "npc.on_sequence_index";
        public string DisplayName => "NPC On Sequence Index";

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("sequence_index", AGISParamType.Int, AGISValue.FromInt(0))
                    { displayName = "Sequence Index", tooltip = "The sequence step to compare against.",
                      hasMin = true, intMin = 0 },
                new AGISParamSpec("comparison", AGISParamType.Int, AGISValue.FromInt(0))
                    { displayName = "Comparison",
                      tooltip = "0 = Equal, 1 = GreaterThan, 2 = LessThan",
                      hasMin = true, intMin = 0, hasMax = true, intMax = 2 },
            }
        };

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            int target     = args.Params.GetInt("sequence_index", 0);
            int comparison = args.Params.GetInt("comparison", 0);

            if (!args.Ctx.Blackboard.TryGet<int>("npc.route.sequence_index", out int current))
                return false;

            switch (comparison)
            {
                case 1:  return current > target;
                case 2:  return current < target;
                default: return current == target;
            }
        }
    }
}
