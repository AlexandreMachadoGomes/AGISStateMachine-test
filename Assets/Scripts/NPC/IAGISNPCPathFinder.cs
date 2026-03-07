// File: IAGISNPCPathFinder.cs
// Folder: Assets/Scripts/NPC/
// Purpose: Abstraction over whatever pathfinding + locomotion bridge is in use.
//          All NPC movement states and conditions talk to this interface only —
//          no direct references to A* or UCC types.

using UnityEngine;

namespace AGIS.NPC
{
    public interface IAGISNPCPathFinder
    {
        bool    ReachedDestination  { get; }   // → AStarAIAgentMovement.HasArrived
        Vector3 DesiredVelocity     { get; }   // → IAstarAI.desiredVelocity
        bool    IsPathfindingActive { get; }   // → AStarAIAgentMovement.IsActive

        void EnablePathfinding();                        // locomotion.TryStartAbility(ability)
        void DisablePathfinding();                       // locomotion.TryStopAbility(ability) + clear tracking

        void SetWalkTarget(Vector3 worldPosition);       // ability.SetDestination(pos)
        void SetWalkTargetTransform(Transform target);   // stores ref; Update() calls SetDestination each frame
        void ClearWalkTarget();                          // clears tracked transform; keeps ability running

        Vector3 SnapToGraph(Vector3 worldPosition);      // AstarPath.active.GetNearest(pos).position
    }
}
