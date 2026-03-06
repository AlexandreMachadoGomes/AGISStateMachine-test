// File: AGISParamSchema.cs
// Folder: Assets/Scripts/AX State Machine/Compilation/
// Purpose: Collection of ParamSpecs for a NodeType or ConditionType; used by editor/runtime/validator.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGIS.ESM.UGC.Params
{
    [Serializable]
    public sealed class AGISParamSchema
    {
        [SerializeField] public int schemaVersion = 1;

        [SerializeField] public List<AGISParamSpec> specs = new List<AGISParamSpec>();

        [NonSerialized] private Dictionary<string, AGISParamSpec> _byKey;

        public void InvalidateCache() => _byKey = null;

        public IReadOnlyList<AGISParamSpec> Specs => specs;

        public bool TryGetSpec(string key, out AGISParamSpec spec)
        {
            EnsureCache();
            return _byKey.TryGetValue(key, out spec);
        }

        public AGISParamSpec GetSpecOrNull(string key)
        {
            EnsureCache();
            _byKey.TryGetValue(key, out var spec);
            return spec;
        }

        public void AddOrReplace(AGISParamSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.key))
                return;

            EnsureCache();

            if (_byKey.TryGetValue(spec.key, out var existing))
            {
                for (int i = 0; i < specs.Count; i++)
                {
                    if (ReferenceEquals(specs[i], existing))
                    {
                        specs[i] = spec;
                        break;
                    }
                }
                _byKey[spec.key] = spec;
            }
            else
            {
                specs.Add(spec);
                _byKey[spec.key] = spec;
            }
        }

        public IEnumerable<string> Keys()
        {
            EnsureCache();
            return _byKey.Keys;
        }

        private void EnsureCache()
        {
            if (_byKey != null)
                return;

            _byKey = new Dictionary<string, AGISParamSpec>(StringComparer.Ordinal);
            if (specs == null)
                specs = new List<AGISParamSpec>();

            for (int i = 0; i < specs.Count; i++)
            {
                var s = specs[i];
                if (s == null || string.IsNullOrEmpty(s.key))
                    continue;

                // Keep last duplicate.
                _byKey[s.key] = s;
            }
        }
    }
}
