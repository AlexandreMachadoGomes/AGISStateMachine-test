// File: NPCDetectionMeter.cs
// Folder: Assets/Scripts/NPC/
// Purpose: Pure config holder for the detection meter system. Tick logic has moved into
//          NPCStealthMeterNodeType (npc.stealth_meter), which runs in a dedicated "Stealth"
//          AGISStateMachineSlot. This component remains on the NPC so the runner's component
//          scan auto-populates AGISActorState with the persistent keys at startup.
//
// Inspector fields:
//   fillRate             (float, 1.0)  — units/second added while target is in the detection cone
//   drainRate            (float, 0.4)  — units/second removed while target is NOT in the cone
//   maxDetection         (float, 3.0)  — maximum meter value; reaching this means full alert
//   investigateThreshold (float, 1.0)  — informational; read by NPCDetectionMeterConditionType
//   targetTag            (string, "Player") — tag used to locate the target
//
// Persistent AGISActorState keys declared:
//   npc.detection_meter       (Float,   0)    — current meter value [0, maxDetection]
//   npc.last_known_target_pos (Vector3, zero) — last world-space position where target was seen

using System.Collections.Generic;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC.Params;
using UnityEngine;

namespace AGIS.NPC
{
    public sealed class NPCDetectionMeter : MonoBehaviour, IAGISPersistentNodeType
    {
        [Header("Detection Meter")]
        [Tooltip("Units per second added to the meter while the target is visible in the detection cone.")]
        [Min(0f)] public float fillRate = 1.0f;

        [Tooltip("Units per second subtracted from the meter while the target is NOT visible.")]
        [Min(0f)] public float drainRate = 0.4f;

        [Tooltip("Maximum meter value. When the meter reaches this value the NPC is at full alert.")]
        [Min(0.1f)] public float maxDetection = 3.0f;

        [Tooltip("Informational threshold at which the NPC should start investigating. " +
                 "Read by NPCDetectionMeterConditionType when threshold = investigateThreshold.")]
        [Min(0f)] public float investigateThreshold = 1.0f;

        [Tooltip("GameObject tag used to find the target in the scene.")]
        public string targetTag = "Player";

        // ── IAGISPersistentNodeType ───────────────────────────────────────────────────

        public IReadOnlyList<AGISParamSpec> PersistentParams { get; } = new AGISParamSpec[]
        {
            new AGISParamSpec("npc.detection_meter", AGISParamType.Float, AGISValue.FromFloat(0f))
                { displayName = "Detection Meter",
                  tooltip     = "Current suspicion level [0, maxDetection]. " +
                                "Rises while the target is in the detection cone; drains otherwise." },
            new AGISParamSpec("npc.last_known_target_pos", AGISParamType.Vector3, AGISValue.FromVector3(Vector3.zero))
                { displayName = "Last Known Target Position",
                  tooltip     = "World-space position where the target was last seen inside the detection cone. " +
                                "Only updated while the target is detected — persists after detection ends." },
        };
    }
}
