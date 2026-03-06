// File: AGISRuntimeGraphCache.cs
// Folder: Assets/Scripts/AX State Machine/Execution/
// Purpose: Runtime graph cache keyed by deterministic content fingerprint.
// Canvas alignment: users should not need to manually manage "version" or revision numbers while authoring.
// Notes: Graph.version remains a FORMAT version for migration; caching uses the content fingerprint.

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISRuntimeGraphCache
    {
        private readonly AGISGraphCompiler _compiler;

        private readonly Dictionary<AGISGraphCacheKey, AGISRuntimeGraph> _cache = new Dictionary<AGISGraphCacheKey, AGISRuntimeGraph>();

        public AGISRuntimeGraphCache(AGISGraphCompiler compiler)
        {
            _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        }

        public void Clear() => _cache.Clear();

        public AGISRuntimeGraph GetOrCompile(AGISStateMachineGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            // Fingerprint = deterministic content hash. No manual version bumps required.
            var fp = AGISGraphFingerprint.Compute(graph);
            var key = new AGISGraphCacheKey(graph.graphId, graph.version, fp);

            if (_cache.TryGetValue(key, out var compiled) && compiled != null)
                return compiled;

            compiled = _compiler.Compile(graph);
            _cache[key] = compiled;
            return compiled;
        }

        private readonly struct AGISGraphCacheKey : IEquatable<AGISGraphCacheKey>
        {
            private readonly AGISGuid _graphId;
            private readonly int _formatVersion;
            private readonly ulong _fingerprint;

            public AGISGraphCacheKey(AGISGuid graphId, int formatVersion, ulong fingerprint)
            {
                _graphId = graphId;
                _formatVersion = formatVersion;
                _fingerprint = fingerprint;
            }

            public bool Equals(AGISGraphCacheKey other)
            {
                return _graphId.Equals(other._graphId)
                    && _formatVersion == other._formatVersion
                    && _fingerprint == other._fingerprint;
            }

            public override bool Equals(object obj) => obj is AGISGraphCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = (h * 31) + _graphId.GetHashCode();
                    h = (h * 31) + _formatVersion;
                    // fold 64-bit fingerprint into 32-bit
                    h = (h * 31) + (int)_fingerprint;
                    h = (h * 31) + (int)(_fingerprint >> 32);
                    return h;
                }
            }
        }
    }
}
