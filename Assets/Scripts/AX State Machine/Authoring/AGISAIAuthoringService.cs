// File: AGISAIAuthoringService.cs
// Folder: Assets/Scripts/AX State Machine/Authoring/
// Purpose: Programmatic API for the AI authoring layer.
//          An AI (or any external system) uses this class to:
//            - Define new state and condition types from code bodies
//            - Build and modify state machine graphs
//            - Query the available type catalogue
//          All operations go through the same pipeline the visual editor uses.

using System;
using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.Runtime;

namespace AGIS.ESM.Authoring
{
    /// <summary>
    /// Result returned by <see cref="AGISAIAuthoringService.DefineNodeType"/> and
    /// <see cref="AGISAIAuthoringService.DefineConditionType"/>.
    /// </summary>
    public sealed class AGISDefineTypeResult
    {
        public readonly bool     Success;
        public readonly string   TypeId;
        public readonly string   Guid;
        public readonly string[] Errors;
        public readonly string[] Warnings;
        public readonly string   GeneratedSource;

        public AGISDefineTypeResult(bool success, string typeId, string guid, string[] errors, string[] warnings, string source)
        {
            Success         = success;
            TypeId          = typeId;
            Guid            = guid;
            Errors          = errors   ?? Array.Empty<string>();
            Warnings        = warnings ?? Array.Empty<string>();
            GeneratedSource = source;
        }
    }

    /// <summary>
    /// Singleton service.  Call <c>AGISAIAuthoringService.Configure(...)</c> once at startup
    /// (after the runner is set up) to wire it to the live registries and store.
    /// </summary>
    public sealed class AGISAIAuthoringService
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        private static AGISAIAuthoringService _instance;
        public static AGISAIAuthoringService Instance
        {
            get
            {
                _instance ??= new AGISAIAuthoringService();
                return _instance;
            }
        }

        // ── Dependencies ──────────────────────────────────────────────────────
        private AGISNodeTypeRegistry      _nodeRegistry;
        private AGISConditionTypeRegistry _condRegistry;
        private AGISAuthoredTypeStore     _store;

        private AGISAIAuthoringService() { }

        /// <summary>
        /// Wire the service to live registries and a persistent store.
        /// Call once during game startup before any AI authoring is attempted.
        /// </summary>
        public static void Configure(
            AGISNodeTypeRegistry      nodeRegistry,
            AGISConditionTypeRegistry condRegistry,
            AGISAuthoredTypeStore     store = null)
        {
            Instance._nodeRegistry = nodeRegistry;
            Instance._condRegistry = condRegistry;
            Instance._store        = store;
        }

        // ── Type authoring ────────────────────────────────────────────────────

        /// <summary>
        /// Define, compile, and register a new node type.
        /// The type is available in all registries immediately after a successful call.
        /// If a <see cref="AGISAuthoredTypeStore"/> is configured, the record is persisted.
        /// </summary>
        public AGISDefineTypeResult DefineNodeType(
            string                            displayName,
            string                            typeId,
            IReadOnlyList<AGISSchemaParamDef> schemaParams = null,
            string                            privateFields  = null,
            string                            ctorBody       = null,
            string                            enterBody      = null,
            string                            tickBody       = null,
            string                            exitBody       = null,
            string                            helperMethods  = null)
        {
            if (string.IsNullOrEmpty(typeId))
                return Fail(typeId, "typeId must not be empty.");

            string source = AGISNodeTypeCodeTemplate.Generate(
                displayName, typeId, schemaParams,
                privateFields, ctorBody,
                enterBody, tickBody, exitBody,
                helperMethods);

            string guid = AGISGuid.New().ToString();
            var compileResult = AGISRoslynCompiler.Compile(source, "AGISAuthored_" + guid.Replace("-", ""));

            if (!compileResult.Success)
                return new AGISDefineTypeResult(false, typeId, guid, compileResult.Errors, compileResult.Warnings, source);

            bool registered = TryRegisterNodeType(compileResult.Assembly, typeId);
            if (!registered)
                return Fail(typeId, $"Compiled successfully but could not find an IAGISNodeType implementation in the output assembly. Check that typeId '{typeId}' matches the generated class.");

            PersistRecord(guid, typeId, displayName, AGISAuthoredTypeKind.NodeType,
                schemaParams, source, privateFields, ctorBody, enterBody, tickBody, exitBody, helperMethods, null);

            return new AGISDefineTypeResult(true, typeId, guid, compileResult.Errors, compileResult.Warnings, source);
        }

        /// <summary>
        /// Define, compile, and register a new condition type.
        /// </summary>
        public AGISDefineTypeResult DefineConditionType(
            string                            displayName,
            string                            typeId,
            IReadOnlyList<AGISSchemaParamDef> schemaParams  = null,
            string                            evaluateBody  = null,
            string                            helperMethods = null)
        {
            if (string.IsNullOrEmpty(typeId))
                return Fail(typeId, "typeId must not be empty.");

            string source = AGISConditionTypeCodeTemplate.Generate(
                displayName, typeId, schemaParams, evaluateBody, helperMethods);

            string guid = AGISGuid.New().ToString();
            var compileResult = AGISRoslynCompiler.Compile(source, "AGISAuthored_" + guid.Replace("-", ""));

            if (!compileResult.Success)
                return new AGISDefineTypeResult(false, typeId, guid, compileResult.Errors, compileResult.Warnings, source);

            bool registered = TryRegisterConditionType(compileResult.Assembly, typeId);
            if (!registered)
                return Fail(typeId, $"Compiled successfully but could not find an IAGISConditionType implementation in the output assembly.");

            PersistRecord(guid, typeId, displayName, AGISAuthoredTypeKind.ConditionType,
                schemaParams, source, null, null, null, null, null, helperMethods, evaluateBody);

            return new AGISDefineTypeResult(true, typeId, guid, compileResult.Errors, compileResult.Warnings, source);
        }

