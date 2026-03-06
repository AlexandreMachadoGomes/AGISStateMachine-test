// File: NPCDetectionMeterConditionType.cs
// Folder: Assets/Scripts/NPC/Conditions/
// Purpose: True when the NPC's npc.detection_meter AGISActorState value meets or exceeds
//          a configured threshold.
//
// Params:
//   threshold         (Float, 1.0)  — comparison value; ignored when use_max_detection = true
//   use_max_detection (Bool,  false) — when true, reads maxDetection from the NPCDetectionMeter
//                                      component on the actor (avoids hardcoding the full-alert value)
//
// Returns false when the actor, AGISActorState, or (in use_max mode) NPCDetectionMeter is absent.

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using UnityEngine;

namespace AGIS.NPC.Conditions
{
    public sealed class NPCDetectionMeterConditionType : IAGISConditionType
    {
        public string TypeId      => "npc.detection_meter_exceeds";
        public string DisplayName => "NPC Detection Meter Exceeds";

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("threshold", AGISParamType.Float, AGISValue.FromFloat(1.0f))
                    { displayName = "Threshold",
                      tooltip     = "Meter value that must be reached for this condition to return true. " +
                                    "Ignored when Use Max Detection is enabled.",
                      hasMin = true, floatMin = 0f },
                new AGISParamSpec("use_max_detection", AGISParamType.Bool, AGISValue.FromBool(false))
                    { displayName = "Use Max Detection",
                      tooltip     = "When true, the threshold is taken from the maxDetection field of the " +
                                    "NPCDetectionMeter component on the actor rather than the Threshold param. " +
                                    "Use this to express 'meter is full / NPC is at full alert'." },
            }
        };

        public bool Evaluate(in AGISConditionEvalArgs args)
        {
            var actor = args.Ctx.Actor;
            if (actor == null) return false;

            var actorState = actor.GetComponent<AGISActorState>();
            if (actorState == null) return false;

            float meter = actorState.GetFloat("npc.detection_meter");

            bool  useMax = args.Params.GetBool("use_max_detection", false);
            float threshold;

            if (useMax)
            {
                var meterComp = actor.GetComponent<NPCDetectionMeter>();
                if (meterComp == null) return false;
                threshold = meterComp.maxDetection;
            }
            else
            {
                threshold = args.Params.GetFloat("threshold", 1.0f);
            }

            return meter >= threshold;
        }
    }
}
