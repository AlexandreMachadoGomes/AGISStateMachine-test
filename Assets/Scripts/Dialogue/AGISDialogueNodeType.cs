// File: AGISDialogueNodeType.cs
// Folder: Assets/Scripts/Dialogue/
// Purpose: State machine node that represents a single beat of dialogue.
//          It waits for game code to write a player choice to the blackboard,
//          then outgoing edges (with AGISDialogueOptionConditionType) fire on the next tick.
//
// Params (live on the node — no persistent actor state):
//   dialogue_id  (String, default "")                    — opaque ID game code uses to look
//                                                          up the dialogue content / UI to show
//   choice_key   (String, default "agis.dialogue.choice") — blackboard key where the chosen
//                                                          option index will be written
//
// Lifecycle:
//   Enter  → writes choice_key = NoChoice (-1) to blackboard (clears previous choice)
//          → writes agis.dialogue.active_id = dialogue_id (signals game code)
//   Tick   → does nothing; waits for game code to call blackboard.Set(choice_key, optionIndex)
//   Exit   → clears agis.dialogue.active_id from blackboard
//
// Game code integration:
//   // Listen for the active_id key changing (poll or event-driven):
//   if (blackboard.TryGet<string>(AGISDialogueConstants.ActiveIdKey, out var id))
//       ShowDialogueUI(id);
//
//   // When player picks option 2:
//   blackboard.Set(AGISDialogueConstants.DefaultChoiceKey, 2);
//
//   // The state machine transitions on the next tick via matching option conditions.

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.Dialogue
{
    public sealed class AGISDialogueNodeType : IAGISNodeType
    {
        public string TypeId      => "agis.dialogue";
        public string DisplayName => "Dialogue";
        public AGISNodeKind Kind   => AGISNodeKind.Normal;

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("dialogue_id", AGISParamType.String, AGISValue.FromString(""))
                    { displayName = "Dialogue ID",
                      tooltip     = "Opaque identifier passed to game code via the blackboard " +
                                    $"(key: '{AGISDialogueConstants.ActiveIdKey}'). " +
                                    "Use it to look up dialogue content, drive UI, etc." },
                new AGISParamSpec("choice_key", AGISParamType.String,
                                  AGISValue.FromString(AGISDialogueConstants.DefaultChoiceKey))
                    { displayName = "Choice Key",
                      tooltip     = "Blackboard key where game code writes the chosen option index (int). " +
                                    "All outgoing option conditions must reference the same key." },
                new AGISParamSpec("loop", AGISParamType.Bool, AGISValue.FromBool(false))
                    { displayName = "Loop",
                      tooltip     = "When true, any player choice is cleared each tick before outgoing edges " +
                                    "are evaluated, keeping the node active indefinitely. Useful for repeating " +
                                    "terminal dialogue beats (e.g. 'I have nothing more to say.'). " +
                                    "Outgoing edges with non-dialogue conditions (e.g. a flag) still fire normally." },
            }
        };

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
        {
            string dialogueId = args.Params.GetString("dialogue_id", "");
            string choiceKey  = args.Params.GetString("choice_key",  AGISDialogueConstants.DefaultChoiceKey);
            bool   loop       = args.Params.GetBool  ("loop",        false);
            return new Runtime(args.Ctx, dialogueId, choiceKey, loop);
        }

        private sealed class Runtime : IAGISNodeRuntime
        {
            private readonly AGISExecutionContext _ctx;
            private readonly string               _dialogueId;
            private readonly string               _choiceKey;
            private readonly bool                 _loop;

            public Runtime(AGISExecutionContext ctx, string dialogueId, string choiceKey, bool loop)
            {
                _ctx        = ctx;
                _dialogueId = dialogueId;
                _choiceKey  = choiceKey;
                _loop       = loop;
            }

            public void Enter()
            {
                // Clear any choice from a previous visit to this node.
                _ctx.Blackboard.Set(_choiceKey, AGISDialogueConstants.NoChoice);

                // Signal game code which dialogue beat is active.
                _ctx.Blackboard.Set(AGISDialogueConstants.ActiveIdKey, _dialogueId);
            }

            public void Tick(float dt)
            {
                if (!_loop) return;

                // Clear any choice before outgoing edges are evaluated this tick,
                // so agis.dialogue_option conditions never fire and the node stays active.
                if (_ctx.Blackboard.TryGet<int>(_choiceKey, out int choice)
                    && choice != AGISDialogueConstants.NoChoice)
                {
                    _ctx.Blackboard.Set(_choiceKey, AGISDialogueConstants.NoChoice);
                }
            }

            public void Exit()
            {
                // Remove the active ID so game code knows no dialogue is running.
                _ctx.Blackboard.Remove(AGISDialogueConstants.ActiveIdKey);
            }
        }
    }
}