        // ── Graph building helpers ────────────────────────────────────────────

        /// <summary>
        /// Create a new empty graph with an entry node of the given type.
        /// Returns a ready-to-use <see cref="AGISStateMachineGraph"/>.
        /// </summary>
        public static AGISStateMachineGraph CreateGraph(string entryNodeTypeId)
        {
            var graph = new AGISStateMachineGraph
            {
                nodes = new System.Collections.Generic.List<AGISNodeInstanceDef>(),
                edges = new System.Collections.Generic.List<AGISTransitionEdgeDef>(),
            };

            var entry = AddNode(graph, entryNodeTypeId, position: new Vector2(200, 200));
            graph.entryNodeId = entry.nodeId;
            return graph;
        }

        /// <summary>Add a node of the given type at the specified canvas position.</summary>
        public static AGISNodeInstanceDef AddNode(
            AGISStateMachineGraph graph,
            string typeId,
            Vector2 position = default,
            AGISParamTable overrides = null)
        {
            var def = new AGISNodeInstanceDef
            {
                nodeId     = AGISGuid.New(),
                nodeTypeId = typeId,
                @params    = overrides ?? new AGISParamTable(),
            };
            def.visual.position = position;
            graph.nodes ??= new System.Collections.Generic.List<AGISNodeInstanceDef>();
            graph.nodes.Add(def);
            return def;
        }

        /// <summary>Add a transition edge between two nodes.</summary>
        public static AGISTransitionEdgeDef AddEdge(
            AGISStateMachineGraph graph,
            AGISGuid fromNodeId,
            AGISGuid toNodeId,
            AGISConditionExprDef condition = null,
            int priority = 0)
        {
            var edge = new AGISTransitionEdgeDef
            {
                edgeId     = AGISGuid.New(),
                fromNodeId = fromNodeId,
                toNodeId   = toNodeId,
                condition  = condition ?? AGISConditionExprDef.True(),
                priority   = priority,
            };
            graph.edges ??= new System.Collections.Generic.List<AGISTransitionEdgeDef>();
            graph.edges.Add(edge);
            return edge;
        }

        // ── Catalogue queries ─────────────────────────────────────────────────

        /// <summary>Returns all registered node type IDs and display names.</summary>
        public IEnumerable<(string typeId, string displayName)> GetAvailableNodeTypes()
        {
            if (_nodeRegistry == null) yield break;
            foreach (var t in _nodeRegistry.AllTypes)
                yield return (t.TypeId, t.DisplayName);
        }

        /// <summary>Returns all registered condition type IDs and display names.</summary>
        public IEnumerable<(string typeId, string displayName)> GetAvailableConditionTypes()
        {
            if (_condRegistry == null) yield break;
            foreach (var t in _condRegistry.AllTypes)
                yield return (t.TypeId, t.DisplayName);
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private bool TryRegisterNodeType(System.Reflection.Assembly assembly, string expectedTypeId)
        {
            foreach (var t in assembly.GetTypes())
            {
                if (!typeof(IAGISNodeType).IsAssignableFrom(t) || t.IsAbstract || t.IsInterface) continue;
                try
                {
                    var instance = (IAGISNodeType)Activator.CreateInstance(t);
                    _nodeRegistry?.Register(instance);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            return false;
        }

        private bool TryRegisterConditionType(System.Reflection.Assembly assembly, string expectedTypeId)
        {
            foreach (var t in assembly.GetTypes())
            {
                if (!typeof(IAGISConditionType).IsAssignableFrom(t) || t.IsAbstract || t.IsInterface) continue;
                try
                {
                    var instance = (IAGISConditionType)Activator.CreateInstance(t);
                    _condRegistry?.Register(instance);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            return false;
        }

        private void PersistRecord(
            string                            guid,
            string                            typeId,
            string                            displayName,
            AGISAuthoredTypeKind              kind,
            IReadOnlyList<AGISSchemaParamDef> schemaParams,
            string                            source,
            string                            privateFields,
            string                            ctorBody,
            string                            enterBody,
            string                            tickBody,
            string                            exitBody,
            string                            helperMethods,
            string                            evaluateBody)
        {
            if (_store == null) return;

            var record = new AGISAuthoredTypeRecord
            {
                guid         = guid,
                typeId       = typeId,
                displayName  = displayName,
                kind         = kind,
                fullSource   = source,
                privateFields = privateFields,
                ctorBody      = ctorBody,
                enterBody     = enterBody,
                tickBody      = tickBody,
                exitBody      = exitBody,
                helperMethods = helperMethods,
                evaluateBody  = evaluateBody,
                lastModifiedUtc = DateTime.UtcNow.ToString("o"),
            };

            if (schemaParams != null)
            {
                record.schemaParams = new System.Collections.Generic.List<AGISAuthoredParamRecord>();
                foreach (var p in schemaParams)
                {
                    if (p == null) continue;
                    record.schemaParams.Add(new AGISAuthoredParamRecord
                    {
                        key                 = p.Key,
                        type                = p.Type,
                        defaultValueLiteral = p.DefaultValueLiteral,
                        displayName         = p.DisplayName,
                        tooltip             = p.Tooltip,
                    });
                }
            }

            _store.Upsert(record);
        }

        private static AGISDefineTypeResult Fail(string typeId, string message)
            => new AGISDefineTypeResult(false, typeId, null, new[] { message }, Array.Empty<string>(), null);
    }
}
