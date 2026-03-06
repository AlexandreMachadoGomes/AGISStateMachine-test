// File: AGISActorState.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Persistent, serializable key-value store that lives on the actor GameObject.
//          Unlike AGISBlackboardRuntime (transient, in-memory, starts empty every time),
//          AGISActorState is serialized by Unity — values survive play-mode restarts,
//          prefab instantiation, and transitions between states.
//
// Lifecycle:
//   - AGISStateMachineRunner.Awake() scans all graphs for IAGISPersistentNodeType
//     implementations and calls EnsureKey for each declared param.
//   - EnsureKey adds the key with its default value ONLY if not already present,
//     so existing values (e.g. a resumed patrol index) are never overwritten on startup.
//   - Node runtimes read and write values directly via Get/Set.
//
// Usage in node types:
//   var state = ctx.Actor.GetComponent<AGISActorState>();
//   int idx = state.GetInt("npc.route.sequence_index");
//   state.Set("npc.route.sequence_index", AGISValue.FromInt(idx + 1));

using UnityEngine;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AGISActorState : MonoBehaviour
    {
        [Tooltip("Persistent state entries. Populated automatically by AGISStateMachineRunner on startup. " +
                 "Values survive state transitions and (when serialized) play-mode restarts.")]
        [SerializeField] private AGISParamTable _values = new AGISParamTable();

        // ── Write ─────────────────────────────────────────────────────────────────────

        public void Set(string key, AGISValue value) => _values.Set(key, value);

        /// <summary>
        /// Adds <paramref name="key"/> with <paramref name="defaultValue"/> only if the key
        /// is not already present. Called by the runner at compile time.
        /// </summary>
        public void EnsureKey(string key, AGISValue defaultValue)
        {
            if (!_values.TryGet(key, out _))
                _values.Set(key, defaultValue);
        }

        // ── Read ──────────────────────────────────────────────────────────────────────

        public AGISValue Get(string key, AGISValue fallback = default)
        {
            return _values.TryGet(key, out var v) ? v : fallback;
        }

        public bool   GetBool  (string key, bool   fallback = false) => Get(key, AGISValue.FromBool(fallback)).AsBool(fallback);
        public int    GetInt   (string key, int    fallback = 0)     => Get(key, AGISValue.FromInt(fallback)).AsInt(fallback);
        public float  GetFloat (string key, float  fallback = 0f)    => Get(key, AGISValue.FromFloat(fallback)).AsFloat(fallback);
        public string GetString(string key, string fallback = "")    => Get(key, AGISValue.FromString(fallback)).AsString(fallback);

        // ── Debug ─────────────────────────────────────────────────────────────────────

        [ContextMenu("Log State")]
        private void LogState()
        {
            if (_values?.values == null || _values.values.Count == 0)
            {
                Debug.Log($"[AGISActorState] {name}: (empty)");
                return;
            }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[AGISActorState] {name}:");
            for (int i = 0; i < _values.values.Count; i++)
            {
                var pv = _values.values[i];
                if (pv != null)
                    sb.AppendLine($"  {pv.key} ({pv.value.Type}) = {FormatValue(pv.value)}");
            }
            Debug.Log(sb.ToString());
        }

        private static string FormatValue(AGISValue v)
        {
            switch (v.Type)
            {
                case AGISParamType.Bool:    return v.AsBool().ToString();
                case AGISParamType.Int:     return v.AsInt().ToString();
                case AGISParamType.Float:   return v.AsFloat().ToString("F3");
                case AGISParamType.String:  return $"\"{v.AsString()}\"";
                case AGISParamType.Vector2: return v.AsVector2().ToString();
                case AGISParamType.Vector3: return v.AsVector3().ToString();
                default:                   return v.Type.ToString();
            }
        }
    }
}
