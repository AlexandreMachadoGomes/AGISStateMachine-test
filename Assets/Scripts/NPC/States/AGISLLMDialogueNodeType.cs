// File: AGISLLMDialogueNodeType.cs
// Folder: Assets/Scripts/NPC/States/
// Purpose: Node type that drives open-ended NPC conversation via a language model.
//          TypeId: "agis.llm_dialogue"
//
// Schema params control personality, patience, mood, knowledge docs, etc.
// Persistent params in AGISActorState hold per-actor LLM state between sessions.
// LLM responses may contain command hooks that write back to ActorState.
// Requires AGISKnowledgeState component (via IAGISNodeComponentRequirements).

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using AGIS.ESM.Runtime;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.Knowledge;

namespace AGIS.NPC.States
{
    public sealed class AGISLLMDialogueNodeType : IAGISNodeType, IAGISPersistentNodeType, IAGISNodeComponentRequirements
    {
        public string TypeId      => "agis.llm_dialogue";
        public string DisplayName => "LLM Dialogue";
        public AGISNodeKind Kind  => AGISNodeKind.Normal;

        public AGISParamSchema Schema { get; } = BuildSchema();

        public IReadOnlyList<AGISParamSpec> PersistentParams { get; } = new[]
        {
            new AGISParamSpec { key = "npc.llm.personality", type = AGISParamType.String, defaultValue = AGISValue.FromString("") },
            new AGISParamSpec { key = "npc.llm.patience",    type = AGISParamType.Float,  defaultValue = AGISValue.FromFloat(1f)   },
            new AGISParamSpec { key = "npc.llm.mood",        type = AGISParamType.String, defaultValue = AGISValue.FromString("neutral") },
            new AGISParamSpec { key = "npc.llm.input",       type = AGISParamType.String, defaultValue = AGISValue.FromString("") },
            new AGISParamSpec { key = "npc.llm.response",    type = AGISParamType.String, defaultValue = AGISValue.FromString("") },
        };

        public IReadOnlyList<Type> GetRequiredComponents(IAGISParamAccessor resolvedParams)
            => new[] { typeof(AGISKnowledgeState) };

        private static AGISParamSchema BuildSchema()
        {
            var s = new AGISParamSchema();
            s.AddOrReplace(new AGISParamSpec { key = "input_key",       type = AGISParamType.String, defaultValue = AGISValue.FromString("npc.llm.input"),    displayName = "Input Key",       tooltip = "ActorState key the game UI writes player text into." });
            s.AddOrReplace(new AGISParamSpec { key = "response_key",    type = AGISParamType.String, defaultValue = AGISValue.FromString("npc.llm.response"), displayName = "Response Key",    tooltip = "Blackboard key where the LLM response is written." });
            s.AddOrReplace(new AGISParamSpec { key = "voice_id",        type = AGISParamType.String, defaultValue = AGISValue.FromString(""),                displayName = "Voice ID",         tooltip = "TTS voice model ID. Empty = text only." });
            s.AddOrReplace(new AGISParamSpec { key = "include_history", type = AGISParamType.Bool,   defaultValue = AGISValue.FromBool(true),                displayName = "Include History",  tooltip = "Include prior turns in each prompt." });
            s.AddOrReplace(new AGISParamSpec { key = "command_prefix",  type = AGISParamType.String, defaultValue = AGISValue.FromString("cmd.:"),           displayName = "Command Prefix",   tooltip = "Prefix for hook commands embedded in the LLM response." });
            s.AddOrReplace(new AGISParamSpec { key = "writable_keys",   type = AGISParamType.String, defaultValue = AGISValue.FromString("npc.llm.personality,npc.llm.patience,npc.llm.mood"), displayName = "Writable Keys", tooltip = "Comma-separated ActorState keys LLM may modify via commands." });
            for (int i = 0; i < 8; i++)
                s.AddOrReplace(new AGISParamSpec { key = $"knowledge_doc_{i}", type = AGISParamType.String, defaultValue = AGISValue.FromString(""), displayName = $"Knowledge Doc {i}", tooltip = "GUID of a baseline knowledge document (validated by compiler)." });
            return s;
        }

        public IAGISNodeRuntime CreateRuntime(in AGISNodeRuntimeCreateArgs args)
            => new Runtime(args.Ctx, args.Params);

