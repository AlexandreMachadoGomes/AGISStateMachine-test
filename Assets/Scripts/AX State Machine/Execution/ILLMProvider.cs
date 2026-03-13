// File: ILLMProvider.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Abstraction for LLM API calls used by AGISLLMDialogueNodeType.
//          Implementations supply the actual network call; the state machine layer
//          never imports a specific LLM SDK.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace AGIS.ESM.Runtime
{
    /// <summary>
    /// Implement this interface (typically on a MonoBehaviour wrapper) to provide
    /// LLM completions to agis.llm_dialogue nodes.
    /// A null provider causes the dialogue node to log a warning and write a stub response.
    /// </summary>
    public interface ILLMProvider
    {
        /// <summary>
        /// Request a completion. Implementations call the LLM API asynchronously.
        /// Must never throw — return a stub/error string on failure.
        /// </summary>
        Task<string> CompleteAsync(LLMDialogueRequest request);
    }

    /// <summary>
    /// All data needed to build a single LLM prompt for a dialogue turn.
    /// </summary>
    public sealed class LLMDialogueRequest
    {
        /// <summary>
        /// System prompt: personality, patience context, knowledge document content, and
        /// instructions for using command hooks to modify ActorState keys.
        /// </summary>
        public string SystemPrompt;

        /// <summary>The player's input text for this turn.</summary>
        public string UserMessage;

        /// <summary>Prior turns in chronological order (role = "user" or "assistant").</summary>
        public List<(string role, string content)> History = new List<(string, string)>();

        /// <summary>
        /// ActorState keys the LLM is allowed to modify via the command prefix hook.
        /// The provider may include this list in the system prompt as a capability declaration.
        /// </summary>
        public string[] AvailableCommandKeys;
    }

    /// <summary>
    /// Optional MonoBehaviour base class for LLM provider implementations.
    /// Derive from this to expose the provider to AGISStateMachineRunner in the Inspector.
    /// </summary>
    public abstract class LLMProviderBehaviour : UnityEngine.MonoBehaviour, ILLMProvider
    {
        public abstract Task<string> CompleteAsync(LLMDialogueRequest request);
    }
}
