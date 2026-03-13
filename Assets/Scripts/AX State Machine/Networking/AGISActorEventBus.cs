// File: AGISActorEventBus.cs
// Folder: Assets/Scripts/AX State Machine/Networking/
// Purpose: Pure-C# singleton event bus for cross-actor coordination.
//          Actors register on Awake via AGISActorRegistry.
//          Any state machine node can Emit an event with a scope and key-value payload.
//          The bus resolves scope → actor set → writes AGISActorState values directly.
//          Subscription callbacks are also supported for immediate effects.

using System;
using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Networking
{
    public enum AGISEventScopeKind
    {
        Single    = 0,  // Target one specific actor by instanceId
        Group     = 1,  // Target all actors in a named group
        Broadcast = 2,  // Target all registered actors
    }

    public struct AGISEventScope
    {
        public AGISEventScopeKind Kind;
        /// <summary>
        /// For Single: the target actor's instance ID as string.
        /// For Group: the group ID string.
        /// For Broadcast: empty.
        /// </summary>
        public string TargetId;
    }

    public readonly struct AGISEventPayload
    {
        public readonly string EventType;
        public readonly AGISEventScope Scope;
        public readonly (string key, AGISValue value)[] Entries;

        public AGISEventPayload(string eventType, AGISEventScope scope, (string key, AGISValue value)[] entries)
        {
            EventType = eventType;
            Scope     = scope;
            Entries   = entries;
        }
    }

    public sealed class AGISActorEventBus
    {
        // ── Singleton ─────────────────────────────────────────────────────────────────

        private static AGISActorEventBus _instance;
        private static readonly object _lock = new object();

        public static AGISActorEventBus Instance
        {
            get
            {
                if (_instance == null)
                    lock (_lock) { _instance ??= new AGISActorEventBus(); }
                return _instance;
            }
        }

        // ── Subscriptions ─────────────────────────────────────────────────────────────

        // eventType → (instanceId → callback)
        private readonly Dictionary<string, Dictionary<int, Action<AGISEventPayload>>> _subs
            = new Dictionary<string, Dictionary<int, Action<AGISEventPayload>>>(StringComparer.Ordinal);

        // ── API ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Emit an event. Resolves scope to a set of actors via AGISActorRegistry,
        /// writes each payload key-value into their AGISActorState, then fires callbacks.
        /// </summary>
        public void Emit(string eventType, AGISEventScope scope, (string key, AGISValue value)[] payload)
        {
            if (string.IsNullOrEmpty(eventType)) return;
            payload ??= Array.Empty<(string, AGISValue)>();

            var targets = ResolveTargets(scope);
            foreach (var actorState in targets)
            {
                if (actorState == null) continue;
                foreach (var (key, value) in payload)
                    if (!string.IsNullOrEmpty(key))
                        actorState.Set(key, value);
            }

            // Fire callbacks.
            var eventPayload = new AGISEventPayload(eventType, scope, payload);
            FireCallbacks(eventType, eventPayload);
        }

        public void Subscribe(string eventType, int instanceId, Action<AGISEventPayload> handler)
        {
            if (string.IsNullOrEmpty(eventType) || handler == null) return;

            lock (_lock)
            {
                if (!_subs.TryGetValue(eventType, out var dict))
                {
                    dict = new Dictionary<int, Action<AGISEventPayload>>();
                    _subs[eventType] = dict;
                }
                dict[instanceId] = handler;
            }
        }

        public void Unsubscribe(string eventType, int instanceId)
        {
            if (string.IsNullOrEmpty(eventType)) return;

            lock (_lock)
            {
                if (_subs.TryGetValue(eventType, out var dict))
                    dict.Remove(instanceId);
            }
        }

        public void Clear()
        {
            lock (_lock) { _subs.Clear(); }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private IReadOnlyList<AGISActorState> ResolveTargets(AGISEventScope scope)
        {
            var registry = AGISActorRegistry.Instance;

            switch (scope.Kind)
            {
                case AGISEventScopeKind.Single:
                    if (int.TryParse(scope.TargetId, out int id) &&
                        registry.TryGetActorState(id, out var single))
                        return new[] { single };
                    return Array.Empty<AGISActorState>();

                case AGISEventScopeKind.Group:
                    return registry.GetGroup(scope.TargetId ?? string.Empty);

                case AGISEventScopeKind.Broadcast:
                default:
                    return registry.GetAll();
            }
        }

        private void FireCallbacks(string eventType, AGISEventPayload payload)
        {
            Dictionary<int, Action<AGISEventPayload>> dict;
            lock (_lock)
            {
                if (!_subs.TryGetValue(eventType, out dict)) return;
                // Copy to avoid modification during iteration.
                dict = new Dictionary<int, Action<AGISEventPayload>>(dict);
            }

            foreach (var handler in dict.Values)
            {
                try { handler(payload); }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
