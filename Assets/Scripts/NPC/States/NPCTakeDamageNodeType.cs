// File: NPCTakeDamageNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: Triggers a Take Damage ability via UCC (Opsive Ultimate Character Controller)
//          and signals completion once the ability finishes.
//          Add an outgoing edge with condition "agis.node_complete" to auto-transition out.
//
// Params:
//   damage_flag_key  (String, default "npc.is_damaged") — blackboard key cleared on Enter so
//                                                         the AnyState interrupt does not re-fire
//                                                         while the damage animation plays.
//                                                         Leave empty to skip.
//
// Guarded by #if OPSIVE_UCC. Without the define the node registers as a shell:
//   • CreateRuntime() returns a NoOpNodeRuntime that completes on the first Tick.
//   • IAGISNodeComponentRequirements returns an empty array.
//
// The TakeDamage UCC ability is resolved by type (GetAbility<TakeDamage>()) — no inspector
// index required. Ability order in the UCC ability list does not affect this node.
//
// Implements IAGISNodeSignal so "agis.node_complete" edges fire when IsComplete = true.
// IsComplete is reset to false on Enter() — safe to re-enter (e.g. rapid hits).

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
    public sealed class NPCTakeDamageNodeType : IAGISNodeType, IAGISNodeComponentRequirements
    {
        public string TypeId      => "npc.take_damage";
        public string DisplayName => "NPC Take Damage";
        public AGISNodeKind Kind   => AGISNodeKind.Normal;

        public AGISParamSchema Schema { get; } = new AGISParamSchema
        {
            specs =
            {
                new AGISParamSpec("damage_flag_key", AGISParamType.String, AGISValue.FromString("npc.is_damaged"))
                    { displayName = "Damage Flag Key",
                      tooltip     = "Blackboard key cleared on Enter to prevent the AnyState interrupt " +
                                    "edge from re-firing while the damage animation plays. Leave empty to skip." },
            }
        };

        public IReadOnlyList<Type> GetRequiredComponents(IAGISParamAccessor resolvedParams) =>
#if OPSIVE_UCC
            new[] { typeof(UltimateCharacterLocomotion) };
#else
            Array.Empty<Type>();
#endif

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
        {
            string damageFlagKey = args.Params.GetString("damage_flag_key", "npc.is_damaged");

#if OPSIVE_UCC
            return new UCCRuntime(args.Ctx, damageFlagKey);
#else
            return new NoOpRuntime(args.Ctx, damageFlagKey);
#endif
        }

        // ─────────────────────────────────────────────────────────────────────
        // Shell runtime used when OPSIVE_UCC is not defined.
        // Completes immediately on the first Tick so the graph never gets stuck.
        // ─────────────────────────────────────────────────────────────────────
        private sealed class NoOpRuntime : IAGISNodeRuntime, IAGISNodeSignal
        {
            private readonly IAGISBlackboard _blackboard;
            private readonly string          _damageFlagKey;

            public bool IsComplete { get; private set; }

            public NoOpRuntime(AGISExecutionContext ctx, string damageFlagKey)
            {
                _blackboard    = ctx.Blackboard;
                _damageFlagKey = damageFlagKey;
            }

            public void Enter()
            {
                IsComplete = false;
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
            private readonly UltimateCharacterLocomotion _locomotion;
            private readonly IAGISBlackboard             _blackboard;
            private readonly string                      _damageFlagKey;

            private TakeDamage _ability;

            public bool IsComplete { get; private set; }

            public UCCRuntime(AGISExecutionContext ctx, string damageFlagKey)
            {
                _locomotion    = ctx.Actor?.GetComponent<UltimateCharacterLocomotion>();
                _blackboard    = ctx.Blackboard;
                _damageFlagKey = damageFlagKey;
            }

            public void Enter()
            {
                IsComplete = false;

                if (!string.IsNullOrEmpty(_damageFlagKey))
                    _blackboard?.Set(_damageFlagKey, false);

                if (_locomotion == null) return;

                _ability = _locomotion.GetAbility<TakeDamage>();
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
