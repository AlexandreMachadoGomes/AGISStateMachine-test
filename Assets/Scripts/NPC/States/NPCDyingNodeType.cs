// File: NPCDyingNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: Triggers a Die ability via UCC (Opsive Ultimate Character Controller), disables
//          pathfinding, and signals completion once the ability finishes. After completion an
//          "agis.node_complete" edge should lead to a terminal dead-idle node with no outgoing edges.
//
// On Enter:
//   • Disables pathfinding so the NPC stops moving immediately.
//   • Writes death_flag_key = true to AGISActorState (persistent — survives state transitions).
//     This allows conditions on other edges to guard against further interrupts.
//   • Clears damage_flag_key from the blackboard so the AnyState→TakeDamage interrupt
//     does not re-fire mid-animation.
//   • Starts the Die UCC ability by type.
//
// IsComplete:
//   • Becomes true when the ability is no longer active.
//   • If no UltimateCharacterLocomotion (or no OPSIVE_UCC define), IsComplete fires immediately.
//
// Params:
//   death_flag_key   (String, default "npc.is_dead")    — AGISActorState key set true on Enter
//   damage_flag_key  (String, default "npc.is_damaged") — blackboard key cleared on Enter
//
// The Die UCC ability is resolved by type (GetAbility<Die>()) — no inspector index required.
// Ability order in the UCC ability list does not affect this node.
//
// Guarded by #if OPSIVE_UCC. Without the define the node registers as a shell that completes
// immediately. Persistent param npc.is_dead is always declared regardless of the define.

using System;
using System.Collections.Generic;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using UnityEngine;

#if OPSIVE_UCC
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
#endif

namespace AGIS.NPC.States
{
    public sealed class NPCDyingNodeType : IAGISNodeType, IAGISPersistentNodeType, IAGISNodeComponentRequirements
    {
        public string TypeId      => "npc.dying";
        public string DisplayName => "NPC Dying";
        public AGISNodeKind Kind   => AGISNodeKind.Normal;

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("death_flag_key", AGISParamType.String, AGISValue.FromString("npc.is_dead"))
                    { displayName = "Death Flag Key",
                      tooltip     = "AGISActorState key written true on Enter. Used by other conditions " +
                                    "(e.g. to block the TakeDamage interrupt while dead). Leave empty to skip." },
                new AGISParamSpec("damage_flag_key", AGISParamType.String, AGISValue.FromString("npc.is_damaged"))
                    { displayName = "Damage Flag Key",
                      tooltip     = "Blackboard key cleared on Enter so the AnyState TakeDamage interrupt " +
                                    "does not re-fire mid-animation. Leave empty to skip." },
            }
        };

        // npc.is_dead persists across state transitions so the NPC stays dead
        // even if the state machine re-enters other nodes (e.g. via AnyState edges).
        public IReadOnlyList<AGISParamSpec> PersistentParams { get; } = new AGISParamSpec[]
        {
            new AGISParamSpec("npc.is_dead", AGISParamType.Bool, AGISValue.FromBool(false))
                { displayName = "Is Dead",
                  tooltip     = "Set true by NPCDyingNodeType on Enter. Persists in AGISActorState. " +
                                "Use npc.actor_state_bool to guard edges that should not fire after death." },
        };

        public IReadOnlyList<Type> GetRequiredComponents(IAGISParamAccessor resolvedParams) =>
#if OPSIVE_UCC
            new[] { typeof(UltimateCharacterLocomotion) };
#else
            Array.Empty<Type>();
