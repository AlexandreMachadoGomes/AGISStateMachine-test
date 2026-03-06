// File: AGISHasDialogueChoiceConditionType.cs
// Folder: Assets/Scripts/Dialogue/
// Purpose: True when the player has made ANY choice in the active dialogue beat
//          (i.e. the choice key holds a value >= 0).
//          Useful as a guard before checking which specific option was chosen,
//          or when any response should advance dialogue regardless of which was picked.
//
// Params:
//   choice_key  (String, default "agis.dialogue.choice") — blackboard key to read from

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.Dialogue
{
    public sealed class AGISHasDialogueChoiceConditionType : IAGISConditionType
    {
        public string TypeId      => "agis.has_dialogue_choice";
        public string DisplayName => "Has Dialogue Choice";

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("choice_key", AGISParamType.String,
                                  AGISValue.FromString(AGISDialogueConstants.DefaultChoiceKey))
                    { displayName = "Choice Key",
                      tooltip     = "Blackboard key to check. Returns true for any value >= 0." },
            }
        };

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            string choiceKey = args.Params.GetString("choice_key", AGISDialogueConstants.DefaultChoiceKey);

            return args.Ctx.Blackboard.TryGet<int>(choiceKey, out int chosen)
                   && chosen != AGISDialogueConstants.NoChoice;
        }
    }
}