        // ─────────────────────────────────────────────────────────────────────────────
        private sealed class Runtime : IAGISNodeRuntime
        {
            private readonly AGISExecutionContext _ctx;
            private readonly AGISActorState _actorState;
            private readonly AGISKnowledgeState _knowledgeState;
            private readonly ILLMProvider _llmProvider;

            private readonly string _inputKey;
            private readonly string _responseKey;
            private readonly bool _includeHistory;
            private readonly string _commandPrefix;
            private readonly string[] _writableKeys;
            private readonly AGISGuid[] _baselineDocGuids;

            private readonly List<(string role, string content)> _history = new List<(string, string)>();
            private string _lastSeenInput;
            private Task<string> _pendingRequest;

            public Runtime(AGISExecutionContext ctx, IAGISParamAccessor p)
            {
                _ctx             = ctx;
                _actorState      = ctx.Actor != null ? ctx.Actor.GetComponent<AGISActorState>() : null;
                _knowledgeState  = ctx.Actor != null ? ctx.Actor.GetComponent<AGISKnowledgeState>() : null;
                _llmProvider     = ctx.Actor != null ? ctx.Actor.GetComponent<ILLMProvider>() : null;

                _inputKey        = p.GetString("input_key",       "npc.llm.input");
                _responseKey     = p.GetString("response_key",    "npc.llm.response");
                _includeHistory  = p.GetBool  ("include_history", true);
                _commandPrefix   = p.GetString("command_prefix",  "cmd.:");

                string writableRaw = p.GetString("writable_keys", "npc.llm.personality,npc.llm.patience,npc.llm.mood");
                _writableKeys = writableRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < _writableKeys.Length; i++)
                    _writableKeys[i] = _writableKeys[i].Trim();

                var guids = new List<AGISGuid>();
                for (int i = 0; i < 8; i++)
                {
                    string guidStr = p.GetString($"knowledge_doc_{i}", "");
                    if (string.IsNullOrEmpty(guidStr)) break;
                    var g = new AGISGuid(guidStr);
                    if (g.IsValid) guids.Add(g);
                }
                _baselineDocGuids = guids.ToArray();
            }

            public void Enter()
            {
                if (!_includeHistory)
                    _history.Clear();

                // Remember input at entry so we don't react to stale values.
                _lastSeenInput = _actorState?.GetString(_inputKey, "") ?? string.Empty;
                _pendingRequest = null;
            }

            public void Tick(float dt)
            {
                // Check if a pending request completed.
                if (_pendingRequest != null && _pendingRequest.IsCompleted)
                {
                    string response = _pendingRequest.Result ?? string.Empty;
                    _pendingRequest = null;

                    // Parse command hooks from response.
                    string cleanedResponse = ParseCommandHooks(response);

                    // Write cleaned response to blackboard.
                    _ctx.Blackboard?.Set(_responseKey, cleanedResponse);

                    // Append exchange to history.
                    _history.Add(("assistant", cleanedResponse));

                    // Update _lastSeenInput so same input doesn't re-fire.
                    _lastSeenInput = _actorState?.GetString(_inputKey, "") ?? string.Empty;
                    return;
                }

                // Don't stack requests.
                if (_pendingRequest != null) return;

                // Check for new player input.
                string currentInput = _actorState?.GetString(_inputKey, "") ?? string.Empty;
                if (currentInput == _lastSeenInput) return;
                if (string.IsNullOrEmpty(currentInput)) return;

                // Append player input to history.
                _history.Add(("user", currentInput));

                // Build and fire request.
                var request = BuildRequest(currentInput);

                if (_llmProvider == null)
                {
                    // Stub response when no provider is wired.
                    Debug.LogWarning($"[AGISLLMDialogueNodeType] No ILLMProvider found on {_ctx.Actor?.name}. Using stub response.");
                    _ctx.Blackboard?.Set(_responseKey, "[LLM provider not configured]");
                    _history.Add(("assistant", "[LLM provider not configured]"));
                    _lastSeenInput = currentInput;
                    return;
                }

                _pendingRequest = _llmProvider.CompleteAsync(request);
            }

            public void Exit()
            {
                if (!_includeHistory)
                    _history.Clear();

                // Cancel pending request (Task cancellation is best-effort; we just null it).
                _pendingRequest = null;

                // Remove response key from blackboard.
                _ctx.Blackboard?.Remove(_responseKey);
            }

            // ── Helpers ──────────────────────────────────────────────────────────────

