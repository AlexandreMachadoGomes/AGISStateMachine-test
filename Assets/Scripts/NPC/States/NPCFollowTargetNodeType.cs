// File: NPCFollowTargetNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: NPC moves toward a target. By default follows the GameObject tagged "Player".
//          Can follow any GameObject stored on the blackboard under target_key instead.
//
// Optional detection memory:
//   When use_detection_memory = true the node requires NPCDetectionCone on the actor.
//   Each tick it checks whether the target is still within the detection volume.
//   If yes: npc.target_time_lost is reset to 0.
//   If no:  npc.target_time_lost accumulates dt.
//   Pair this with NPCHasLostTargetConditionType on an exit edge to stop pursuit
//   after the target has been out of detection for longer than a configurable timeout.
//
// Optional pursuit cone expansion:
//   pursuit_range_bonus and pursuit_angle_bonus widen the NPCDetectionCone while this
//   node is active. On Enter the override is applied; on Exit it is cleared, restoring
//   the inspector values. Works independently of use_detection_memory.
//
// Params:
//   follow_player          (Bool,   default true)  — find "Player"-tagged object when true
//   target_key             (String, default "")    — blackboard key for a GameObject target
//   use_detection_memory   (Bool,   default false) — enable lost-target timer via NPCDetectionCone
//   pursuit_range_bonus    (Float,  default 0)     — extra detection range while in pursuit
//   pursuit_angle_bonus    (Float,  default 0)     — extra cone angle (degrees) while in pursuit

using System;
using System.Collections.Generic;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using Pathfinding;
using UnityEngine;

