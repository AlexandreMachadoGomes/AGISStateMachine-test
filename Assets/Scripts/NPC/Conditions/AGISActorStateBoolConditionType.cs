// File: AGISActorStateBoolConditionType.cs
// Folder: Assets/Scripts/NPC/Conditions/
// Purpose: True when the bool stored in AGISActorState under `key` equals `expected`.
//          Use this to drive transitions based on persistent actor flags (e.g. npc.use_routes).
//
// Params:
//   key       (String, default "")   — AGISActorState key to read
//   expected  (Bool,   default true) — value to compare against

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.NPC.Conditions
{
    public sealed class AGISActorStateBoolConditionType : IAGISConditionType
    {
        public string TypeId      => "npc.actor_state_bool";
        public string DisplayName => "Actor State Bool";

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("key",      AGISParamType.String, AGISValue.FromString(""))
                    { displayName = "Key",
                      tooltip     = "The AGISActorState key to read (e.g. \"npc.use_routes\")." },
                new AGISParamSpec("expected", AGISParamType.Bool,   AGISValue.FromBool(true))
                    { displayName = "Expected Value" },
            }
        };

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            if (args.Ctx.Actor == null) return false;

            string key      = args.Params.GetString("key",      "");
            bool   expected = args.Params.GetBool  ("expected", true);

            if (string.IsNullOrEmpty(key)) return false;

            var actorState = args.Ctx.Actor.GetComponent<AGISActorState>();
            if (actorState == null) return false;

            return actorState.GetBool(key) == expected;
        }
    }
}
