// File: AGISKnowledgeDocumentRegistry.cs
// Folder: Assets/Scripts/AX State Machine/Knowledge/
// Purpose: Registry for knowledge document assets, keyed by AGISGuid.
//          Runtime registration pattern mirrors AGISNodeTypeRegistry.

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Knowledge
{
    public sealed class AGISKnowledgeDocumentRegistry
    {
        private readonly Dictionary<AGISGuid, AGISKnowledgeDocumentAsset> _docs
            = new Dictionary<AGISGuid, AGISKnowledgeDocumentAsset>();

        public IEnumerable<AGISKnowledgeDocumentAsset> AllDocuments => _docs.Values;

        public void Register(AGISKnowledgeDocumentAsset doc)
        {
            if (doc == null) return;
            if (!doc.documentId.IsValid) return;
            _docs[doc.documentId] = doc;
        }

        public bool TryGet(AGISGuid id, out AGISKnowledgeDocumentAsset doc)
        {
            if (!id.IsValid) { doc = null; return false; }
            return _docs.TryGetValue(id, out doc);
        }

        public void Unregister(AGISGuid id)
        {
            if (id.IsValid) _docs.Remove(id);
        }

        public void Clear() => _docs.Clear();

        public int Count => _docs.Count;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: scans AssetDatabase for all AGISKnowledgeDocumentAsset instances and registers them.
        /// Returns the number of documents registered.
        /// </summary>
        public int RegisterAllFromProject()
        {
            int count = 0;
            var guids = UnityEditor.AssetDatabase.FindAssets("t:AGISKnowledgeDocumentAsset");
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var doc = UnityEditor.AssetDatabase.LoadAssetAtPath<AGISKnowledgeDocumentAsset>(path);
                if (doc != null)
                {
                    Register(doc);
                    count++;
                }
            }
            return count;
        }
#endif
    }
}
