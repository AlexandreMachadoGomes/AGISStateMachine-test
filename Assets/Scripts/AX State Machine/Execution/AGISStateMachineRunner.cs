// File: AGISStateMachineRunner.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Runs multiple state machine slots per actor (canvas: many actors + multiple machines per actor).
// Notes:
// - Auto-registers structural node types (Grouped/Parallel) to reduce configuration burden.
// - Adds small public APIs for programmatic setup (useful for testing and future in-game authoring UI).

using System;
using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public enum AGISRunnerUpdateMode
    {
        Update = 0,
        FixedUpdate = 1,
        Manual = 2,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(AGISActorRuntime))]
    public sealed class AGISStateMachineRunner : MonoBehaviour
    {
        [Header("Tick")]
        [SerializeField, Min(1f)] private float tickHz = 30f;

        [Tooltip("Safety cap to avoid infinite transition loops in one tick.")]
        [SerializeField, Min(1)] private int maxTransitionsPerTick = 4;

        [SerializeField] private AGISRunnerUpdateMode updateMode = AGISRunnerUpdateMode.Update;

        [Header("Validation")]
        [SerializeField] private bool validateGroupedAssets = true;
        [SerializeField] private bool validateGroupedInternalGraphs = true;
        [SerializeField] private bool allowUnknownParamKeys = false;

        [Header("Grouped Assets (for validation + runtime resolution)")]
        [Tooltip("Optional list of known grouped assets to allow scope/binding validation and runtime resolution without an AssetDatabase resolver.")]
        [SerializeField] private List<AGISGroupedStateAsset> knownGroupedAssets = new List<AGISGroupedStateAsset>();

        [Header("Type Registration (optional)")]
        [Tooltip("If enabled, registries will auto-register all types from loaded assemblies that implement IAGISNodeType / IAGISConditionType and have parameterless constructors.")]
        [SerializeField] private bool autoRegisterTypesFromAssemblies = false;

        [Header("Slots")]
        [SerializeField] private List<AGISStateMachineSlot> slots = new List<AGISStateMachineSlot>();

        [Header("Debug")]
        [SerializeField] private AGISDebugTrace trace = new AGISDebugTrace();

        public AGISNodeTypeRegistry NodeTypes { get; private set; }
        public AGISConditionTypeRegistry ConditionTypes { get; private set; }

        public IReadOnlyList<AGISStateMachineSlot> Slots => slots;

        private AGISGraphValidator _validator;
        private AGISGraphCompiler _compiler;
        private AGISRuntimeGraphCache _cache;
        private AGISTransitionEvaluator _evaluator;
        private AGISNodeRuntimeFactory _nodeFactory;

        private AGISActorRuntime _actor;

        private Dictionary<AGISGuid, AGISGroupedStateAsset> _groupIndex;

        private void Awake()
        {
            _actor = GetComponent<AGISActorRuntime>();
            _actor.EnsureInitialized();

            NodeTypes = new AGISNodeTypeRegistry();
            ConditionTypes = new AGISConditionTypeRegistry();

            if (autoRegisterTypesFromAssemblies)
            {
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                NodeTypes.RegisterAllFromAssemblies(asms);
                ConditionTypes.RegisterAllFromAssemblies(asms);
            }

            _compiler = new AGISGraphCompiler(NodeTypes);
            _cache = new AGISRuntimeGraphCache(_compiler);
            _evaluator = new AGISTransitionEvaluator(ConditionTypes);
            _nodeFactory = new AGISNodeRuntimeFactory(NodeTypes);

            BuildGroupIndex();

            EnsureBuiltInStructuralNodeTypes();

            PopulateActorState();

            ResolveComponentDependencies();

            _validator = new AGISGraphValidator(NodeTypes, ConditionTypes, ResolveGroupAsset);

            RebuildAllSlots();
        }

        private void OnEnable()
        {
            RebuildAllSlots();
        }

        private void OnDisable()
        {
            StopAllSlots();
        }

        private void Update()
        {
            if (updateMode != AGISRunnerUpdateMode.Update)
                return;

            TickInternal(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (updateMode != AGISRunnerUpdateMode.FixedUpdate)
                return;

            TickInternal(Time.fixedDeltaTime);
        }

        public void TickManual(float dt)
        {
            if (updateMode != AGISRunnerUpdateMode.Manual)
                return;

            TickInternal(dt);
        }

        // -------------------------
        // Public helper APIs (testing + future UI)
        // -------------------------

        public void EnsureSlotCount(int count)
        {
            if (count < 0) count = 0;
            slots ??= new List<AGISStateMachineSlot>();
            while (slots.Count < count)
                slots.Add(new AGISStateMachineSlot());
        }

        public bool SetSlotGraphAsset(int slotIndex, AGISStateMachineGraphAsset graphAsset, bool rebuild = true)
        {
            if (slotIndex < 0) return false;
            EnsureSlotCount(slotIndex + 1);

            var slot = slots[slotIndex];
            if (slot == null)
            {
                slot = new AGISStateMachineSlot();
                slots[slotIndex] = slot;
            }

            slot.graphAsset = graphAsset;

            if (rebuild)
                RebuildSlot(slot);

            return true;
        }

        public void RegisterGroupedAsset(AGISGroupedStateAsset asset, bool rebuildIndex = true)
        {
            if (asset == null || !asset.groupAssetId.IsValid)
                return;

            knownGroupedAssets ??= new List<AGISGroupedStateAsset>();

            bool exists = false;
            for (int i = 0; i < knownGroupedAssets.Count; i++)
            {
                if (knownGroupedAssets[i] == asset)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
                knownGroupedAssets.Add(asset);

            if (rebuildIndex)
                BuildGroupIndex();
        }

        // -------------------------
        // Slot lifecycle
        // -------------------------

        public void RebuildAllSlots()
        {
            if (slots == null) slots = new List<AGISStateMachineSlot>();
            for (int i = 0; i < slots.Count; i++)
                RebuildSlot(slots[i]);
        }

        public void RebuildSlot(AGISStateMachineSlot slot)
        {
            if (slot == null)
                return;

            slot.instance?.Stop();
            slot.instance = null;
            slot.compiledGraph = null;
            slot.lastValidation = null;
            slot.ResetRuntimeAccumulators();

            if (!slot.enabled)
                return;

            var graph = slot.GetGraphDef();
            if (graph == null)
            {
                trace.Warn($"Slot '{slot.slotName}' has no graphAsset assigned.");
                return;
            }

            var options = new AGISGraphValidatorOptions
            {
                AllowUnknownParamKeys = allowUnknownParamKeys,
                ValidateGroupedAssets = validateGroupedAssets,
                ValidateGroupedInternalGraphs = validateGroupedInternalGraphs
            };

            var report = _validator.ValidateGraph(graph, options);
            slot.lastValidation = report;

            if (report.HasErrors)
            {
                trace.Error($"Slot '{slot.slotName}' graph failed validation ({report.Issues.Count} issues).");
                if (trace.enabled)
                {
                    for (int i = 0; i < report.Issues.Count; i++)
                    {
                        var issue = report.Issues[i];
                        if (issue.Severity == AGISValidationSeverity.Error)
                            trace.Error(issue.ToString());
                        else if (issue.Severity == AGISValidationSeverity.Warning)
                            trace.Warn(issue.ToString());
                        else
                            trace.Info(issue.ToString());
                    }
                }
                return;
            }

            slot.compiledGraph = _cache.GetOrCompile(graph);

            var policyRuntime = new AGISTransitionPolicyRuntime();
            slot.instance = new AGISStateMachineInstance(_actor.Context, slot.compiledGraph, _evaluator, _nodeFactory, policyRuntime, trace);
            slot.instance.StartAtEntry();
        }

        private void StopAllSlots()
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Count; i++)
                slots[i]?.instance?.Stop();
        }

        private void TickInternal(float frameDt)
        {
            if (slots == null || slots.Count == 0)
                return;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || !slot.enabled || slot.instance == null)
                    continue;

                var slotHz = Mathf.Max(1f, slot.GetTickHz(tickHz));
                var slotTickDt = 1f / slotHz;

                slot.tickAccumulator += frameDt;

                while (slot.tickAccumulator >= slotTickDt)
                {
                    slot.tickAccumulator -= slotTickDt;
                    slot.instance.Tick(slotTickDt, slot.GetMaxTransitionsPerTick(maxTransitionsPerTick));
                }
            }
        }

        // -------------------------
        // Group asset resolution
        // -------------------------

        private void BuildGroupIndex()
        {
            _groupIndex = new Dictionary<AGISGuid, AGISGroupedStateAsset>();
            if (knownGroupedAssets == null) knownGroupedAssets = new List<AGISGroupedStateAsset>();

            for (int i = 0; i < knownGroupedAssets.Count; i++)
            {
                var g = knownGroupedAssets[i];
                if (g == null) continue;
                if (!g.groupAssetId.IsValid) continue;
                _groupIndex[g.groupAssetId] = g;
            }
        }

        private AGISGroupedStateAsset ResolveGroupAsset(AGISGuid id)
        {
            if (_groupIndex != null && id.IsValid && _groupIndex.TryGetValue(id, out var g))
                return g;
            return null;
        }

        private void EnsureBuiltInStructuralNodeTypes()
        {
            if (!NodeTypes.TryGet(AGISParallelNodeType.TYPE_ID, out _))
                NodeTypes.Register(new AGISParallelNodeType(_nodeFactory, trace));

            if (!NodeTypes.TryGet(AGISGroupedNodeType.TYPE_ID, out _))
            {
                var applier = new AGISParamTargetApplier(NodeTypes, ConditionTypes, trace);
                var binder = new AGISGroupedParamBinder(applier, trace);

                NodeTypes.Register(new AGISGroupedNodeType(ResolveGroupAsset, _compiler, _evaluator, _nodeFactory, binder, trace));
            }
        }

        // ── Persistent state population ───────────────────────────────────────────────

        /// <summary>
        /// Ensures AGISActorState is populated with all persistent keys before any node
        /// runtimes are created. Two discovery sources are scanned:
        ///   1. IAGISPersistentComponent  — MonoBehaviours on the actor GameObject.
        ///      Keys are ensured regardless of which graph is active.
        ///   2. IAGISPersistentNodeType   — node types found inside the slot graphs
        ///      (recursing into grouped sub-graphs and parallel children).
        /// In both cases EnsureKey semantics apply: existing values are never overwritten.
        /// Adds AGISActorState to the actor if absent.
        /// </summary>
        private void PopulateActorState()
        {
            var actorState = _actor.gameObject.GetComponent<AGISActorState>()
                          ?? _actor.gameObject.AddComponent<AGISActorState>();

            // Scan MonoBehaviour components on the actor that declare persistent keys.
            // IAGISPersistentNodeType is now constraint-free — both node types and
            // MonoBehaviours can implement it. These keys are always ensured regardless
            // of which graph is currently active.
            var persistentComponents = _actor.gameObject.GetComponents<IAGISPersistentNodeType>();
            for (int i = 0; i < persistentComponents.Length; i++)
            {
                var specs = persistentComponents[i]?.PersistentParams;
                if (specs == null) continue;
                for (int j = 0; j < specs.Count; j++)
                {
                    var spec = specs[j];
                    if (spec != null && spec.IsKeyValid)
                        actorState.EnsureKey(spec.key, spec.defaultValue);
                }
            }

            // Scan slot graphs (recursing into grouped sub-graphs) for IAGISPersistentNodeType.
            if (slots == null) return;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || !slot.enabled) continue;

                var graph = slot.GetGraphDef();
                if (graph != null)
                    ScanGraphForPersistentParams(graph, actorState);
            }
        }

        private void ScanGraphForPersistentParams(AGISStateMachineGraph graph, AGISActorState actorState)
        {
            if (graph?.nodes == null) return;
            for (int i = 0; i < graph.nodes.Count; i++)
                ScanNodeForPersistentParams(graph.nodes[i], actorState);
        }

        private void ScanNodeForPersistentParams(AGISNodeInstanceDef node, AGISActorState actorState)
        {
            if (node == null) return;

            // Register persistent params declared by this node's type.
            if (!string.IsNullOrEmpty(node.nodeTypeId) &&
                NodeTypes.TryGet(node.nodeTypeId, out var nodeType) &&
                nodeType is IAGISPersistentNodeType persistent)
            {
                var specs = persistent.PersistentParams;
                if (specs != null)
                    for (int i = 0; i < specs.Count; i++)
                        if (specs[i] != null && specs[i].IsKeyValid)
                            actorState.EnsureKey(specs[i].key, specs[i].defaultValue);
            }

            // Recurse into grouped sub-graphs.
            if (node.nodeTypeId == AGISGroupedNodeType.TYPE_ID && node.groupAssetId.IsValid)
            {
                var asset = ResolveGroupAsset(node.groupAssetId);
                if (asset?.internalGraph != null)
                    ScanGraphForPersistentParams(asset.internalGraph, actorState);
            }

            // Recurse into parallel children.
            if (node.parallelChildren != null)
                for (int i = 0; i < node.parallelChildren.Count; i++)
                    ScanNodeForPersistentParams(node.parallelChildren[i], actorState);
        }

        // ── Component dependency resolution ───────────────────────────────────────────

        /// <summary>
        /// Scans all slot graphs for node types that implement IAGISNodeComponentRequirements.
        /// For each, resolves the node's instance params (overrides merged with schema defaults)
        /// and passes them to GetRequiredComponents. Any missing types are added to the actor
        /// via AGISActorComponentFixer.
        /// </summary>
        private void ResolveComponentDependencies()
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || !slot.enabled) continue;

                var graph = slot.GetGraphDef();
                if (graph != null)
                    ScanGraphForComponentRequirements(graph);
            }
        }

        private void ScanGraphForComponentRequirements(AGISStateMachineGraph graph)
        {
            if (graph?.nodes == null) return;
            for (int i = 0; i < graph.nodes.Count; i++)
                ScanNodeForComponentRequirements(graph.nodes[i]);
        }

        private void ScanNodeForComponentRequirements(AGISNodeInstanceDef node)
        {
            if (node == null) return;

            if (!string.IsNullOrEmpty(node.nodeTypeId) &&
                NodeTypes.TryGet(node.nodeTypeId, out var nodeType) &&
                nodeType is IAGISNodeComponentRequirements compReqs)
            {
                // Resolve instance params (overrides merged with schema defaults) so the
                // node type can make param-gated decisions (e.g. only require NPCDetectionCone
                // when use_detection_memory = true).
                var accessor = AGISParamResolver.BuildAccessor(nodeType.Schema, node.@params);
                var required = compReqs.GetRequiredComponents(accessor);

                if (required != null && required.Count > 0)
                    AGISActorComponentFixer.EnsureComponents(_actor.gameObject, required);
            }

            // Recurse into grouped sub-graphs.
            if (node.nodeTypeId == AGISGroupedNodeType.TYPE_ID && node.groupAssetId.IsValid)
            {
                var asset = ResolveGroupAsset(node.groupAssetId);
                if (asset?.internalGraph != null)
                    ScanGraphForComponentRequirements(asset.internalGraph);
            }

            // Recurse into parallel children.
            if (node.parallelChildren != null)
                for (int i = 0; i < node.parallelChildren.Count; i++)
                    ScanNodeForComponentRequirements(node.parallelChildren[i]);
        }
    }
}
