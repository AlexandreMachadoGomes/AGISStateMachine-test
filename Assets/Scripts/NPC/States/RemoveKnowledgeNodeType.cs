// File: RemoveKnowledgeNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: Transient node that removes knowledge document GUIDs from AGISKnowledgeState.
//          TypeId: "agis.remove_knowledge"

using System;
using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.Knowledge;

namespace AGIS.NPC.States
{
    public sealed class RemoveKnowledgeNodeType : IAGISNodeType, IAGISNodeComponentRequirements
    {
        public string TypeId      => "agis.remove_knowledge";
        public string DisplayName => "Remove Knowledge";
        public AGISNodeKind Kind  => AGISNodeKind.Normal;

        public AGISParamSchema Schema { get; } = BuildSchema();

        private static AGISParamSchema BuildSchema()
        {
            var s = new AGISParamSchema();
            for (int i = 0; i < 8; i++)
                s.AddOrReplace(new AGISParamSpec { key = $"doc_guid_{i}", type = AGISParamType.String, defaultValue = AGISValue.FromString(""), displayName = $"Doc GUID {i}" });
            return s;
        }

        public IReadOnlyList<Type> GetRequiredComponents(IAGISParamAccessor resolvedParams)
            => new[] { typeof(AGISKnowledgeState) };

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
            => new Runtime(args.Ctx, args.Params);

        private sealed class Runtime : IAGISNodeRuntime, IAGISNodeSignal
        {
            public bool IsComplete { get; private set; }

            private readonly AGISKnowledgeState _knowledgeState;
            private readonly AGISGuid[] _docGuids;

            public Runtime(AGISExecutionContext ctx, IAGISParamAccessor p)
            {
                _knowledgeState = ctx.Actor != null ? ctx.Actor.GetComponent<AGISKnowledgeState>() : null;

                var guids = new List<AGISGuid>();
                for (int i = 0; i < 8; i++)
                {
                    string guidStr = p.GetString($"doc_guid_{i}", "");
                    if (string.IsNullOrEmpty(guidStr)) break;
                    var g = new AGISGuid(guidStr);
                    if (g.IsValid) guids.Add(g);
                }
                _docGuids = guids.ToArray();
            }

            public void Enter()
            {
                IsComplete = false;
                if (_knowledgeState != null)
                    foreach (var id in _docGuids)
                        _knowledgeState.RemoveDocument(id);
                IsComplete = true;
            }

            public void Tick(float dt) { }

            public void Exit() => IsComplete = false;
        }
    }
}
