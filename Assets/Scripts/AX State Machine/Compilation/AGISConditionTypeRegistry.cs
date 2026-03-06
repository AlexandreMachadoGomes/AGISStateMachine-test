// File: AGISConditionTypeRegistry.cs
// Folder: Assets/Scripts/AX State Machine/Compilation/
// Purpose: Single source of truth mapping conditionTypeId -> ConditionType (used by evaluator + editor + validator).

using System;
using System.Collections.Generic;
using System.Reflection;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISConditionTypeRegistry
    {
        private readonly Dictionary<string, IAGISConditionType> _types = new Dictionary<string, IAGISConditionType>(StringComparer.Ordinal);

        public IEnumerable<IAGISConditionType> AllTypes => _types.Values;

        public void Register(IAGISConditionType type)
        {
            if (type == null || string.IsNullOrEmpty(type.TypeId))
                throw new ArgumentException("Invalid condition type.");

            _types[type.TypeId] = type;
        }

        public bool TryGet(string typeId, out IAGISConditionType type)
        {
            if (string.IsNullOrEmpty(typeId))
            {
                type = null;
                return false;
            }
            return _types.TryGetValue(typeId, out type);
        }

        public IAGISConditionType GetOrThrow(string typeId)
        {
            if (!TryGet(typeId, out var type))
                throw new KeyNotFoundException($"ConditionType '{typeId}' not registered.");
            return type;
        }

        /// <summary>
        /// Optional convenience: Register all IAGISConditionType with parameterless constructors.
        /// Useful for editor tooling; use carefully for AOT targets.
        /// </summary>
        public int RegisterAllFromAssemblies(params Assembly[] assemblies)
        {
            int count = 0;
            if (assemblies == null) return count;

            for (int a = 0; a < assemblies.Length; a++)
            {
                var asm = assemblies[a];
                if (asm == null) continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }

                if (types == null) continue;

                for (int i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    if (t == null) continue;
                    if (t.IsAbstract || t.IsInterface) continue;
                    if (!typeof(IAGISConditionType).IsAssignableFrom(t)) continue;

                    if (t.GetConstructor(Type.EmptyTypes) == null) continue;

                    var inst = (IAGISConditionType)Activator.CreateInstance(t);
                    Register(inst);
                    count++;
                }
            }

            return count;
        }
    }
}
