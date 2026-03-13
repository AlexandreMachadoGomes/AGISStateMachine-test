// File: AGISKnowledgeState.cs
// Folder: Assets/Scripts/AX State Machine/Knowledge/
// Purpose: MonoBehaviour holding the per-actor runtime list of knowledge document GUIDs.
//          Baseline documents are defined in node schema params; this component tracks
//          documents added/removed at runtime by AddKnowledgeNodeType / RemoveKnowledgeNodeType.

using System.Collections.Generic;
using UnityEngine;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Knowledge
{
    public sealed class AGISKnowledgeState : MonoBehaviour
    {
        private readonly List<AGISGuid> _runtimeDocuments = new List<AGISGuid>();

        /// <summary>Documents added at runtime (dynamic additions only; baseline is in node schema params).</summary>
        public IReadOnlyList<AGISGuid> RuntimeDocuments => _runtimeDocuments;

        public void AddDocument(AGISGuid id)
        {
            if (!id.IsValid) return;
            if (!_runtimeDocuments.Contains(id))
                _runtimeDocuments.Add(id);
        }

        public void RemoveDocument(AGISGuid id)
        {
            if (!id.IsValid) return;
            _runtimeDocuments.Remove(id);
        }

        public void ClearRuntimeDocuments() => _runtimeDocuments.Clear();
    }
}
