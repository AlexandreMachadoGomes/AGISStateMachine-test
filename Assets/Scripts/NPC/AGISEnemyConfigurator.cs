// File: AGISEnemyConfigurator.cs
// Folder: Assets/Scripts/NPC/
// Purpose: Runtime API to set up a spawned enemy GameObject with all required AGIS
//          and A* Pathfinding components, then inject a template asset's values into them.
//
// ── Single-call API ───────────────────────────────────────────────────────────────────
//
//   var go = Instantiate(enemyPrefab, spawnPoint, Quaternion.identity);
//   // Awake fires here — runner initialises with whatever was in the prefab's inspector.
//   AGISEnemyConfigurator.UpgradeEnemy(go, enemyTemplate);
//   // Done. Runner is rebuilt and the enemy is ready.
//
// ── Two-call API (if you need control between steps) ─────────────────────────────────
//
//   AGISEnemyConfigurator.Configure(go);          // add missing components
//   AGISEnemyConfigurator.Inject(go, template);   // push template values
//   go.GetComponent<AGISStateMachineRunner>().RebuildAllSlots();
//
// ── What Configure() ensures is present ──────────────────────────────────────────────
//
//   AGIS infrastructure:
//     AGISActorRuntime          — per-actor execution context
//     AGISActorState            — persistent key-value store (survives transitions)
//     AGISStateMachineRunner    — hosts state machine slots; auto-registration enabled
//
//   NPC systems:
//     NPCDetectionCone          — sphere/cone sensor for detecting targets
//     NPCDetectionMeter         — alert-level meter driven by detection cone
//     NPCRouteDataHolder        — waypoint route data for patrol sequences
//
//   Pathfinding (A* Pathfinding Project):
//     Seeker                    — requests paths from the A* graph
//     AIPath                    — follows computed paths; controls character movement
//     AIDestinationSetter       — keeps the AIPath target synced to a Transform
//
// ── What Inject() wires ───────────────────────────────────────────────────────────────
//
//   NPCDetectionMeter  ← fillRate, drainRate, maxDetection, investigateThreshold, targetTag
//   NPCDetectionCone   ← range, angle, detectionMask (all layers — caller may narrow)
//   NPCRouteDataHolder ← routeData
//   AGISStateMachineRunner:
//     Slot 0 "Stealth" ← data.stealthGraph
//     Slot 1 "Patrol"  ← data.patrolGraph
//     knownGroupedAssets registered so grouped nodes can resolve
//
// ── UCC DEPENDENCIES (#if OPSIVE_UCC) ────────────────────────────────────────────────
//
//   ⚠ 1. UltimateCharacterLocomotion must already exist on the prefab.
//        AGISEnemyConfigurator CANNOT add it — UCC requires a complete character setup
//        (abilities, item manager, effect manager, etc.) done in the Unity inspector.
//        → If this component is missing when UpgradeEnemy/Configure runs, a LogWarning
//          is emitted and the actor will not have UCC animation ability support.
//
//   ⚠ 2. AIPath directly writes to CharacterController/Rigidbody.
//        When UCC is active, UltimateCharacterLocomotion also owns the character's
//        movement. Having both active causes jitter or physics conflicts.
//        → Recommended fix: disable AIPath.canMove, then create a UCC movement Ability
//          that reads AIPath.desiredVelocity and forwards it to
//          CharacterLocomotion.InputVector each UpdateAbility() tick.
//        → Until that bridge is in place, either disable AIPath (no movement) or
//          remove UltimateCharacterLocomotion (no UCC abilities).
//
//   ⚠ 3. NPCTakeDamageNodeType and NPCDyingNodeType use an ability_index param.
//        This index must match the actual position of the ability inside
//        UltimateCharacterLocomotion.Abilities[] as shown in the inspector.
//        Out-of-range indices are silently treated as "no ability" (NoOp behaviour).

using System;
using System.Reflection;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.NPC.Routes;
using Pathfinding;
using UnityEngine;