            private LLMDialogueRequest BuildRequest(string userMessage)
            {
                string personality = _actorState?.GetString("npc.llm.personality", "") ?? "";
                float patience     = _actorState?.GetFloat ("npc.llm.patience", 1f) ?? 1f;
                string mood        = _actorState?.GetString("npc.llm.mood", "neutral") ?? "neutral";

                // Collect knowledge document contents.
                var sb = new StringBuilder();
                sb.AppendLine($"Personality: {personality}");
                sb.AppendLine($"Patience: {patience:F2}");
                sb.AppendLine($"Mood: {mood}");

                // Baseline docs (from schema params).
                // Note: full resolution from AGISKnowledgeDocumentRegistry would require
                // injecting the registry — for now we describe the GUIDs; integrators
                // should subclass or configure the provider with doc content.
                if (_baselineDocGuids.Length > 0 || (_knowledgeState?.RuntimeDocuments.Count > 0))
                {
                    sb.AppendLine("\n--- Knowledge Documents ---");
                    // Runtime docs from AGISKnowledgeState.
                    if (_knowledgeState != null)
                    {
                        foreach (var id in _knowledgeState.RuntimeDocuments)
                            sb.AppendLine($"[Doc {id.Value}]");
                    }
                }

                if (_writableKeys.Length > 0)
                {
                    sb.AppendLine($"\nYou may modify actor state using: {_commandPrefix} <key>='<value>'");
                    sb.AppendLine($"Allowed keys: {string.Join(", ", _writableKeys)}");
                }

                return new LLMDialogueRequest
                {
                    SystemPrompt       = sb.ToString(),
                    UserMessage        = userMessage,
                    History            = new List<(string, string)>(_history),
                    AvailableCommandKeys = _writableKeys,
                };
            }

            /// <summary>
            /// Scans the response for command hooks ({prefix} {key}='{value}'), applies them
            /// to ActorState (if key is in writable_keys), and returns the cleaned text.
            /// </summary>
            private string ParseCommandHooks(string response)
            {
                if (string.IsNullOrEmpty(response) || _actorState == null || _writableKeys.Length == 0)
                    return response;

                var result = new StringBuilder(response.Length);
                int pos = 0;

                while (pos < response.Length)
                {
                    int cmdIdx = response.IndexOf(_commandPrefix, pos, StringComparison.Ordinal);
                    if (cmdIdx < 0)
                    {
                        result.Append(response, pos, response.Length - pos);
                        break;
                    }

                    // Append text before the command.
                    result.Append(response, pos, cmdIdx - pos);

                    // Parse key='value' after the prefix.
                    int afterPrefix = cmdIdx + _commandPrefix.Length;
                    int eqIdx = response.IndexOf('=', afterPrefix);
                    if (eqIdx < 0) { pos = cmdIdx + 1; result.Append(_commandPrefix); continue; }

                    string key = response.Substring(afterPrefix, eqIdx - afterPrefix).Trim();

                    // Find the quoted value.
                    int quoteStart = response.IndexOf('\'', eqIdx + 1);
                    if (quoteStart < 0) { pos = cmdIdx + 1; result.Append(_commandPrefix); continue; }
                    int quoteEnd = response.IndexOf('\'', quoteStart + 1);
                    if (quoteEnd < 0) { pos = cmdIdx + 1; result.Append(_commandPrefix); continue; }

                    string value = response.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                    pos = quoteEnd + 1;

                    // Apply if key is writable.
                    bool writable = false;
                    foreach (var wk in _writableKeys)
                        if (wk == key) { writable = true; break; }

                    if (writable)
                    {
                        // Infer type from current ActorState value.
                        var existing = _actorState.Get(key);
                        AGISValue newVal;
                        switch (existing.Type)
                        {
                            case AGISParamType.Bool:
                                newVal = AGISValue.FromBool(value == "true" || value == "1");
                                break;
                            case AGISParamType.Int:
                                newVal = int.TryParse(value, out int iv) ? AGISValue.FromInt(iv) : existing;
                                break;
                            case AGISParamType.Float:
                                newVal = float.TryParse(value, out float fv) ? AGISValue.FromFloat(fv) : existing;
                                break;
                            default:
                                newVal = AGISValue.FromString(value);
                                break;
                        }
                        _actorState.Set(key, newVal);
                    }
                }

                return result.ToString().Trim();
            }
        }
    }
}
