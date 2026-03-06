// File: NPCIsWithinDistanceConditionType.cs
// Folder: Assets/Scripts/NPC/Conditions/
// Purpose: True when the NPC is within `distance` units of a target.
//          Finds the player via GameObject.FindWithTag("Player") when use_player is true.
//          Can instead read a GameObject from the blackboard via target_key.
//
// Params:
//   distance      (Float, default 5)    — radius threshold in world units
//   use_player    (Bool,  default true) — measure to the "Player" tagged object when true
//   target_key    (String, default "")  — blackboard key for a GameObject (when use_player false)

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using UnityEngine;

namespace AGIS.NPC.Conditions
{
    public sealed class NPCIsWithinDistanceConditionType : IAGISConditionType
    {
        public string TypeId => "npc.is_within_distance";
        public string DisplayName => "NPC Is Within Distance";

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("distance",   AGISParamType.Float,  AGISValue.FromFloat(5f))
                    { displayName = "Distance",   hasMin = true, floatMin = 0f },
                new AGISParamSpec("use_player",  AGISParamType.Bool,   AGISValue.FromBool(true))
                    { displayName = "Use Player",
                      tooltip = "When true, measures distance to the GameObject tagged 'Player'." },
                new AGISParamSpec("target_key",  AGISParamType.String, AGISValue.FromString(""))
                    { displayName = "Target Key",
                      tooltip = "Blackboard key for a GameObject (used when Use Player is false)." },
            }
        };

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            if (args.Ctx.Actor == null) return false;

            float  threshold = args.Params.GetFloat("distance", 5f);
            bool   usePlayer = args.Params.GetBool("use_player", true);
            string key       = args.Params.GetString("target_key", "");

            GameObject target = null;

            if (usePlayer)
            {
                target = GameObject.FindWithTag("Player");
            }
            else if (!string.IsNullOrEmpty(key))
            {
                args.Ctx.Blackboard.TryGet<GameObject>(key, out target);
            }

            if (target == null) return false;

            float sqrDist = (args.Ctx.Actor.transform.position - target.transform.position).sqrMagnitude;
            return sqrDist <= threshold * threshold;
        }
    }
}