namespace AGIS.NPC
{
    public static class AGISEnemyConfigurator
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // UpgradeEnemy — single-call convenience wrapper.
        // Runs Configure → Inject → RebuildAllSlots in one shot.
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fully upgrades <paramref name="enemy"/> in one call:
        /// adds any missing infrastructure components, injects all values from
        /// <paramref name="template"/>, and rebuilds the state machine.
        ///
        /// <para>Safe to call on a freshly Instantiated prefab whose Awake() has already
        /// run — the runner will be rebuilt with the template's slots after this call.</para>
        ///
        /// <para><b>UCC pre-requisites</b> (only relevant when OPSIVE_UCC is defined):<br/>
        /// • <c>UltimateCharacterLocomotion</c> must already be configured on the prefab.<br/>
        /// • <c>AIPath</c> conflicts with UCC locomotion — implement a UCC movement ability
        ///   that reads <c>AIPath.desiredVelocity</c> and feeds it to
        ///   <c>CharacterLocomotion.InputVector</c>.<br/>
        /// • <c>ability_index</c> params in TakeDamage/Dying nodes must match the ability's
        ///   position in <c>UltimateCharacterLocomotion.Abilities[]</c>.</para>
        /// </summary>
        public static void UpgradeEnemy(GameObject enemy, AGISEnemyTemplateData template)
        {
            if (enemy    == null) throw new ArgumentNullException(nameof(enemy));
            if (template == null) throw new ArgumentNullException(nameof(template));

            Configure(enemy);
            Inject(enemy, template);

            var runner = enemy.GetComponent<AGISStateMachineRunner>();
            if (runner != null)
                runner.RebuildAllSlots();
            else
                Debug.LogWarning("[AGISEnemyConfigurator] AGISStateMachineRunner not found after Configure — this should not happen.", enemy);

#if OPSIVE_UCC
            WarnIfUCCMissing(enemy);
#endif
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Configure — step 1: ensure all infrastructure components are present.
        // Safe to call multiple times: never duplicates components.
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Ensures the following components exist on <paramref name="actor"/>,
        /// adding them if absent. Never duplicates an existing component.
        /// <list type="bullet">
        ///   <item>AGISActorRuntime, AGISActorState, AGISStateMachineRunner</item>
        ///   <item>NPCDetectionCone, NPCDetectionMeter, NPCRouteDataHolder</item>
        ///   <item>Seeker, AIPath, AIDestinationSetter (A* Pathfinding)</item>
        /// </list>
        /// When AGISStateMachineRunner is newly added its <c>autoRegisterTypesFromAssemblies</c>
        /// flag is enabled so all node/condition types are discovered automatically.
        /// </summary>
        public static void Configure(GameObject actor)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));

            EnsureComponent<AGISActorRuntime>(actor);
            EnsureComponent<AGISActorState>(actor);

            bool runnerAdded = EnsureComponent<AGISStateMachineRunner>(actor);
            if (runnerAdded)
            {
                var runner = actor.GetComponent<AGISStateMachineRunner>();
                SetPrivateBool(runner, "autoRegisterTypesFromAssemblies", true);
            }

            EnsureComponent<NPCDetectionCone>(actor);
            EnsureComponent<NPCDetectionMeter>(actor);
            EnsureComponent<NPCRouteDataHolder>(actor);

            // A* Pathfinding — always added for path computation.
            // With UCC active, disable AIPath.canMove and bridge desiredVelocity to UCC
            // (see file header for details).
            EnsureComponent<Seeker>(actor);
            EnsureComponent<AIPath>(actor);
            EnsureComponent<AIDestinationSetter>(actor);

#if OPSIVE_UCC
            WarnIfUCCMissing(actor);
