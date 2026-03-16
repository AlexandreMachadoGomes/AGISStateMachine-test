// File: NPCAStarPathFinder.cs
// Folder: Assets/Scripts/NPC/
// Purpose: IAGISNPCPathFinder implementation that drives AIPath directly — no UCC required.
//          Used when OPSIVE_UCC is not defined. The NPC moves purely via A* AIPath.

using Pathfinding;
using UnityEngine;

namespace AGIS.NPC
{
    [DisallowMultipleComponent]
    public sealed class NPCAStarPathFinder : MonoBehaviour, IAGISNPCPathFinder
    {
        private IAstarAI  _agent;
        private Transform _trackedTarget;

        private void Awake()
        {
            _agent = GetComponent<IAstarAI>();
            if (_agent == null)
                Debug.LogError("[NPCAStarPathFinder] No IAstarAI (AIPath/RichAI) found on this GameObject.", this);
        }

        private void Update()
        {
            if (_trackedTarget != null && _agent != null)
                _agent.destination = _trackedTarget.position;
        }

        // ── IAGISNPCPathFinder ────────────────────────────────────────────────────────

        public bool    ReachedDestination  => _agent != null && (_agent.reachedEndOfPath || _agent.reachedDestination);
        public Vector3 DesiredVelocity     => _agent?.desiredVelocity ?? Vector3.zero;
        public bool    IsPathfindingActive => _agent != null && !_agent.isStopped && _agent.hasPath;

        public void EnablePathfinding()
        {
            if (_agent != null) _agent.isStopped = false;
        }

        public void DisablePathfinding()
        {
            _trackedTarget = null;
            if (_agent != null)
            {
                _agent.isStopped    = true;
                _agent.destination  = Vector3.positiveInfinity;
            }
        }

        public void SetWalkTarget(Vector3 pos)
        {
            _trackedTarget = null;
            if (_agent != null)
            {
                _agent.destination = pos;
                _agent.isStopped   = false;
            }
        }

        public void SetWalkTargetTransform(Transform target)
        {
            _trackedTarget = target;
            if (target != null && _agent != null)
            {
                _agent.destination = target.position;
                _agent.isStopped   = false;
            }
        }

        public void ClearWalkTarget()
        {
            _trackedTarget = null;
        }

        public Vector3 SnapToGraph(Vector3 pos)
        {
            if (AstarPath.active == null) return pos;
            return (Vector3)AstarPath.active.GetNearest(pos, NNConstraint.Default).position;
        }
    }
}
