// File: AGISAuthoredTypeStore.cs
// Folder: Assets/Scripts/AX State Machine/Authoring/
// Purpose: ScriptableObject that persists authored type records across sessions.
//          At runtime, call RecompileAll(nodeRegistry, conditionRegistry) once at startup
//          to recompile all stored records and register the resulting types.
//
// Setup:
//   Create a store asset: Assets → Create → AGIS → Authored Type Store
//   Assign the store to AGISStateMachineRunner (or a bootstrapper MonoBehaviour).
//   The runner (or bootstrapper) calls store.RecompileAll(...) in Awake.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using AGIS.ESM.Runtime;

namespace AGIS.ESM.Authoring
{
    [CreateAssetMenu(fileName = "AGISAuthoredTypeStore", menuName = "AGIS/Authored Type Store")]
    public sealed class AGISAuthoredTypeStore : ScriptableObject
    {
        [SerializeField]
        private List<AGISAuthoredTypeRecord> _records = new List<AGISAuthoredTypeRecord>();

        // ── CRUD ──────────────────────────────────────────────────────────────

        public IReadOnlyList<AGISAuthoredTypeRecord> Records => _records;

        /// <summary>
        /// Add or replace a record by GUID.  Call record.RegenerateSource() before this.
        /// </summary>
        public void Upsert(AGISAuthoredTypeRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.guid)) return;
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i]?.guid == record.guid)
                {
                    _records[i] = record;
                    MarkDirty();
                    return;
                }
            }
            _records.Add(record);
            MarkDirty();
        }

        /// <summary>Remove a record by GUID.</summary>
        public bool Remove(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return false;
            int idx = _records.FindIndex(r => r?.guid == guid);
            if (idx < 0) return false;
            _records.RemoveAt(idx);
            MarkDirty();
            return true;
        }

        public AGISAuthoredTypeRecord Find(string guid)
        {
            foreach (var r in _records)
                if (r?.guid == guid) return r;
            return null;
        }

        // ── Recompile ─────────────────────────────────────────────────────────

        /// <summary>
        /// Recompile all stored records and register the resulting types in the supplied registries.
        /// Call once at startup (before any state machine compiles).
        /// </summary>
        /// <returns>Number of types successfully registered.</returns>
        public int RecompileAll(
            AGISNodeTypeRegistry      nodeRegistry,
            AGISConditionTypeRegistry condRegistry)
        {
            int count = 0;
            foreach (var record in _records)
            {
                if (record == null) continue;
                if (string.IsNullOrWhiteSpace(record.fullSource))
                {
                    record.RegenerateSource();
                    if (string.IsNullOrWhiteSpace(record.fullSource)) continue;
                }

                var result = AGISRoslynCompiler.Compile(record.fullSource, "AGISAuthored_" + record.guid);
                if (!result.Success)
                {
                    Debug.LogError($"[AGIS] Failed to recompile authored type '{record.displayName}' ({record.typeId}):\n" +
                                   string.Join("\n", result.Errors));
                    continue;
                }

                bool registered = TryRegisterFromAssembly(result.Assembly, record.kind, nodeRegistry, condRegistry);
                if (registered)
                    count++;
                else
                    Debug.LogWarning($"[AGIS] Compiled '{record.typeId}' but could not find a matching type in the assembly.");
            }
            return count;
        }

        /// <summary>
        /// Compile and register a single record immediately (used after authoring a new type live).
        /// </summary>
        public bool CompileAndRegisterOne(
            AGISAuthoredTypeRecord    record,
            AGISNodeTypeRegistry      nodeRegistry,
            AGISConditionTypeRegistry condRegistry,
            out AGISCompileResult     compileResult)
        {
            compileResult = null;
            if (record == null) return false;

            record.RegenerateSource();
            compileResult = AGISRoslynCompiler.Compile(record.fullSource, "AGISAuthored_" + record.guid);

            if (!compileResult.Success) return false;

            bool ok = TryRegisterFromAssembly(compileResult.Assembly, record.kind, nodeRegistry, condRegistry);
            if (ok) Upsert(record);
            return ok;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool TryRegisterFromAssembly(
            Assembly                  assembly,
            AGISAuthoredTypeKind      kind,
            AGISNodeTypeRegistry      nodeRegistry,
            AGISConditionTypeRegistry condRegistry)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (kind == AGISAuthoredTypeKind.NodeType &&
                    typeof(IAGISNodeType).IsAssignableFrom(type) &&
                    !type.IsAbstract && !type.IsInterface)
                {
                    var instance = (IAGISNodeType)Activator.CreateInstance(type);
                    nodeRegistry?.Register(instance);
                    return true;
                }

                if (kind == AGISAuthoredTypeKind.ConditionType &&
                    typeof(IAGISConditionType).IsAssignableFrom(type) &&
                    !type.IsAbstract && !type.IsInterface)
                {
                    var instance = (IAGISConditionType)Activator.CreateInstance(type);
                    condRegistry?.Register(instance);
                    return true;
                }
            }
            return false;
        }

        private void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