#endif
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Inject — step 2: push template values into the components.
        // Call Configure(actor) first (or ensure all components are already present).
        // After Inject, call runner.RebuildAllSlots() to re-initialise the state machine.
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Pushes all field values from <paramref name="data"/> into the matching components
        /// on <paramref name="actor"/>. Call <c>Configure</c> first so the components exist,
        /// then call <c>runner.RebuildAllSlots()</c> afterward to activate the new slots.
        /// </summary>
        public static void Inject(GameObject actor, AGISEnemyTemplateData data)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (data  == null) throw new ArgumentNullException(nameof(data));

            InjectDetectionMeter(actor, data);
            InjectDetectionCone(actor, data);
            InjectRouteData(actor, data);
            InjectRunnerSlots(actor, data);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────────────

        private static void InjectDetectionMeter(GameObject actor, AGISEnemyTemplateData data)
        {
            var meter = actor.GetComponent<NPCDetectionMeter>();
            if (meter == null)
            {
                Debug.LogWarning("[AGISEnemyConfigurator] NPCDetectionMeter not found. Call Configure() before Inject().", actor);
                return;
            }
            meter.fillRate             = data.fillRate;
            meter.drainRate            = data.drainRate;
            meter.maxDetection         = data.maxDetection;
            meter.investigateThreshold = data.investigateThreshold;
            meter.targetTag            = data.targetTag;
        }

        private static void InjectDetectionCone(GameObject actor, AGISEnemyTemplateData data)
        {
            var cone = actor.GetComponent<NPCDetectionCone>();
            if (cone == null)
            {
                Debug.LogWarning("[AGISEnemyConfigurator] NPCDetectionCone not found. Call Configure() before Inject().", actor);
                return;
            }
            cone.range         = data.detectionRange;
            cone.angle         = data.detectionAngle;
            cone.detectionMask = ~0; // all layers — narrow after Inject if needed
        }

        private static void InjectRouteData(GameObject actor, AGISEnemyTemplateData data)
        {
            var holder = actor.GetComponent<NPCRouteDataHolder>();
            if (holder == null)
            {
                Debug.LogWarning("[AGISEnemyConfigurator] NPCRouteDataHolder not found. Call Configure() before Inject().", actor);
                return;
            }
            holder.routeData = data.routeData;
        }

        private static void InjectRunnerSlots(GameObject actor, AGISEnemyTemplateData data)
        {
            var runner = actor.GetComponent<AGISStateMachineRunner>();
            if (runner == null)
            {
                Debug.LogWarning("[AGISEnemyConfigurator] AGISStateMachineRunner not found. Call Configure() before Inject().", actor);
                return;
            }

            // Register grouped assets so the resolver index is ready when slots rebuild.
            if (data.knownGroupedAssets != null && data.knownGroupedAssets.Length > 0)
            {
                for (int i = 0; i < data.knownGroupedAssets.Length; i++)
                {
                    var grouped = data.knownGroupedAssets[i];
                    if (grouped == null) continue;
                    bool isLast = (i == data.knownGroupedAssets.Length - 1);
                    runner.RegisterGroupedAsset(grouped, rebuildIndex: isLast);
                }
            }

            // Slot 0 → Stealth, Slot 1 → Patrol.
            // rebuild: false — caller (or UpgradeEnemy) calls RebuildAllSlots afterwards.
            runner.EnsureSlotCount(2);
            runner.SetSlotGraphAsset(0, data.stealthGraph, rebuild: false);
            runner.SetSlotGraphAsset(1, data.patrolGraph,  rebuild: false);

            var slots = runner.Slots;
            if (slots.Count > 0 && slots[0] != null) slots[0].slotName = "Stealth";
            if (slots.Count > 1 && slots[1] != null) slots[1].slotName = "Patrol";
        }

        /// <summary>Adds T if absent. Returns true when newly added.</summary>
        private static bool EnsureComponent<T>(GameObject actor) where T : Component
        {
            if (actor.GetComponent<T>() != null) return false;
            actor.AddComponent<T>();
            return true;
        }

        private static void SetPrivateBool(object target, string fieldName, bool value)
        {
            if (target == null) return;
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null && field.FieldType == typeof(bool))
                field.SetValue(target, value);
            else
                Debug.LogWarning($"[AGISEnemyConfigurator] Could not find bool field '{fieldName}' on {target.GetType().Name}.");
        }

#if OPSIVE_UCC
        private static void WarnIfUCCMissing(GameObject actor)
        {
            // UltimateCharacterLocomotion cannot be added at runtime — it requires a full
            // UCC character setup. Emit a clear warning so the problem is obvious.
            if (actor.GetComponent<Opsive.UltimateCharacterController.Character.UltimateCharacterLocomotion>() == null)
            {
                Debug.LogWarning(
                    "[AGISEnemyConfigurator] UCC is enabled (OPSIVE_UCC defined) but " +
                    "UltimateCharacterLocomotion is missing on '" + actor.name + "'. " +
                    "NPCTakeDamageNodeType and NPCDyingNodeType will fall back to NoOp. " +
                    "Configure UCC on the prefab in the inspector before spawning.",
                    actor);
            }
        }
#endif
    }
}
