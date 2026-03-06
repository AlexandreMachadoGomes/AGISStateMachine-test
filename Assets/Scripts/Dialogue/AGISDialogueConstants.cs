// File: AGISDialogueConstants.cs
// Folder: Assets/Scripts/Dialogue/
// Purpose: Shared keys and sentinel values used across all dialogue node types and conditions.
//          Keeping these in one place avoids key-string mismatches.

namespace AGIS.Dialogue
{
    public static class AGISDialogueConstants
    {
        /// <summary>
        /// Default blackboard key where the player's chosen option index is written.
        /// Value type: int. Written by game code; read by AGISDialogueOptionConditionType.
        /// </summary>
        public const string DefaultChoiceKey = "agis.dialogue.choice";

        /// <summary>
        /// Blackboard key written by AGISDialogueNodeType on Enter with the active dialogue_id.
        /// Cleared on Exit. Game code reads this to know which dialogue to display.
        /// </summary>
        public const string ActiveIdKey = "agis.dialogue.active_id";

        /// <summary>
        /// Sentinel written to the choice key on Enter to signal "no choice made yet".
        /// Game code writes any value >= 0 to register a choice.
        /// </summary>
        public const int NoChoice = -1;
    }
}
