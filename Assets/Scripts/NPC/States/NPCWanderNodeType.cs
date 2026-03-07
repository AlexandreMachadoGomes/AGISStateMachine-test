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
            => new[] { typeof(IAGISNPCPathFinder) };

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
        {
            float radius    = args.Params.GetFloat("wander_radius", 10f);
            float pauseTime = args.Params.GetFloat("pause_time", 0.5f);
            return new Runtime(args.Ctx, radius, pauseTime);
        }

        private sealed class Runtime : IAGISNodeRuntime
        {
            private readonly AGISExecutionContext _ctx;
            private readonly IAGISNPCPathFinder   _pathFinder;
            private readonly float                _radius;
            private readonly float                _pauseTime;

            private float _pauseTimer;
            private bool  _waiting;

            public Runtime(AGISExecutionContext ctx, float radius, float pauseTime)
            {
                _ctx        = ctx;
                _pathFinder = ctx.Actor?.GetComponent<IAGISNPCPathFinder>();
                _radius     = radius;
                _pauseTime  = pauseTime;
            }

            public void Enter()
            {
                if (_pathFinder == null) return;

                _pathFinder.EnablePathfinding();
                _waiting    = false;
                _pauseTimer = 0f;
                PickNewDestination();
            }

            public void Tick(float dt)
            {
                if (_pathFinder == null) return;

                if (_waiting)
                {
                    _pauseTimer -= dt;
                    if (_pauseTimer <= 0f) { _waiting = false; PickNewDestination(); }
                    return;
                }

                if (_pathFinder.ReachedDestination)
                {
                    _waiting    = true;
                    _pauseTimer = _pauseTime;
                }
            }

            public void Exit() => _pathFinder?.DisablePathfinding();

            private void PickNewDestination()
            {
                if (_pathFinder == null || _ctx.Actor == null) return;

                Vector3 origin = _ctx.Actor.transform.position;
                Vector2 rand2D = UnityEngine.Random.insideUnitCircle * _radius;
                Vector3 candidate = new Vector3(origin.x + rand2D.x, origin.y, origin.z + rand2D.y);

                _pathFinder.SetWalkTarget(_pathFinder.SnapToGraph(candidate));
            }
        }
    }
}
