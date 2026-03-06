// File: AGISGraphValidationReport.cs
// Folder: Assets/Scripts/AX State Machine/Compilation/
// Purpose: Structured validation report for graphs/assets (used by compiler + editor feedback).

using System.Collections.Generic;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Runtime
{
    public enum AGISValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    public readonly struct AGISValidationIssue
    {
        public readonly AGISValidationSeverity Severity;
        public readonly string Code;
        public readonly string Message;
        public readonly string Path;

        public readonly AGISGuid NodeId;
        public readonly AGISGuid EdgeId;
        public readonly AGISGuid GroupAssetId;

        public AGISValidationIssue(
            AGISValidationSeverity severity,
            string code,
            string message,
            string path = null,
            AGISGuid nodeId = default,
            AGISGuid edgeId = default,
            AGISGuid groupAssetId = default)
        {
            Severity = severity;
            Code = code;
            Message = message;
            Path = path;
            NodeId = nodeId;
            EdgeId = edgeId;
            GroupAssetId = groupAssetId;
        }

        public override string ToString()
        {
            var loc = "";
            if (NodeId.IsValid) loc += $" node:{NodeId}";
            if (EdgeId.IsValid) loc += $" edge:{EdgeId}";
            if (GroupAssetId.IsValid) loc += $" group:{GroupAssetId}";
            if (!string.IsNullOrEmpty(Path)) loc += $" path:{Path}";
            return $"{Severity} [{Code}] {Message}{loc}";
        }
    }

    public sealed class AGISGraphValidationReport
    {
        private readonly List<AGISValidationIssue> _issues = new List<AGISValidationIssue>();

        public IReadOnlyList<AGISValidationIssue> Issues => _issues;

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < _issues.Count; i++)
                    if (_issues[i].Severity == AGISValidationSeverity.Error)
                        return true;
                return false;
            }
        }

        public void Add(AGISValidationIssue issue) => _issues.Add(issue);

        public void Info(string code, string message, string path = null, AGISGuid nodeId = default, AGISGuid edgeId = default, AGISGuid groupAssetId = default)
            => Add(new AGISValidationIssue(AGISValidationSeverity.Info, code, message, path, nodeId, edgeId, groupAssetId));

        public void Warn(string code, string message, string path = null, AGISGuid nodeId = default, AGISGuid edgeId = default, AGISGuid groupAssetId = default)
            => Add(new AGISValidationIssue(AGISValidationSeverity.Warning, code, message, path, nodeId, edgeId, groupAssetId));

        public void Error(string code, string message, string path = null, AGISGuid nodeId = default, AGISGuid edgeId = default, AGISGuid groupAssetId = default)
            => Add(new AGISValidationIssue(AGISValidationSeverity.Error, code, message, path, nodeId, edgeId, groupAssetId));
    }
}
