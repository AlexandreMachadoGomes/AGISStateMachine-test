// File: AGISEnemyTemplateData.cs
// Folder: Assets/Scripts/NPC/
// Purpose: ScriptableObject that bundles all data needed to configure a runtime-spawned
//          enemy via AGISEnemyConfigurator. Create via:
//            Assets → Create → AGIS → Enemy Template Data
//
// Usage:
//   var go = Instantiate(enemyPrefab, spawnPoint, Quaternion.identity);
//   // Awake fires here — runner initialises with whatever is in the prefab's inspector.
//   AGISEnemyConfigurator.Inject(go, enemyTemplate);
//   go.GetComponent<AGISStateMachineRunner>().RebuildAllSlots();

using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.NPC.Routes;
using UnityEngine;

namespace AGIS.NPC
{
    [CreateAssetMenu(menuName = "AGIS/Enemy Template Data", fileName = "EnemyTemplateData")]
    public sealed class AGISEnemyTemplateData : ScriptableObject
    {
        [Header("State Machine Graphs")]
        [Tooltip("Patrol graph assigned to slot index 1 (slot name: Patrol).")]
        public AGISStateMachineGraphAsset patrolGraph;

        [Tooltip("Stealth / detection graph assigned to slot index 0 (slot name: Stealth).")]
        public AGISStateMachineGraphAsset stealthGraph;

        [Tooltip("Additional grouped state assets referenced by the graphs above.")]
        public AGISGroupedStateAsset[] knownGroupedAssets = System.Array.Empty<AGISGroupedStateAsset>();

        [Header("Route Data")]
        [Tooltip("Route waypoint data for the NPC route system. Assigned to NPCRouteDataHolder.routeData.")]
        public NPCRouteData routeData;

        [Header("Detection Meter")]
        [Tooltip("Units/second added to the meter while the target is visible.")]
        [Min(0f)] public float fillRate = 1.0f;

        [Tooltip("Units/second removed from the meter while the target is NOT visible.")]
        [Min(0f)] public float drainRate = 0.4f;

        [Tooltip("Maximum meter value; reaching this value means full alert.")]
        [Min(0.1f)] public float maxDetection = 3.0f;

        [Tooltip("Meter value at which the NPC should start investigating.")]
        [Min(0f)] public float investigateThreshold = 1.0f;

        [Tooltip("Tag used to locate the detection target in the scene.")]
        public string targetTag = "Player";

        [Header("Detection Cone")]
        [Tooltip("Radius of the detection sphere / cone.")]
        [Min(0f)] public float detectionRange = 10.0f;

        [Tooltip("Half-angle (degrees) of the detection cone. Only used when the cone shape is Cone.")]
        [Range(0f, 180f)] public float detectionAngle = 60.0f;
    }
}
