// File: NPCUCCPathFinder.cs
// Folder: Assets/Scripts/NPC/
// Purpose: IAGISNPCPathFinder implementation that delegates to AStarAIAgentMovement,
//          the official A*/UCC integration ability. No #if guard — UCC is always present.
//
// Pre-requisites (must be done in inspector / CharacterBuilder before UpgradeEnemy() runs):
//   1. UltimateCharacterLocomotion must be configured on the prefab.
//   2. AStarAIAgentMovement ability must be added to UltimateCharacterLocomotion.Abilities[]
//      (concurrent ability — add via the UCC Character Manager in inspector).
//   3. AIPath (or RichAI) must be on the same GameObject — provides IAstarAI.
//   4. OPSIVE_UCC scripting define must be set in Player Settings.
//
//   NPCUCCPathFinder.Awake() will log a clear error if any of the above is missing.

using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Integrations.AstarPathfindingProject;
using Pathfinding;
using UnityEngine;

namespace AGIS.NPC
{
    [DisallowMultipleComponent]
    public sealed class NPCUCCPathFinder : MonoBehaviour, IAGISNPCPathFinder
    {
        private IAstarAI                  _agent;          // AIPath or RichAI on this GO
        private UltimateCharacterLocomotion _locomotion;
        private AStarAIAgentMovement       _ability;
        private Transform                  _trackedTarget;  // live-target for SetWalkTargetTransform

        private void Awake()
        {
            _agent      = GetComponent<IAstarAI>();
            _locomotion = GetComponent<UltimateCharacterLocomotion>();
            _ability    = _locomotion?.GetAbility<AStarAIAgentMovement>();

            if (_agent   == null) Debug.LogError("[NPCUCCPathFinder] No IAstarAI found.", this);
            if (_ability == null) Debug.LogError(
                "[NPCUCCPathFinder] AStarAIAgentMovement ability not found on " +
                "UltimateCharacterLocomotion. Add it in the UCC inspector.", this);
        }

        private void Update()
        {
            // Live-track a moving transform (e.g. following the player).
            if (_trackedTarget != null && _ability != null)
                _ability.SetDestination(_trackedTarget.position);
        }

        // ── IAGISNPCPathFinder ──────────────────────────────────────────────────────

        public bool    ReachedDestination  => _ability != null && _ability.HasArrived;
        public Vector3 DesiredVelocity     => _agent?.desiredVelocity ?? Vector3.zero;
        public bool    IsPathfindingActive => _ability != null && _ability.IsActive;

        public void EnablePathfinding()
        {
            if (_locomotion != null && _ability != null)
                _locomotion.TryStartAbility(_ability);
        }

        public void DisablePathfinding()
        {
            _trackedTarget = null;
            if (_locomotion != null && _ability != null)
                _locomotion.TryStopAbility(_ability);
        }

        public void SetWalkTarget(Vector3 pos)
        {
            _trackedTarget = null;
            _ability?.SetDestination(pos);
        }

        public void SetWalkTargetTransform(Transform target)
        {
            _trackedTarget = target;
            if (target != null) _ability?.SetDestination(target.position);
        }

        public void ClearWalkTarget() => _trackedTarget = null;

        public Vector3 SnapToGraph(Vector3 pos)
        {
            if (AstarPath.active == null) return pos;
            return (Vector3)AstarPath.active.GetNearest(pos, NNConstraint.Default).position;
        }
    }
}
