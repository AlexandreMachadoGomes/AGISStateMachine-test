// File: AGISKnowledgeDocumentAsset.cs
// Folder: Assets/Scripts/AX State Machine/Knowledge/
// Purpose: ScriptableObject wrapping a named knowledge document for LLM dialogue nodes.

using UnityEngine;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Knowledge
{
    [CreateAssetMenu(menuName = "AGIS/Knowledge Document", fileName = "NewKnowledgeDocument")]
    public class AGISKnowledgeDocumentAsset : ScriptableObject
    {
        [SerializeField] public AGISGuid documentId = AGISGuid.New();
        [SerializeField] public string title;
        [SerializeField, TextArea(4, 30)] public string content;
        [SerializeField] public string[] tags; // for future semantic filtering
    }
}
