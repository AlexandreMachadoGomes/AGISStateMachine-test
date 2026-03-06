// File: AGISActorComponentFixer.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Ensures required MonoBehaviour components are present on an actor GameObject.
//          Called by AGISStateMachineRunner during startup when IAGISNodeComponentRequirements
//          reports missing dependencies.
//
// Current behaviour: missing components are added immediately (no confirmation step).
// Future: replace the body of AddComponent to show a confirmation dialog (e.g. via
//         EditorUtility.DisplayDialog in an editor hook) before adding in edit mode.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGIS.ESM.Runtime
{
    public static class AGISActorComponentFixer
    {
        /// <summary>
        /// Checks each type in <paramref name="requiredTypes"/> against <paramref name="actor"/>.
        /// For any that are missing, calls <see cref="AddComponent"/> to resolve the dependency.
        /// Logs a message for every component added.
        /// </summary>
        public static void EnsureComponents(GameObject actor, IReadOnlyList<Type> requiredTypes)
        {
            if (actor == null || requiredTypes == null) return;

            for (int i = 0; i < requiredTypes.Count; i++)
            {
                var type = requiredTypes[i];
                if (type == null || !typeof(Component).IsAssignableFrom(type)) continue;
                if (actor.GetComponent(type) != null) continue;

                AddComponent(actor, type);
            }
        }

        // ── Extension point ───────────────────────────────────────────────────────────
        // Replace or wrap this method to add confirmation (e.g. EditorUtility.DisplayDialog
        // in editor, or a runtime approval callback). Currently auto-adds unconditionally.

        private static void AddComponent(GameObject actor, Type type)
        {
            actor.AddComponent(type);
            Debug.Log($"[AGIS] Auto-added required component '{type.Name}' to '{actor.name}'.");
        }
    }
}
