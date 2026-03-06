// File: NPCDetectsObjectConditionType.cs
// Folder: Assets/Scripts/NPC/Conditions/
// Purpose: True when NPCDetectionCone detects an object satisfying the configured target rule.
//
// Three detection modes (mutually exclusive, evaluated in order):
//   1. use_player = true         — checks if the "Player"-tagged object is in the cone
//   2. target_key != ""          — checks if the GameObject at that blackboard key is in the cone
//   3. (fallback)                — DetectAll() from the cone's LayerMask; true if anything found
//
// Params:
//   use_player    (Bool,   default false) — target the Player-tagged object
//   target_key    (String, default "")   — blackboard key to a specific target GameObject
//   detected_key  (String, default "")   — if non-empty, writes the detected GameObject to
//                                          the blackboard under this key (overwritten each tick)
//
// Requires NPCDetectionCone on the actor. Returns false if the component is absent.

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using UnityEngine;

namespace AGIS.NPC.Conditions
{
    public sealed class NPCDetectsObjectConditionType : IAGISConditionType
    {
        public string TypeId      => "npc.detects_object";
        public string DisplayName => "NPC Detects Object";

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("use_player",   AGISParamType.Bool,   AGISValue.FromBool(false))
                    { displayName = "Use Player",
                      tooltip     = "When true, checks detection specifically against the GameObject tagged 'Player'." },
                new AGISParamSpec("target_key",   AGISParamType.String, AGISValue.FromString(""))
                    { displayName = "Target Key",
                      tooltip     = "Blackboard key for a specific target GameObject (used when Use Player is false)." },
                new AGISParamSpec("detected_key", AGISParamType.String, AGISValue.FromString(""))
                    { displayName = "Detected Key",
                      tooltip     = "If non-empty, the detected GameObject is written to this blackboard key each tick." },
            }
        };

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            var actor = args.Ctx.Actor;
            if (actor == null) return false;

            var cone = actor.GetComponent<NPCDetectionCone>();
            if (cone == null) return false;

            bool   usePlayer   = args.Params.GetBool  ("use_player",   false);
            string targetKey   = args.Params.GetString ("target_key",   "");
            string detectedKey = args.Params.GetString ("detected_key", "");

            // ── Mode 1: check the Player ──────────────────────────────────────────────
            if (usePlayer)
            {
                var player = GameObject.FindWithTag("Player");
                if (player == null) return false;

                bool detected = cone.IsDetected(player.transform);
                if (detected && !string.IsNullOrEmpty(detectedKey))
                    args.Ctx.Blackboard.Set(detectedKey, player);
                return detected;
            }

            // ── Mode 2: check a specific blackboard target ────────────────────────────
            if (!string.IsNullOrEmpty(targetKey))
            {
                args.Ctx.Blackboard.TryGet<GameObject>(targetKey, out var target);
                if (target == null) return false;

                bool detected = cone.IsDetected(target.transform);
                if (detected && !string.IsNullOrEmpty(detectedKey))
                    args.Ctx.Blackboard.Set(detectedKey, target);
                return detected;
            }

            // ── Mode 3: detect any object in the cone's LayerMask ────────────────────
            var closest = cone.DetectClosest();
            if (closest == null) return false;

            if (!string.IsNullOrEmpty(detectedKey))
                args.Ctx.Blackboard.Set(detectedKey, closest.gameObject);
            return true;
        }
    }
}
