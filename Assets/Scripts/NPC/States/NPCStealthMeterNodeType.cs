// File: NPCStealthMeterNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: ESM-native stealth slot node. Runs forever as the sole node in a dedicated
//          "Stealth" AGISStateMachineSlot on the NPC runner. Each Tick(dt) fills or
//          drains npc.detection_meter in AGISActorState based on whether the player is
//          inside the NPCDetectionCone. The main "Patrol" slot reads the meter value
//          via NPCDetectionMeterConditionType — no changes to conditions or edges needed.
//
// TypeId: npc.stealth_meter
//
// Persistent AGISActorState keys declared (matches NPCDetectionMeter's declaration —
// EnsureKey is idempotent so both can co-exist safely):
//   npc.detection_meter       (Float,   0)    — current suspicion level [0, maxDetection]
//   npc.last_known_target_pos (Vector3, zero) — last seen world-space position of target

using System.Collections.Generic;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using UnityEngine;

namespace AGIS.NPC.States
{
    public sealed class NPCStealthMeterNodeType : IAGISNodeType, IAGISPersistentNodeType
    {
        public string TypeId      => "npc.stealth_meter";
        public string DisplayName => "NPC Stealth Meter";
        public AGISNodeKind Kind  => AGISNodeKind.Normal;

        // No schema params — all config lives on the NPCDetectionMeter MonoBehaviour.
        public AGISParamSchema Schema { get; } = new AGISParamSchema();

        public IReadOnlyList<AGISParamSpec> PersistentParams { get; } = new AGISParamSpec[]
        {
            new AGISParamSpec("npc.detection_meter", AGISParamType.Float, AGISValue.FromFloat(0f))
                { displayName = "Detection Meter",
                  tooltip     = "Current suspicion level [0, maxDetection]. " +
                                "Rises while the target is in the detection cone; drains otherwise." },
            new AGISParamSpec("npc.last_known_target_pos", AGISParamType.Vector3, AGISValue.FromVector3(Vector3.zero))
                { displayName = "Last Known Target Position",
                  tooltip     = "World-space position where the target was last seen inside the detection cone." },
        };

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
        {
            var cone   = args.Ctx.Actor?.GetComponent<NPCDetectionCone>();
            var config = args.Ctx.Actor?.GetComponent<NPCDetectionMeter>();
            var state  = args.Ctx.Actor?.GetComponent<AGISActorState>();
            return new Runtime(cone, config, state);
        }

        // ── Runtime ───────────────────────────────────────────────────────────────────

        private sealed class Runtime : IAGISNodeRuntime
        {
            private readonly NPCDetectionCone  _cone;
            private readonly NPCDetectionMeter _config;
            private readonly AGISActorState    _actorState;

            public Runtime(NPCDetectionCone cone, NPCDetectionMeter config, AGISActorState actorState)
            {
                _cone       = cone;
                _config     = config;
                _actorState = actorState;
            }

            // This node is permanent — Enter/Exit are no-ops.
            public void Enter() { }
            public void Exit()  { }

            public void Tick(float dt)
            {
                if (_actorState == null || _config == null) return;

                var   target   = GameObject.FindWithTag(_config.targetTag);
                float meter    = _actorState.GetFloat("npc.detection_meter");

                bool detected = target != null && _cone != null && _cone.IsDetected(target.transform);

                if (detected)
                {
                    meter = Mathf.Min(meter + _config.fillRate * dt, _config.maxDetection);
                    _actorState.Set("npc.last_known_target_pos",
                        AGISValue.FromVector3(target.transform.position));
                }
                else
                {
                    meter = Mathf.Max(meter - _config.drainRate * dt, 0f);
                }

                _actorState.Set("npc.detection_meter", AGISValue.FromFloat(meter));
            }
        }
    }
}
