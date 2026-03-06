// File: IAGISNodeComponentRequirements.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Implement on an IAGISNodeType to declare which MonoBehaviour component types
//          must be present on the actor, given the node's resolved instance params.
//          AGISStateMachineRunner scans all slot graphs at startup and calls
//          AGISActorComponentFixer.EnsureComponents for any types that are missing.
//
// The param accessor passed to GetRequiredComponents is pre-resolved (instance overrides
// merged with schema defaults), so the method can safely read any param by key.
//
// Return an empty list when the node's current params do not activate the dependency —
// this is how param-gated requirements work (e.g. only require NPCDetectionCone when
// use_detection_memory = true).
//
// Example:
//   public IReadOnlyList<Type> GetRequiredComponents(IAGISParamAccessor p)
//   {
//       return p.GetBool("use_detection_memory", false)
//           ? new[] { typeof(NPCDetectionCone) }
//           : Array.Empty<Type>();
//   }

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public interface IAGISNodeComponentRequirements
    {
        /// <summary>
        /// Returns the MonoBehaviour component types that must be present on the actor
        /// for this node to function correctly with the given resolved params.
        /// Return an empty (non-null) list when no components are required.
        /// </summary>
        IReadOnlyList<Type> GetRequiredComponents(IAGISParamAccessor resolvedParams);
    }
}
