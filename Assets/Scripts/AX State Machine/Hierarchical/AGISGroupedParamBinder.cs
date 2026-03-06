// File: AGISGroupedParamBinder.cs
// Folder: Assets/Scripts/AX State Machine/Hierarchical/
// Purpose: Apply grouped exposedOverrides (public knobs) onto a cloned internal graph via stable targets.
// Policy: apply on Enter + on Change (change detection handled by Grouped runtime).

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC;
using AGIS.ESM.UGC.Params;

namespace AGIS.ESM.Runtime
{
    public sealed class AGISGroupedParamBinder
    {
        private readonly AGISParamTargetApplier _applier;
        private readonly AGISDebugTrace _trace;

        public AGISGroupedParamBinder(AGISParamTargetApplier applier, AGISDebugTrace trace = null)
        {
            _applier = applier ?? throw new ArgumentNullException(nameof(applier));
            _trace = trace;
        }

        public void ApplyBindings(AGISGroupedStateAsset groupAsset, AGISNodeInstanceDef groupedNodeInstanceDef, AGISStateMachineGraph internalGraphClone)
        {
            if (groupAsset == null || groupedNodeInstanceDef == null || internalGraphClone == null)
                return;

            var exposedById = new Dictionary<AGISGuid, AGISExposedParamDef>();
            if (groupAsset.exposedParams != null)
            {
                for (int i = 0; i < groupAsset.exposedParams.Count; i++)
                {
                    var ep = groupAsset.exposedParams[i];
                    if (ep != null && ep.exposedId.IsValid)
                        exposedById[ep.exposedId] = ep;
                }
            }

            var valuesByExposedId = new Dictionary<AGISGuid, AGISValue>();
            foreach (var kv in exposedById)
            {
                var exposedId = kv.Key;
                var ep = kv.Value;

                var effective = GetEffectiveExposedValue(ep, groupedNodeInstanceDef.exposedOverrides);
                valuesByExposedId[exposedId] = effective;
            }

            if (groupAsset.bindings == null)
                return;

            for (int i = 0; i < groupAsset.bindings.Count; i++)
            {
                var binding = groupAsset.bindings[i];
                if (binding == null) continue;

                if (!binding.exposedId.IsValid || !valuesByExposedId.TryGetValue(binding.exposedId, out var value))
                    continue;

                if (binding.targets == null || binding.targets.Count == 0)
                    continue;

                for (int t = 0; t < binding.targets.Count; t++)
                {
                    var target = binding.targets[t];
                    if (target == null) continue;

                    _applier.ApplyToTarget(internalGraphClone, target, value);
                }
            }
        }

        private AGISValue GetEffectiveExposedValue(AGISExposedParamDef ep, AGISParamTable overrides)
        {
            if (ep == null)
                return default;

            var def = ep.defaultValue;
            if (def.Type != ep.type)
                def = GetTypeDefault(ep.type);

            if (overrides != null && !string.IsNullOrEmpty(ep.publicKey) && overrides.TryGet(ep.publicKey, out var ov))
            {
                if (ov.Type == ep.type)
                    return ov;

                _trace?.Warn($"Exposed override type mismatch for '{ep.publicKey}': expected {ep.type}, got {ov.Type}. Using default.");
            }

            return def;
        }

        private static AGISValue GetTypeDefault(AGISParamType type)
        {
            switch (type)
            {
                case AGISParamType.Bool: return AGISValue.FromBool(false);
                case AGISParamType.Int: return AGISValue.FromInt(0);
                case AGISParamType.Float: return AGISValue.FromFloat(0f);
                case AGISParamType.String: return AGISValue.FromString("");
                case AGISParamType.Vector2: return AGISValue.FromVector2(UnityEngine.Vector2.zero);
                case AGISParamType.Vector3: return AGISValue.FromVector3(UnityEngine.Vector3.zero);
                case AGISParamType.Guid: return AGISValue.FromGuid(AGISGuid.Empty);
                default: return default;
            }
        }
    }
}