#endif

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
        {
            string deathFlagKey  = args.Params.GetString("death_flag_key",  "npc.is_dead");
            string damageFlagKey = args.Params.GetString("damage_flag_key", "npc.is_damaged");

#if OPSIVE_UCC
            return new UCCRuntime(args.Ctx, deathFlagKey, damageFlagKey);
#else
            return new NoOpRuntime(args.Ctx, deathFlagKey, damageFlagKey);
#endif
        }

        // ─────────────────────────────────────────────────────────────────────
        // Shared pathfinding disable helper (used by both runtimes).
        // ─────────────────────────────────────────────────────────────────────
        private static void DisablePathfinding(GameObject actor)
        {
            if (actor == null) return;
            actor.GetComponent<IAGISNPCPathFinder>()?.DisablePathfinding();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Shell runtime used when OPSIVE_UCC is not defined.
        // ─────────────────────────────────────────────────────────────────────
        private sealed class NoOpRuntime : IAGISNodeRuntime, IAGISNodeSignal
        {
            private readonly GameObject      _actor;
            private readonly IAGISBlackboard _blackboard;
            private readonly AGISActorState  _actorState;
            private readonly string          _deathFlagKey;
            private readonly string          _damageFlagKey;

            public bool IsComplete { get; private set; }

            public NoOpRuntime(AGISExecutionContext ctx, string deathFlagKey, string damageFlagKey)
            {
                _actor         = ctx.Actor;
                _blackboard    = ctx.Blackboard;
                _actorState    = ctx.Actor?.GetComponent<AGISActorState>();
                _deathFlagKey  = deathFlagKey;
                _damageFlagKey = damageFlagKey;
            }

            public void Enter()
            {
                IsComplete = false;
                DisablePathfinding(_actor);

                if (!string.IsNullOrEmpty(_deathFlagKey) && _actorState != null)
                    _actorState.Set(_deathFlagKey, AGISValue.FromBool(true));

                if (!string.IsNullOrEmpty(_damageFlagKey))
                    _blackboard?.Set(_damageFlagKey, false);
            }

            public void Tick(float dt) => IsComplete = true;

            public void Exit() => IsComplete = false;
        }

#if OPSIVE_UCC
        // ─────────────────────────────────────────────────────────────────────
        // UCC runtime — starts the ability by type, polls IsActive for completion.
        // ─────────────────────────────────────────────────────────────────────
        private sealed class UCCRuntime : IAGISNodeRuntime, IAGISNodeSignal
        {
            private readonly GameObject                  _actor;
            private readonly UltimateCharacterLocomotion _locomotion;
            private readonly IAGISBlackboard             _blackboard;
            private readonly AGISActorState              _actorState;
            private readonly string                      _deathFlagKey;
            private readonly string                      _damageFlagKey;

            private Die _ability;

            public bool IsComplete { get; private set; }

            public UCCRuntime(AGISExecutionContext ctx, string deathFlagKey, string damageFlagKey)
            {
                _actor         = ctx.Actor;
                _locomotion    = ctx.Actor?.GetComponent<UltimateCharacterLocomotion>();
                _blackboard    = ctx.Blackboard;
                _actorState    = ctx.Actor?.GetComponent<AGISActorState>();
                _deathFlagKey  = deathFlagKey;
                _damageFlagKey = damageFlagKey;
            }

            public void Enter()
            {
                IsComplete = false;
                DisablePathfinding(_actor);

                if (!string.IsNullOrEmpty(_deathFlagKey) && _actorState != null)
                    _actorState.Set(_deathFlagKey, AGISValue.FromBool(true));

                if (!string.IsNullOrEmpty(_damageFlagKey))
                    _blackboard?.Set(_damageFlagKey, false);

                if (_locomotion == null) return;

                _ability = _locomotion.GetAbility<Die>();
                if (_ability != null)
                    _locomotion.TryStartAbility(_ability);
            }

            public void Tick(float dt)
            {
                if (IsComplete) return;

                if (_locomotion == null || _ability == null)
                {
                    IsComplete = true;
                    return;
                }

                IsComplete = !_ability.IsActive;
            }

            public void Exit()
            {
                if (_ability != null && _ability.IsActive)
                    _locomotion?.TryStopAbility(_ability);

                IsComplete = false;
                _ability   = null;
            }
        }
#endif
    }
}
