// File: AGISDialogueOptionConditionType.cs
// Folder: Assets/Scripts/Dialogue/
// Purpose: True when the player has chosen the specified option index in the active dialogue.
//          Reads the choice from the blackboard — no persistent actor state required.
//
// Params:
//   option      (Int,    default 0)                       — option index to match (0-based)
//   choice_key  (String, default "agis.dialogue.choice")  — blackboard key to read from;
//                                                           must match the source dialogue node's
//                                                           choice_key param

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.Dialogue
{
    public sealed class AGISDialogueOptionConditionType : IAGISConditionType
    {
        public string TypeId      => "agis.dialogue_option";
        public string DisplayName => "Dialogue Option";

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("option", AGISParamType.Int, AGISValue.FromInt(0))
                    { displayName = "Option Index",
                      tooltip     = "Zero-based index of the dialogue option this edge represents.",
                      hasMin = true, intMin = 0 },
                new AGISParamSpec("choice_key", AGISParamType.String,
                                  AGISValue.FromString(AGISDialogueConstants.DefaultChoiceKey))
                    { displayName = "Choice Key",
                      tooltip     = "Blackboard key to read the chosen option from. " +
                                    "Must match the choice_key on the source Dialogue node." },
            }
        };

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            string choiceKey     = args.Params.GetString("choice_key", AGISDialogueConstants.DefaultChoiceKey);
            int    expectedOption = args.Params.GetInt  ("option",     0);

            if (!args.Ctx.Blackboard.TryGet<int>(choiceKey, out int chosen))
                return false;

            return chosen == expectedOption;
        }
    }
}
