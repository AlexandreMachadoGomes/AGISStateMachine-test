// File: EmitActorEventNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: Node type that fires an event to the AGISActorEventBus, writing payload values
//          into target actors' AGISActorState based on scope (single / group / broadcast).
//          TypeId: "agis.emit_event"

using System;
using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.Networking;

namespace AGIS.NPC.States
{
    public sealed class EmitActorEventNodeType : IAGISNodeType
    {
        public string TypeId      => "agis.emit_event";
        public string DisplayName => "Emit Actor Event";
        public AGISNodeKind Kind  => AGISNodeKind.Normal;

        public AGISParamSchema Schema { get; } = BuildSchema();

        private static AGISParamSchema BuildSchema()
        {
            var s = new AGISParamSchema();
            s.AddOrReplace(new AGISParamSpec { key = "event_type",   type = AGISParamType.String, defaultValue = AGISValue.FromString(""),         displayName = "Event Type",    tooltip = "Named event identifier." });
            s.AddOrReplace(new AGISParamSpec { key = "scope_kind",   type = AGISParamType.String, defaultValue = AGISValue.FromString("broadcast"), displayName = "Scope Kind",    tooltip = "\"single\", \"group\", or \"broadcast\"." });
            s.AddOrReplace(new AGISParamSpec { key = "scope_target", type = AGISParamType.String, defaultValue = AGISValue.FromString(""),         displayName = "Scope Target",  tooltip = "instanceId (single) or groupId (group). Empty for broadcast." });
            s.AddOrReplace(new AGISParamSpec { key = "payload_key_0",   type = AGISParamType.String, defaultValue = AGISValue.FromString(""), displayName = "Payload Key 0" });
            s.AddOrReplace(new AGISParamSpec { key = "payload_value_0", type = AGISParamType.String, defaultValue = AGISValue.FromString("actorstate"), displayName = "Payload Value 0", tooltip = "Literal or \"actorstate\" to read from emitter." });
            s.AddOrReplace(new AGISParamSpec { key = "payload_key_1",   type = AGISParamType.String, defaultValue = AGISValue.FromString(""), displayName = "Payload Key 1" });
            s.AddOrReplace(new AGISParamSpec { key = "payload_value_1", type = AGISParamType.String, defaultValue = AGISValue.FromString("actorstate"), displayName = "Payload Value 1" });
            s.AddOrReplace(new AGISParamSpec { key = "payload_key_2",   type = AGISParamType.String, defaultValue = AGISValue.FromString(""), displayName = "Payload Key 2" });
            s.AddOrReplace(new AGISParamSpec { key = "payload_value_2", type = AGISParamType.String, defaultValue = AGISValue.FromString("actorstate"), displayName = "Payload Value 2" });
            return s;
        }

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
            => new Runtime(args.Ctx, args.Params);

        private sealed class Runtime : IAGISNodeRuntime, IAGISNodeSignal
        {
            public bool IsComplete { get; private set; }

            private readonly AGISExecutionContext _ctx;
            private readonly string _eventType;
            private readonly string _scopeKind;
            private readonly string _scopeTarget;
            private readonly (string key, string valueSrc)[] _payload;

            public Runtime(AGISExecutionContext ctx, IAGISParamAccessor p)
            {
                _ctx        = ctx;
                _eventType  = p.GetString("event_type", "");
                _scopeKind  = p.GetString("scope_kind", "broadcast");
                _scopeTarget = p.GetString("scope_target", "");

                var payloadList = new List<(string, string)>();
                for (int i = 0; i < 3; i++)
                {
                    string k = p.GetString($"payload_key_{i}", "");
                    if (string.IsNullOrEmpty(k)) break;
                    string v = p.GetString($"payload_value_{i}", "actorstate");
                    payloadList.Add((k, v));
                }
                _payload = payloadList.ToArray();
            }

            public void Enter()
            {
                IsComplete = false;

                if (string.IsNullOrEmpty(_eventType)) { IsComplete = true; return; }

                var actorState = _ctx.Actor != null ? _ctx.Actor.GetComponent<AGISActorState>() : null;

                // Build payload entries
                var payloadEntries = new (string key, AGISValue value)[_payload.Length];
                for (int i = 0; i < _payload.Length; i++)
                {
                    var (key, valueSrc) = _payload[i];
                    AGISValue val;
                    if (valueSrc == "actorstate" && actorState != null)
                        val = actorState.Get(key);
                    else
                        val = AGISValue.FromString(valueSrc);
                    payloadEntries[i] = (key, val);
                }

                var scope = new AGISEventScope
                {
                    Kind     = ParseScopeKind(_scopeKind),
                    TargetId = _scopeTarget ?? string.Empty,
                };

                AGISActorEventBus.Instance?.Emit(_eventType, scope, payloadEntries);
                IsComplete = true;
            }

            public void Tick(float dt) { }

            public void Exit()
            {
                IsComplete = false;
            }

            private static AGISEventScopeKind ParseScopeKind(string s)
            {
                if (string.IsNullOrEmpty(s)) return AGISEventScopeKind.Broadcast;
                return s.ToLowerInvariant() switch
                {
                    "single"    => AGISEventScopeKind.Single,
                    "group"     => AGISEventScopeKind.Group,
                    _           => AGISEventScopeKind.Broadcast,
                };
            }
        }
    }
}
