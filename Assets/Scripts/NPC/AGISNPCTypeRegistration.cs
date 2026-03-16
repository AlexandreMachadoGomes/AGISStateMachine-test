// File: AGISNPCTypeRegistration.cs
// Folder: Assets/Scripts/NPC/
// Purpose: Explicit IL2CPP-safe registration of every AGIS node and condition type.
//
// [RuntimeInitializeOnLoadMethod] fires before the first scene loads, so all types
// are buffered in AGISTypeRegistrar before any AGISStateMachineRunner.Awake() runs.
//
// WHY this exists:
//   AGISStateMachineRunner has an autoRegisterTypesFromAssemblies flag that scans
//   assemblies via reflection at runtime. This works in Mono (editor / standalone PC)
//   but fails silently under IL2CPP when the linker strips types that have no static
//   references. This file provides those static references and removes the reliance
//   on reflection-based discovery in shipped builds.
//
// HOW to extend:
//   If you add a new IAGISNodeType or IAGISConditionType, add a line here.
//   Keep autoRegisterTypesFromAssemblies = false in production runner instances.

using AGIS.ESM.Runtime;
using AGIS.NPC.States;
using AGIS.NPC.Conditions;
using AGIS.Dialogue;
using UnityEngine;

namespace AGIS.NPC
{
    internal static class AGISNPCTypeRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // ── ESM built-in node types ───────────────────────────────────────────
            // Note: AGISGroupedNodeType and AGISParallelNodeType require constructor
            //       arguments (resolvers, compilers) so they are always registered
            //       by AGISStateMachineRunner.EnsureBuiltInStructuralNodeTypes().
            AGISTypeRegistrar.Add(new AGISAnyStateNodeType());

            // ── Dialogue node types ───────────────────────────────────────────────
            AGISTypeRegistrar.Add(new AGISDialogueNodeType());

            // ── NPC node types ────────────────────────────────────────────────────
            AGISTypeRegistrar.Add(new NPCIdleNodeType());
            AGISTypeRegistrar.Add(new NPCWanderNodeType());
            AGISTypeRegistrar.Add(new NPCFollowTargetNodeType());
            AGISTypeRegistrar.Add(new NPCBehaviorSelectorNodeType());
            AGISTypeRegistrar.Add(new NPCMoveToWaypointNodeType());
            AGISTypeRegistrar.Add(new NPCAdvanceWaypointNodeType());
            AGISTypeRegistrar.Add(new NPCResetRouteNodeType());
            AGISTypeRegistrar.Add(new NPCInvestigateNodeType());
            AGISTypeRegistrar.Add(new NPCStealthMeterNodeType());

            AGISTypeRegistrar.Add(new NPCTakeDamageNodeType());
            AGISTypeRegistrar.Add(new NPCDyingNodeType());

            // ── LLM / knowledge / event node types ───────────────────────────────
            AGISTypeRegistrar.Add(new AGISLLMDialogueNodeType());
            AGISTypeRegistrar.Add(new AddKnowledgeNodeType());
            AGISTypeRegistrar.Add(new RemoveKnowledgeNodeType());
            AGISTypeRegistrar.Add(new EmitActorEventNodeType());

            // ── ESM built-in condition types ──────────────────────────────────────
            AGISTypeRegistrar.Add(new AGISNodeCompleteConditionType());

            // ── Dialogue condition types ──────────────────────────────────────────
            AGISTypeRegistrar.Add(new AGISDialogueOptionConditionType());
            AGISTypeRegistrar.Add(new AGISHasDialogueChoiceConditionType());

            // ── NPC condition types ───────────────────────────────────────────────
            AGISTypeRegistrar.Add(new NPCHasReachedDestinationConditionType());
            AGISTypeRegistrar.Add(new NPCHasArrivedAtWaypointConditionType());
            AGISTypeRegistrar.Add(new NPCIsMovingConditionType());
            AGISTypeRegistrar.Add(new NPCIsWithinDistanceConditionType());
            AGISTypeRegistrar.Add(new NPCDetectsObjectConditionType());
            AGISTypeRegistrar.Add(new NPCOnSequenceIndexConditionType());
            AGISTypeRegistrar.Add(new NPCHasLostTargetConditionType());
            AGISTypeRegistrar.Add(new NPCDetectionMeterConditionType());
            AGISTypeRegistrar.Add(new AGISActorStateBoolConditionType());
            AGISTypeRegistrar.Add(new BlackboardBoolConditionType());
        }
    }
}
