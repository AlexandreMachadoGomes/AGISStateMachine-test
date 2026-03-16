// File: AGISEnemyTemplateTestSceneBuilder.cs
// Folder: Assets/Scripts/NPC/Editor/
// Purpose: EditorWindow that wires a scene for testing an AGISEnemyTemplateData.
//          Menu: AGIS/NPC/Enemy Template Test Scene Builder
//
// What it creates / configures:
//   Scene infrastructure (optional):
//     Ground         — 50×50 plane
//     Directional Light — if none exists
//     A* Pathfinding — AstarPath with a 50×50 GridGraph (auto-scanned)
//
//   NPC (capsule "NPC_Test"):
//     Rigidbody              — freeze rotation + Y (non-UCC path)
//     CapsuleCollider        — already on primitive
//     Seeker + AIPath        — A* path computation
//     NPCDetectionCone       — sensor, tuned from template
//     NPCDetectionMeter      — alert level, tuned from template
//     NPCRouteDataHolder     — route data from template
//     AGISActorRuntime       — per-actor execution context
//     AGISActorState         — persistent key-value store
//     AGISStateMachineRunner — slots + known grouped assets from template
//     NPCUCCPathFinder       — IAGISNPCPathFinder bridge (requires UCC below)
//
//   Under #if OPSIVE_UCC:
//     CharacterController        — UCC uses this instead of Rigidbody
//     UltimateCharacterLocomotion — main UCC locomotion
//     AStarAIAgentMovement ability — added to UCL's ability array
//
// NOTES:
//   • autoRegisterTypesFromAssemblies is set to FALSE — AGISNPCTypeRegistration handles it.
//   • AIDestinationSetter is intentionally NOT added (conflicts with AStarAIAgentMovement).
//   • If UCC setup fails, a console warning is printed with manual steps.

#if UNITY_EDITOR

using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AGIS.ESM.Runtime;
using AGIS.NPC.Routes;
using Pathfinding;

#if OPSIVE_UCC
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Integrations.AstarPathfindingProject;
#endif

namespace AGIS.NPC.Editor
{
    public sealed class AGISEnemyTemplateTestSceneBuilder : EditorWindow
    {
        // ── Serialized fields (survive domain reload) ─────────────────────────
        [SerializeField] private AGISEnemyTemplateData _template;
        [SerializeField] private string  _npcName         = "NPC_Test";
        [SerializeField] private bool    _createGround    = true;
        [SerializeField] private bool    _createLight     = true;
        [SerializeField] private bool    _createAstar     = true;
        [SerializeField] private Vector3 _spawnPosition   = new Vector3(0f, 1f, 0f);

        private SerializedObject   _so;
        private SerializedProperty _templateProp;
        private SerializedProperty _npcNameProp;
        private SerializedProperty _createGroundProp;
        private SerializedProperty _createLightProp;
        private SerializedProperty _createAstarProp;
        private SerializedProperty _spawnPosProp;

        private string _lastStatus = "";
        private bool   _lastStatusError;

        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("AGIS/NPC/Enemy Template Test Scene Builder")]
        public static void Open()
        {
            var window = GetWindow<AGISEnemyTemplateTestSceneBuilder>("AGIS Test Scene");
            window.minSize = new Vector2(340f, 370f);
            window.Show();
        }

        private void OnEnable()
        {
            _so = new SerializedObject(this);
            _templateProp     = _so.FindProperty(nameof(_template));
            _npcNameProp      = _so.FindProperty(nameof(_npcName));
            _createGroundProp = _so.FindProperty(nameof(_createGround));
            _createLightProp  = _so.FindProperty(nameof(_createLight));
            _createAstarProp  = _so.FindProperty(nameof(_createAstar));
            _spawnPosProp     = _so.FindProperty(nameof(_spawnPosition));
        }

        private void OnGUI()
        {
            _so.Update();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Enemy Template Test Scene Builder", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── Template ──────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Template", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_templateProp, new GUIContent("Enemy Template Data"));

            if (_template == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an AGISEnemyTemplateData asset.\n" +
                    "Create one via: Assets → Create → AGIS → Enemy Template Data",
                    MessageType.Info);
            }

            EditorGUILayout.Space(6);

            // ── NPC ───────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("NPC", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_npcNameProp,    new GUIContent("Name"));
            EditorGUILayout.PropertyField(_spawnPosProp,   new GUIContent("Spawn Position"));

            EditorGUILayout.Space(6);

            // ── Scene infrastructure ──────────────────────────────────────────
            EditorGUILayout.LabelField("Scene Infrastructure", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_createGroundProp, new GUIContent("Create Ground Plane"));
            EditorGUILayout.PropertyField(_createLightProp,  new GUIContent("Create Directional Light"));
            EditorGUILayout.PropertyField(_createAstarProp,  new GUIContent("Create A* Pathfinding"));

