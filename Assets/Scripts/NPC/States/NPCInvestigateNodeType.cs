// File: NPCInvestigateNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: NPC moves to the last known target position then performs a look-around before
//          signalling completion via IAGISNodeSignal.
//
// Schema Params:
//   look_radius       (Float, 2.0) — not used for pathfinding, kept for future extensions
//   look_count        (Int,   3)   — number of random Y-rotation targets to visit
//   look_duration     (Float, 1.5) — seconds to hold each look direction once reached
//   rotation_speed    (Float, 90)  — degrees/second to rotate toward each target angle
//   arrival_distance  (Float, 1.0) — distance threshold (world units) to consider "arrived"
//
// Runtime phases:
//   Navigate  — pathfind to npc.last_known_target_pos from AGISActorState; arrival triggers LookAround
//   LookAround — disable pathfinding, rotate through random Y-angles, set IsComplete when done
//
// Exit — disables pathfinding; the next state re-enables it in its own Enter() if needed.
//
// Implements IAGISNodeSignal so "agis.node_complete" edges can fire when investigation ends.
// Template graphs typically use meter-based conditions instead; IAGISNodeSignal is for custom graphs.

using System.Collections.Generic;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using UnityEngine;

namespace AGIS.NPC.States
{
    public sealed class NPCInvestigateNodeType : IAGISNodeType, IAGISNodeComponentRequirements
    {
        public string TypeId      => "npc.investigate";
        public string DisplayName => "NPC Investigate";
        public AGISNodeKind Kind   => AGISNodeKind.Normal;

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("look_radius", AGISParamType.Float, AGISValue.FromFloat(2.0f))
                    { displayName = "Look Radius",
                      tooltip     = "Reserved for future use — offset radius from the investigation point " +
                                    "when selecting look-around angles.",
                      hasMin = true, floatMin = 0f },
                new AGISParamSpec("look_count", AGISParamType.Int, AGISValue.FromInt(3))
                    { displayName = "Look Count",
                      tooltip     = "Number of random Y-rotation targets to visit during the look-around phase.",
                      hasMin = true, intMin = 1 },
                new AGISParamSpec("look_duration", AGISParamType.Float, AGISValue.FromFloat(1.5f))
                    { displayName = "Look Duration",
                      tooltip     = "Seconds to hold each look direction once the NPC has rotated to face it.",
                      hasMin = true, floatMin = 0f },
                new AGISParamSpec("rotation_speed", AGISParamType.Float, AGISValue.FromFloat(90f))
                    { displayName = "Rotation Speed",
                      tooltip     = "Degrees per second to rotate toward each look target.",
                      hasMin = true, floatMin = 1f },
                new AGISParamSpec("arrival_distance", AGISParamType.Float, AGISValue.FromFloat(1.0f))
                    { displayName = "Arrival Distance",
                      tooltip     = "World-unit distance threshold to consider the NPC arrived at the investigation point.",
                      hasMin = true, floatMin = 0.1f },
            }
        };

        public IReadOnlyList<System.Type> GetRequiredComponents(IAGISParamAccessor resolvedParams)
            => new[] { typeof(IAGISNPCPathFinder) };

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
        {
            int   lookCount       = args.Params.GetInt  ("look_count",       3);
            float lookDuration    = args.Params.GetFloat("look_duration",    1.5f);
            float rotationSpeed   = args.Params.GetFloat("rotation_speed",   90f);
            float arrivalDistance = args.Params.GetFloat("arrival_distance", 1.0f);
            return new Runtime(args.Ctx, lookCount, lookDuration, rotationSpeed, arrivalDistance);
        }

        // ─────────────────────────────────────────────────────────────────────────────

        private sealed class Runtime : IAGISNodeRuntime, IAGISNodeSignal
        {
            private enum Phase { Navigate, LookAround }

            private readonly AGISExecutionContext _ctx;
            private readonly IAGISNPCPathFinder   _pathFinder;
            private readonly AGISActorState       _actorState;
            private readonly int                  _lookCount;
            private readonly float                _lookDuration;
            private readonly float                _rotationSpeed;
            private readonly float                _arrivalDistSq;

            private Phase   _phase;
            private Vector3 _targetPos;

            // Look-around state
            private float[] _lookAngles;
            private int     _lookIndex;
            private float   _holdTimer;
            private bool    _holding;

            public bool IsComplete { get; private set; }

            public Runtime(AGISExecutionContext ctx, int lookCount, float lookDuration,
                           float rotationSpeed, float arrivalDistance)
            {
                _ctx           = ctx;
                _pathFinder    = ctx.Actor?.GetComponent<IAGISNPCPathFinder>();
                _actorState    = ctx.Actor?.GetComponent<AGISActorState>();
                _lookCount     = Mathf.Max(1, lookCount);
                _lookDuration  = lookDuration;
                _rotationSpeed = rotationSpeed;
                _arrivalDistSq = arrivalDistance * arrivalDistance;
            }

            public void Enter()
            {
                IsComplete = false;
                _phase     = Phase.Navigate;
                _holding   = false;

                _targetPos = _actorState != null
                    ? _actorState.Get("npc.last_known_target_pos").AsVector3()
                    : Vector3.zero;

                if (_pathFinder != null)
                {
                    _pathFinder.EnablePathfinding();
                    _pathFinder.SetWalkTarget(_targetPos);
                }
            }

            public void Tick(float dt)
            {
                if (IsComplete) return;

                if (_phase == Phase.Navigate)
                    TickNavigate();
                else
                    TickLookAround(dt);
            }

            public void Exit()
            {
                _pathFinder?.DisablePathfinding();
                IsComplete = false;
            }

            // ── Phase 1: Navigate ─────────────────────────────────────────────────────

            private void TickNavigate()
            {
                if (_ctx.Actor == null) return;

                Vector3 toTarget = _targetPos - _ctx.Actor.transform.position;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude <= _arrivalDistSq)
                    StartLookAround();
            }

            // ── Phase 2: Look Around ──────────────────────────────────────────────────

            private void StartLookAround()
            {
                _phase = Phase.LookAround;
                _pathFinder?.DisablePathfinding();

                float baseAngle = _ctx.Actor != null
                    ? _ctx.Actor.transform.eulerAngles.y
                    : 0f;

                _lookAngles = new float[_lookCount];
                for (int i = 0; i < _lookCount; i++)
                    _lookAngles[i] = baseAngle + Random.Range(-90f, 90f);

                _lookIndex = 0;
                _holdTimer = 0f;
                _holding   = false;
            }

            private void TickLookAround(float dt)
            {
                if (_ctx.Actor == null || _lookAngles == null) return;

                if (_lookIndex >= _lookCount)
                {
                    IsComplete = true;
                    return;
                }

                if (_holding)
                {
                    _holdTimer -= dt;
                    if (_holdTimer <= 0f)
                    {
                        _lookIndex++;
                        _holding = false;
                    }
                    return;
                }

                Transform t          = _ctx.Actor.transform;
                float     current    = t.eulerAngles.y;
                float     targetAngle = _lookAngles[_lookIndex];
                float     newAngle   = Mathf.MoveTowardsAngle(current, targetAngle, _rotationSpeed * dt);
                t.eulerAngles = new Vector3(t.eulerAngles.x, newAngle, t.eulerAngles.z);

                if (Mathf.Abs(Mathf.DeltaAngle(newAngle, targetAngle)) < 5f)
                {
                    _holding   = true;
                    _holdTimer = _lookDuration;
                }
            }
        }
    }
}
