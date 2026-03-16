// File: AGISTypeRegistrar.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Static registry buffer that decouples type registration from the runner.
//
// Use [RuntimeInitializeOnLoadMethod] in any assembly to push explicit type instances here
// before the first AGISStateMachineRunner.Awake() fires. The runner calls ApplyTo() at the
// top of its Awake, so registrations land before reflection-based discovery runs.
//
// This is the recommended path for IL2CPP / AOT builds where assembly scanning
// (autoRegisterTypesFromAssemblies) can silently drop stripped types.
//
// Example (in any assembly):
//
//   [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//   static void Register()
//   {
//       AGISTypeRegistrar.Add(new MyNodeType());
//       AGISTypeRegistrar.Add(new MyConditionType());
//   }

using System.Collections.Generic;

namespace AGIS.ESM.Runtime
{
    public static class AGISTypeRegistrar
    {
        private static readonly List<IAGISNodeType>      _nodes      = new List<IAGISNodeType>();
        private static readonly List<IAGISConditionType> _conditions = new List<IAGISConditionType>();

        /// <summary>Register a node type for all future runner instances.</summary>
        public static void Add(IAGISNodeType type)
        {
            if (type != null) _nodes.Add(type);
        }

        /// <summary>Register a condition type for all future runner instances.</summary>
        public static void Add(IAGISConditionType type)
        {
            if (type != null) _conditions.Add(type);
        }

        /// <summary>
        /// Called by AGISStateMachineRunner.Awake() after creating its registries.
        /// Pushes all buffered types into the runner's own registries.
        /// </summary>
        internal static void ApplyTo(AGISNodeTypeRegistry nodes, AGISConditionTypeRegistry conditions)
        {
            for (int i = 0; i < _nodes.Count; i++)
                nodes.Register(_nodes[i]);

            for (int i = 0; i < _conditions.Count; i++)
                conditions.Register(_conditions[i]);
        }
    }
}