#if OPSIVE_UCC
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "OPSIVE_UCC detected. UltimateCharacterLocomotion and AStarAIAgentMovement " +
                "will be added to the NPC. Verify in the UCC inspector after building.",
                MessageType.Info);
#else
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "OPSIVE_UCC is not defined. NPCUCCPathFinder will be added but locomotion " +
                "won't be wired. Add OPSIVE_UCC to Scripting Define Symbols for full setup.",
                MessageType.Warning);
#endif

            EditorGUILayout.Space(10);

            // ── Build button ──────────────────────────────────────────────────
            GUI.enabled = _template != null;
            if (GUILayout.Button("Build Test Scene", GUILayout.Height(30)))
                Build();
            GUI.enabled = true;

            // ── Status ────────────────────────────────────────────────────────
            if (!string.IsNullOrEmpty(_lastStatus))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    _lastStatus,
                    _lastStatusError ? MessageType.Error : MessageType.Info);
            }

            _so.ApplyModifiedProperties();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Build
        // ─────────────────────────────────────────────────────────────────────

        private void Build()
        {
            if (_template == null)
            {
                SetStatus("No template assigned.", error: true);
                return;
            }

            try
            {
                if (_createGround)    CreateGround();
                if (_createLight)     CreateDirectionalLight();
                AstarPath astar = _createAstar ? CreateAstarPathfinding() : AstarPath.active;

                var npc = CreateNPC();

                if (astar != null)
                    astar.Scan();

                EditorSceneManager.MarkAllScenesDirty();

                SetStatus($"'{_npcName}' built. Press Play to test. " +
                          "Select the NPC to see route gizmos.", error: false);

                Selection.activeGameObject = npc;
            }
            catch (Exception ex)
            {
                SetStatus($"Build failed: {ex.Message}", error: true);
                Debug.LogException(ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Scene object builders
        // ─────────────────────────────────────────────────────────────────────

        private static void CreateGround()
        {
            if (GameObject.Find("Ground") != null) return;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position   = Vector3.zero;
            ground.transform.localScale = new Vector3(5f, 1f, 5f); // 50×50 units
        }

        private static void CreateDirectionalLight()
        {
            if (FindObjectOfType<Light>() != null) return;

            var go    = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.intensity = 1f;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static AstarPath CreateAstarPathfinding()
        {
            if (AstarPath.active != null) return AstarPath.active;

            var go    = new GameObject("A* Pathfinding");
            var astar = go.AddComponent<AstarPath>();

            var graph = astar.data.AddGraph(typeof(GridGraph)) as GridGraph;
            if (graph != null)
            {
                graph.SetDimensions(50, 50, 1f);
                graph.center         = Vector3.zero;
                graph.collision.mask = LayerMask.GetMask("Default");
            }

            return astar;
        }

        private GameObject CreateNPC()
        {
            var existing = GameObject.Find(_npcName);
            if (existing != null)
            {
                Debug.LogWarning($"[AGIS] '{_npcName}' already exists — reconfiguring it.");
                ReconfigureNPC(existing);
                return existing;
            }

            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = _npcName;
            npc.transform.position = _spawnPosition;

#if OPSIVE_UCC
            SetupUCCLocomotion(npc);
#else
            // Non-UCC: drive movement directly via AIPath
            var rb = npc.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation |
                             RigidbodyConstraints.FreezePositionY;
#endif

            // A* pathfinding
            npc.AddComponent<Seeker>();
            var aiPath = npc.AddComponent<AIPath>();
            aiPath.maxSpeed           = 3.5f;
            aiPath.slowdownDistance   = 1f;
            aiPath.endReachedDistance = 0.5f;

#if OPSIVE_UCC
            aiPath.enabled = false; // AStarAIAgentMovement ability controls start/stop
            npc.AddComponent<NPCUCCPathFinder>();
#else
            aiPath.enabled = true;  // NPCAStarPathFinder drives AIPath directly
            npc.AddComponent<NPCAStarPathFinder>();
#endif

            // Detection
            var cone = npc.AddComponent<NPCDetectionCone>();
            cone.range         = _template.detectionRange;
            cone.angle         = _template.detectionAngle;
            cone.detectionMask = ~0;

            var meter = npc.AddComponent<NPCDetectionMeter>();
            meter.fillRate             = _template.fillRate;
            meter.drainRate            = _template.drainRate;
            meter.maxDetection         = _template.maxDetection;
            meter.investigateThreshold = _template.investigateThreshold;
            meter.targetTag            = _template.targetTag;

            // Routes
            var holder = npc.AddComponent<NPCRouteDataHolder>();
            holder.routeData = _template.routeData;

            // AGIS runtime
            npc.AddComponent<AGISActorRuntime>();
            npc.AddComponent<AGISActorState>();

            // Runner
            var runner = npc.AddComponent<AGISStateMachineRunner>();
            WireRunner(runner);

            return npc;
        }

        private void ReconfigureNPC(GameObject npc)
        {
            // Update components that come from the template
            var meter = npc.GetComponent<NPCDetectionMeter>();
            if (meter != null)
            {
                meter.fillRate             = _template.fillRate;
                meter.drainRate            = _template.drainRate;
                meter.maxDetection         = _template.maxDetection;
                meter.investigateThreshold = _template.investigateThreshold;
                meter.targetTag            = _template.targetTag;
            }

            var holder = npc.GetComponent<NPCRouteDataHolder>();
            if (holder != null)
                holder.routeData = _template.routeData;

            var runner = npc.GetComponent<AGISStateMachineRunner>();
            if (runner != null)
                WireRunner(runner);
        }

        // ─────────────────────────────────────────────────────────────────────
        // UCC setup
        // ─────────────────────────────────────────────────────────────────────

#if OPSIVE_UCC
        private static void SetupUCCLocomotion(GameObject npc)
        {
            // UCC uses CharacterController, not Rigidbody
            if (npc.GetComponent<CharacterController>() == null)
                npc.AddComponent<CharacterController>();

            // Add UltimateCharacterLocomotion
            var locomotion = npc.GetComponent<UltimateCharacterLocomotion>()
                          ?? npc.AddComponent<UltimateCharacterLocomotion>();

            // Attempt to add AStarAIAgentMovement ability via SerializedObject
            bool abilityAdded = TryAddAStarAbility(locomotion);

            if (!abilityAdded)
            {
                Debug.LogWarning(
                    $"[AGIS] Could not auto-add AStarAIAgentMovement to '{npc.name}'. " +
                    "Open the UCC Character Manager in the inspector and add it manually " +
                    "as a concurrent ability.",
                    npc);
            }
        }

        private static bool TryAddAStarAbility(UltimateCharacterLocomotion locomotion)
        {
            try
            {
                var so         = new SerializedObject(locomotion);
                // UCC stores abilities in "m_Abilities"
                var abilitiesProp = so.FindProperty("m_Abilities")
                                 ?? so.FindProperty("abilities");

                if (abilitiesProp == null || !abilitiesProp.isArray)
                    return false;

                // Check if already present
                for (int i = 0; i < abilitiesProp.arraySize; i++)
                {
                    var elem = abilitiesProp.GetArrayElementAtIndex(i);
                    if (elem.managedReferenceValue is AStarAIAgentMovement)
                        return true;
                }

                // Insert a new AStarAIAgentMovement instance
                int idx = abilitiesProp.arraySize;
                abilitiesProp.InsertArrayElementAtIndex(idx);
                var newElem = abilitiesProp.GetArrayElementAtIndex(idx);
                newElem.managedReferenceValue = new AStarAIAgentMovement();
                so.ApplyModifiedPropertiesWithoutUndo();
                return true;
            }
            catch
            {
                return false;
            }
        }
#endif

        // ─────────────────────────────────────────────────────────────────────
        // Runner wiring
        // ─────────────────────────────────────────────────────────────────────

        private void WireRunner(AGISStateMachineRunner runner)
        {
            var so = new SerializedObject(runner);

            // Explicit registration — do NOT use reflection-based auto-discovery in builds
            so.FindProperty("autoRegisterTypesFromAssemblies").boolValue = false;

            // Trace enabled for easy debugging in Play Mode
            var traceProp = so.FindProperty("trace");
            var traceEnabled = traceProp?.FindPropertyRelative("enabled");
            if (traceEnabled != null) traceEnabled.boolValue = true;

            // Two slots: Stealth (0) + Patrol (1)
            var slotsProp = so.FindProperty("slots");
            slotsProp.arraySize = 2;

            var stealthSlot = slotsProp.GetArrayElementAtIndex(0);
            stealthSlot.FindPropertyRelative("slotName").stringValue = "Stealth";
            stealthSlot.FindPropertyRelative("enabled").boolValue    = true;
            stealthSlot.FindPropertyRelative("graphAsset").objectReferenceValue = _template.stealthGraph;

            var patrolSlot = slotsProp.GetArrayElementAtIndex(1);
            patrolSlot.FindPropertyRelative("slotName").stringValue = "Patrol";
            patrolSlot.FindPropertyRelative("enabled").boolValue    = true;
            patrolSlot.FindPropertyRelative("graphAsset").objectReferenceValue = _template.patrolGraph;

            // Register known grouped assets (RoutedMovement, etc.)
            var knownProp = so.FindProperty("knownGroupedAssets");
            if (_template.knownGroupedAssets != null && _template.knownGroupedAssets.Length > 0)
            {
                knownProp.arraySize = _template.knownGroupedAssets.Length;
                for (int i = 0; i < _template.knownGroupedAssets.Length; i++)
                    knownProp.GetArrayElementAtIndex(i).objectReferenceValue =
                        _template.knownGroupedAssets[i];
            }
            else
            {
                knownProp.arraySize = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void SetStatus(string msg, bool error)
        {
            _lastStatus      = msg;
            _lastStatusError = error;
            Repaint();
        }
    }
}

#endif
