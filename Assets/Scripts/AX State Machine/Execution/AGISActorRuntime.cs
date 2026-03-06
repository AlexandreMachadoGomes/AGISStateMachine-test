// File: AGISActorRuntime.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Per-actor runtime host for ESM state machines (owns Blackboard + ExecutionContext).

using UnityEngine;

namespace AGIS.ESM.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AGISActorRuntime : MonoBehaviour
    {
        [Header("Blackboard")]
        [SerializeField] private bool createBlackboardOnAwake = true;

        private AGISBlackboardRuntime _blackboard;
        private AGISExecutionContext _ctx;

        public IAGISBlackboard Blackboard => _blackboard;

        public AGISExecutionContext Context
        {
            get
            {
                EnsureInitialized();
                return _ctx;
            }
        }

        private void Awake()
        {
            if (createBlackboardOnAwake)
                EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_blackboard == null)
                _blackboard = new AGISBlackboardRuntime();

            if (_ctx == null || _ctx.Actor != gameObject || _ctx.Blackboard != _blackboard)
                _ctx = new AGISExecutionContext(gameObject, _blackboard);
        }
    }
}
