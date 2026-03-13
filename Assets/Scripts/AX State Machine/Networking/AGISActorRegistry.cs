// File: AGISActorRegistry.cs
// Folder: Assets/Scripts/AX State Machine/Networking/
// Purpose: Thread-safe singleton registry that maps actor instance IDs to their AGISActorState
//          and group membership. Used by AGISActorEventBus to resolve scope → actor set.
//          AGISStateMachineRunner registers/unregisters actors in Awake/OnDestroy.

using System.Collections.Generic;
using AGIS.ESM.Runtime;

namespace AGIS.ESM.Networking
{
    public sealed class AGISActorRegistry
    {
        // ── Singleton ─────────────────────────────────────────────────────────────────

        private static AGISActorRegistry _instance;
        private static readonly object _lock = new object();

        public static AGISActorRegistry Instance
        {
            get
            {
                if (_instance == null)
                    lock (_lock) { _instance ??= new AGISActorRegistry(); }
                return _instance;
            }
        }

        // ── Data ──────────────────────────────────────────────────────────────────────

        private sealed class ActorEntry
        {
            public AGISActorState ActorState;
            public string[] GroupIds;
        }

        private readonly Dictionary<int, ActorEntry> _actorsByInstanceId = new Dictionary<int, ActorEntry>();
        private readonly Dictionary<string, List<AGISActorState>> _groups = new Dictionary<string, List<AGISActorState>>();

        // ── Registration ──────────────────────────────────────────────────────────────

        public void Register(int instanceId, AGISActorState actorState, string[] groupIds)
        {
            if (actorState == null) return;

            lock (_lock)
            {
                if (_actorsByInstanceId.ContainsKey(instanceId))
                    UnregisterInternal(instanceId);

                _actorsByInstanceId[instanceId] = new ActorEntry
                {
                    ActorState = actorState,
                    GroupIds   = groupIds ?? System.Array.Empty<string>(),
                };

                if (groupIds != null)
                    foreach (var gid in groupIds)
                    {
                        if (string.IsNullOrEmpty(gid)) continue;
                        if (!_groups.TryGetValue(gid, out var list))
                        {
                            list = new List<AGISActorState>();
                            _groups[gid] = list;
                        }
                        if (!list.Contains(actorState))
                            list.Add(actorState);
                    }
            }
        }

        public void Unregister(int instanceId)
        {
            lock (_lock) { UnregisterInternal(instanceId); }
        }

        private void UnregisterInternal(int instanceId)
        {
            if (!_actorsByInstanceId.TryGetValue(instanceId, out var entry)) return;

            _actorsByInstanceId.Remove(instanceId);

            foreach (var gid in entry.GroupIds)
            {
                if (string.IsNullOrEmpty(gid)) continue;
                if (_groups.TryGetValue(gid, out var list))
                {
                    list.Remove(entry.ActorState);
                    if (list.Count == 0)
                        _groups.Remove(gid);
                }
            }
        }

        // ── Lookup ────────────────────────────────────────────────────────────────────

        public bool TryGetActorState(int instanceId, out AGISActorState actorState)
        {
            lock (_lock)
            {
                if (_actorsByInstanceId.TryGetValue(instanceId, out var entry))
                {
                    actorState = entry.ActorState;
                    return true;
                }
                actorState = null;
                return false;
            }
        }

        public IReadOnlyList<AGISActorState> GetGroup(string groupId)
        {
            lock (_lock)
            {
                return _groups.TryGetValue(groupId, out var list)
                    ? list.AsReadOnly()
                    : System.Array.Empty<AGISActorState>();
            }
        }

        public IReadOnlyList<AGISActorState> GetAll()
        {
            lock (_lock)
            {
                var all = new List<AGISActorState>(_actorsByInstanceId.Count);
                foreach (var entry in _actorsByInstanceId.Values)
                    all.Add(entry.ActorState);
                return all;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _actorsByInstanceId.Clear();
                _groups.Clear();
            }
        }
    }
}
