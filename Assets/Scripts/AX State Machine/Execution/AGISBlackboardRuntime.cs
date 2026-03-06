// File: AGISBlackboardRuntime.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Minimal runtime blackboard implementation (canvas-aligned: runtime memory/shared facts).

using System;
using System.Collections.Generic;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISBlackboardRuntime : IAGISBlackboard
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>(StringComparer.Ordinal);

        public bool TryGet<T>(string key, out T value)
        {
            if (!string.IsNullOrEmpty(key) && _data.TryGetValue(key, out var boxed) && boxed is T t)
            {
                value = t;
                return true;
            }

            value = default;
            return false;
        }

        public void Set<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            _data[key] = value;
        }

        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            return _data.Remove(key);
        }

        public void Clear() => _data.Clear();
    }
}
