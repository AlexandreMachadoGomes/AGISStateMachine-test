// File: AGISTransitionPolicyRuntime.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Minimal transition policy runtime (cooldown + optional interrupt gating).

using System.Collections.Generic;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Runtime
{
    /// <summary>
    /// Optional interface for node runtimes that can signal interruptibility at runtime.
    /// If not implemented, nodes are assumed interruptible.
    /// </summary>
    public interface IAGISInterruptibility
    {
        bool IsInterruptible { get; }
    }

    public sealed class AGISTransitionPolicyRuntime
    {
        private readonly Dictionary<AGISGuid, float> _cooldownUntil = new Dictionary<AGISGuid, float>();

        public bool CanFire(in AGISCompiledEdge edge, float now, IAGISNodeRuntime currentNodeRuntime)
        {
            // Interrupt gating (opt-in)
            if (edge.Policy != null && edge.Policy.interruptible == false)
            {
                if (currentNodeRuntime is IAGISInterruptibility i && i.IsInterruptible == false)
                    return false;
            }

            // Cooldown gating
            if (edge.Policy != null && edge.Policy.cooldownSeconds > 0f)
            {
                if (_cooldownUntil.TryGetValue(edge.EdgeId, out var until) && now < until)
                    return false;
            }

            return true;
        }

        public void RecordFired(in AGISCompiledEdge edge, float now)
        {
            if (edge.Policy != null && edge.Policy.cooldownSeconds > 0f)
                _cooldownUntil[edge.EdgeId] = now + edge.Policy.cooldownSeconds;
        }

        public void Clear() => _cooldownUntil.Clear();
    }
}
