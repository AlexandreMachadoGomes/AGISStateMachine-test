// File: IAGISPersistentNodeType.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Declares which AGISActorState keys a type needs and their default values.
//          Implement this on either an IAGISNodeType (graph node) or a MonoBehaviour
//          component — the runner discovers both at startup:
//            • Graph scan   — node types that also implement this interface
//            • Component scan — MonoBehaviours on the actor that implement this interface
//          AGISStateMachineRunner calls AGISActorState.EnsureKey for every declared param,
//          adding the default only if the key is not already present.
//
// This means:
//   - First run:  all declared keys are added with defaults.
//   - Subsequent runs / re-entries: existing values are preserved (resume behaviour).
//   - Explicit reset: runtimes write known values (e.g. NPCResetRouteNodeType sets 0s).

using System.Collections.Generic;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public interface IAGISPersistentNodeType
    {
        /// <summary>
        /// Keys this type reads or writes in AGISActorState, with their default values.
        /// The runner calls AGISActorState.EnsureKey for each entry at startup.
        /// </summary>
        IReadOnlyList<AGISParamSpec> PersistentParams { get; }
    }
}
