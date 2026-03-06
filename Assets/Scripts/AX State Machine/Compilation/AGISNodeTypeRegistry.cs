// File: AGISNodeTypeRegistry.cs
// Folder: Assets/Scripts/AX State Machine/Compilation/
// Purpose: Single source of truth mapping nodeTypeId -> NodeType (used by runtime + editor + compiler + validator).

using System;
using System.Collections.Generic;
using System.Reflection;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISNodeTypeRegistry
    {
        private readonly Dictionary<string, IAGISNodeType> _types = new Dictionary<string, IAGISNodeType>(StringComparer.Ordinal);

        public IEnumerable<IAGISNodeType> AllTypes => _types.Values;

        public void Register(IAGISNodeType type)
        {
            if (type == null || string.IsNullOrEmpty(type.TypeId))
                throw new ArgumentException("Invalid node type.");

            _types[type.TypeId] = type;
        }

        public bool TryGet(string typeId, out IAGISNodeType type)
        {
            if (string.IsNullOrEmpty(typeId))
            {
                type = null;
                return false;
            }
            return _types.TryGetValue(typeId, out type);
        }

        public IAGISNodeType GetOrThrow(string typeId)
        {
            if (!TryGet(typeId, out var type))
                throw new KeyNotFoundException($"NodeType '{typeId}' not registered.");
            return type;
        }

        /// <summary>
        /// Optional convenience: Register all IAGISNodeType with parameterless constructors.
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
                    if (!typeof(IAGISNodeType).IsAssignableFrom(t)) continue;

                    if (t.GetConstructor(Type.EmptyTypes) == null) continue;

                    var inst = (IAGISNodeType)Activator.CreateInstance(t);
                    Register(inst);
                    count++;
                }
            }

            return count;
        }
    }
}