namespace AGIS.NPC.States
{
    public sealed class NPCFollowTargetNodeType : IAGISNodeType, IAGISPersistentNodeType, IAGISNodeComponentRequirements
    {
        public string TypeId      => "npc.follow_target";
        public string DisplayName => "NPC Follow Target";
        public AGISNodeKind Kind   => AGISNodeKind.Normal;

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("follow_player", AGISParamType.Bool,   AGISValue.FromBool(true))
                    { displayName = "Follow Player",
                      tooltip     = "When true, follows the GameObject tagged 'Player'." },
                new AGISParamSpec("target_key",    AGISParamType.String, AGISValue.FromString(""))
                    { displayName = "Target Key",
                      tooltip     = "Blackboard key for a GameObject target (used when Follow Player is false)." },
                new AGISParamSpec("use_detection_memory", AGISParamType.Bool, AGISValue.FromBool(false))
                    { displayName = "Use Detection Memory",
                      tooltip     = "When true, tracks how long the target has been outside the detection " +
                                    "volume each tick. Use NPCHasLostTargetConditionType on an exit edge " +
                                    "to end pursuit after a configurable timeout." },
                new AGISParamSpec("pursuit_range_bonus", AGISParamType.Float, AGISValue.FromFloat(0f))
                    { displayName = "Pursuit Range Bonus",
                      tooltip     = "Extra detection range added to NPCDetectionCone while this node is active. " +
                                    "Cleared automatically on Exit.",
                      hasMin = true, floatMin = 0f },
                new AGISParamSpec("pursuit_angle_bonus", AGISParamType.Float, AGISValue.FromFloat(0f))
                    { displayName = "Pursuit Angle Bonus",
                      tooltip     = "Extra cone angle in degrees added to NPCDetectionCone while this node is active. " +
                                    "Cleared automatically on Exit.",
                      hasMin = true, floatMin = 0f },
            }
        };

        public IReadOnlyList<AGISParamSpec> PersistentParams { get; } = new AGISParamSpec[]
        {
            new AGISParamSpec("npc.target_time_lost", AGISParamType.Float, AGISValue.FromFloat(0f))
                { displayName = "Target Time Lost",
                  tooltip     = "Seconds the target has continuously been outside the detection volume. " +
                                "Reset to 0 each tick the target is detected. " +
                                "Only updated when use_detection_memory = true." },
        };

        // ── IAGISNodeComponentRequirements ────────────────────────────────────────────

        public IReadOnlyList<Type> GetRequiredComponents(IAGISParamAccessor resolvedParams)
        {
            if (!resolvedParams.GetBool("use_detection_memory", false))
                return new[] { typeof(AIPath), typeof(Seeker), typeof(AIDestinationSetter) };

            return new[] { typeof(AIPath), typeof(Seeker), typeof(AIDestinationSetter), typeof(NPCDetectionCone) };
        }

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
        {
            bool   followPlayer       = args.Params.GetBool  ("follow_player",        true);
            string targetKey          = args.Params.GetString("target_key",           "");
            bool   useDetectionMemory = args.Params.GetBool  ("use_detection_memory", false);
            float  rangeBonus         = args.Params.GetFloat ("pursuit_range_bonus",  0f);
            float  angleBonus         = args.Params.GetFloat ("pursuit_angle_bonus",  0f);
            return new Runtime(args.Ctx, followPlayer, targetKey, useDetectionMemory, rangeBonus, angleBonus);
        }

        private sealed class Runtime : IAGISNodeRuntime
        {
            private readonly AGISExecutionContext _ctx;
            private readonly AIPath               _aiPath;
            private readonly Seeker               _seeker;
            private readonly AIDestinationSetter  _destSetter;
            private readonly NPCDetectionCone     _detection;
            private readonly AGISActorState       _actorState;
            private readonly bool                 _followPlayer;
            private readonly string               _targetKey;
            private readonly bool                 _useMemory;
            private readonly float                _rangeBonus;
            private readonly float                _angleBonus;

            public Runtime(AGISExecutionContext ctx, bool followPlayer, string targetKey,
                           bool useMemory, float rangeBonus, float angleBonus)
            {
                _ctx          = ctx;
                _followPlayer = followPlayer;
                _targetKey    = targetKey;
                _useMemory    = useMemory;
                _rangeBonus   = rangeBonus;
                _angleBonus   = angleBonus;
                _aiPath       = ctx.Actor?.GetComponent<AIPath>();
                _seeker       = ctx.Actor?.GetComponent<Seeker>();
                _destSetter   = ctx.Actor?.GetComponent<AIDestinationSetter>();
                _detection    = ctx.Actor?.GetComponent<NPCDetectionCone>();
                _actorState   = useMemory ? ctx.Actor?.GetComponent<AGISActorState>() : null;
            }

            public void Enter()
            {
                if (_aiPath == null) return;

                _aiPath.enabled     = true;
                _seeker.enabled     = true;
                _destSetter.enabled = true;

                var target = ResolveTarget();
                if (target != null && _destSetter != null)
                    _destSetter.target = target.transform;

                if (_useMemory && _actorState != null)
                    _actorState.Set("npc.target_time_lost", AGISValue.FromFloat(0f));

                if (_detection != null && (_rangeBonus > 0f || _angleBonus > 0f))
                    _detection.SetPursuitOverride(_detection.range + _rangeBonus,
                                                  _detection.angle + _angleBonus);
            }

            public void Tick(float dt)
            {
                if (!_useMemory || _actorState == null) return;

                var target = ResolveTarget();

                if (_detection == null)
                {
                    _actorState.Set("npc.target_time_lost", AGISValue.FromFloat(0f));
                    return;
                }

                bool canSee = target != null && _detection.IsDetected(target.transform);

                if (canSee)
                    _actorState.Set("npc.target_time_lost", AGISValue.FromFloat(0f));
                else
                {
                    float lost = _actorState.GetFloat("npc.target_time_lost");
                    _actorState.Set("npc.target_time_lost", AGISValue.FromFloat(lost + dt));
                }
            }

            public void Exit()
            {
                _detection?.ClearPursuitOverride();
            }

            private GameObject ResolveTarget()
            {
                if (_followPlayer)
                    return GameObject.FindWithTag("Player");

                if (!string.IsNullOrEmpty(_targetKey))
                {
                    _ctx.Blackboard.TryGet<GameObject>(_targetKey, out var t);
                    return t;
                }

                return null;
            }
        }
    }
}
