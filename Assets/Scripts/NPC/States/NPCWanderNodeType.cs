// File: NPCWanderNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: NPC roams randomly within a radius. When the destination is reached it waits
//          briefly then picks a new random point.
//
// Params:
//   wander_radius  (Float, default 10)  — max distance from the NPC to pick a new point
//   pause_time     (Float, default 0.5) — seconds to wait at each destination before picking next

using System.Collections.Generic;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using Pathfinding;
using UnityEngine;

namespace AGIS.NPC.States
{
    public sealed class NPCWanderNodeType : IAGISNodeType, IAGISNodeComponentRequirements
    {
        public string TypeId => "npc.wander";
        public string DisplayName => "NPC Wander";
        public AGISNodeKind Kind => AGISNodeKind.Normal;

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("wander_radius", AGISParamType.Float, AGISValue.FromFloat(10f))
                    { displayName = "Wander Radius", hasMin = true, floatMin = 0.5f },
                new AGISParamSpec("pause_time",    AGISParamType.Float, AGISValue.FromFloat(0.5f))
                    { displayName = "Pause Time",   hasMin = true, floatMin = 0f },
            }
        };

        public IReadOnlyList<System.Type> GetRequiredComponents(IAGISParamAccessor resolvedParams)
            => new[] { typeof(AIPath), typeof(Seeker), typeof(AIDestinationSetter) };

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
        {
            float radius    = args.Params.GetFloat("wander_radius", 10f);
            float pauseTime = args.Params.GetFloat("pause_time", 0.5f);
            return new Runtime(args.Ctx, radius, pauseTime);
        }

        private sealed class Runtime : IAGISNodeRuntime
        {
            private readonly AGISExecutionContext _ctx;
            private readonly AIPath               _aiPath;
            private readonly Seeker               _seeker;
            private readonly AIDestinationSetter  _destSetter;
            private readonly float                _radius;
            private readonly float                _pauseTime;
            private readonly GameObject           _wanderTarget;

            private float _pauseTimer;
            private bool  _waiting;

            public Runtime(AGISExecutionContext ctx, float radius, float pauseTime)
            {
                _ctx        = ctx;
                _aiPath     = ctx.Actor?.GetComponent<AIPath>();
                _seeker     = ctx.Actor?.GetComponent<Seeker>();
                _destSetter = ctx.Actor?.GetComponent<AIDestinationSetter>();
                _radius     = radius;
                _pauseTime  = pauseTime;

                if (ctx.Actor != null)
                {
                    var go = new GameObject("WanderTarget_temp");
                    go.transform.position = ctx.Actor.transform.position;
                    _wanderTarget = go;
                }
            }

            public void Enter()
            {
                if (_aiPath == null) return;

                _aiPath.enabled     = true;
                _seeker.enabled     = true;
                _destSetter.enabled = true;

                _waiting    = false;
                _pauseTimer = 0f;

                if (_wanderTarget != null)
                {
                    if (_destSetter != null) _destSetter.target = _wanderTarget.transform;
                    PickNewDestination();
                }
            }

            public void Tick(float dt)
            {
                if (_aiPath == null || !_aiPath.enabled) return;

                if (_waiting)
                {
                    _pauseTimer -= dt;
                    if (_pauseTimer <= 0f) { _waiting = false; PickNewDestination(); }
                    return;
                }

                if (_aiPath.reachedDestination)
                {
                    _waiting    = true;
                    _pauseTimer = _pauseTime;
                }
            }

            public void Exit()
            {
                if (_wanderTarget != null)
                    UnityEngine.Object.Destroy(_wanderTarget);
            }

            private void PickNewDestination()
            {
                if (_wanderTarget == null || _ctx.Actor == null) return;

                Vector3 origin = _ctx.Actor.transform.position;
                Vector2 rand2D = UnityEngine.Random.insideUnitCircle * _radius;
                Vector3 candidate = new Vector3(origin.x + rand2D.x, origin.y, origin.z + rand2D.y);

                if (AstarPath.active != null)
                    candidate = AstarPath.active.GetNearest(candidate, NNConstraint.Default).position;

                _wanderTarget.transform.position = candidate;
            }
        }
    }
}
