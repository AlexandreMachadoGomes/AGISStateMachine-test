// File: BlackboardBoolConditionType.cs
// Folder: Assets/Scripts/NPC/Conditions/
// Purpose: Reads a bool from the actor's blackboard. True when the value at `key`
//          matches `expected`. Useful for flag-based state transitions driven by
//          external systems (combat, detection, scripted events, etc.).
//
// Params:
//   key       (String, default "")   — blackboard key to read
//   expected  (Bool,   default true) — value to compare against (supports inverted checks)

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.NPC.Conditions
{
    public sealed class BlackboardBoolConditionType : IAGISConditionType
    {
        public string TypeId => "npc.blackboard_bool";
        public string DisplayName => "Blackboard Bool";

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("key",      AGISParamType.String, AGISValue.FromString(""))
                    { displayName = "Key",      tooltip = "Blackboard key to read.", required = true },
                new AGISParamSpec("expected",  AGISParamType.Bool,   AGISValue.FromBool(true))
                    { displayName = "Expected", tooltip = "The value the blackboard key must equal for this condition to be true." },
            }
        };

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            string key      = args.Params.GetString("key", "");
            bool expected   = args.Params.GetBool("expected", true);

            if (string.IsNullOrEmpty(key)) return false;

            if (!args.Ctx.Blackboard.TryGet<bool>(key, out bool value))
                return false;

            return value == expected;
        }
    }
}
