// File: AGISDebugTrace.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Lightweight logging/tracing settings for runtime. Off by default.

using System;
using UnityEngine;

namespace AGIS.ESM.Runtime
{
    [Serializable]
    public sealed class AGISDebugTrace
    {
        [SerializeField] public bool enabled = false;

        [SerializeField] public bool info = false;
        [SerializeField] public bool warnings = true;
        [SerializeField] public bool errors = true;

        [SerializeField] public string prefix = "[AGIS_ESM]";

        public void Info(string msg)
        {
            if (enabled && info)
                Debug.Log($"{prefix} {msg}");
        }

        public void Warn(string msg)
        {
            if (enabled && warnings)
                Debug.LogWarning($"{prefix} {msg}");
        }

        public void Error(string msg)
        {
            if (enabled && errors)
                Debug.LogError($"{prefix} {msg}");
        }
    }
}
