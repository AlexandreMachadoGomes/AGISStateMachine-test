// File: AGISBindingChangeDetector.cs
// Folder: Assets/Scripts/AX State Machine/Hierarchical/
// Purpose: Detect changes to an exposedOverrides ParamTable so Grouped binder can apply "on change" without per-tick rebinding.
// Canvas alignment: binder policy is "on enter + on change".

using System;
using System.Collections.Generic;
using AGIS.ESM.UGC.Params;
using AGIS.ESM.UGC;

namespace AGIS.ESM.Runtime
{
    public static class AGISBindingChangeDetector
    {
        // FNV-1a 64-bit
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong FingerprintParamTable(AGISParamTable table)
        {
            ulong h = Offset;

            if (table == null || table.values == null)
                return HashInt(h, 0);

            // Normalize (keep last key) without mutating
            var last = new Dictionary<string, AGISValue>(StringComparer.Ordinal);
            for (int i = 0; i < table.values.Count; i++)
            {
                var pv = table.values[i];
                if (pv == null || string.IsNullOrEmpty(pv.key))
                    continue;
                last[pv.key] = pv.value;
            }

            var keys = new List<string>(last.Keys);
            keys.Sort(StringComparer.Ordinal);

            h = HashInt(h, keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                h = HashString(h, key);
                h = HashValue(h, last[key]);
            }

            return h;
        }

        private static ulong HashValue(ulong h, in AGISValue v)
        {
            h = HashInt(h, (int)v.Type);
            switch (v.Type)
            {
                case AGISParamType.Bool: return HashByte(h, v.AsBool() ? (byte)1 : (byte)0);
                case AGISParamType.Int: return HashInt(h, v.AsInt());
                case AGISParamType.Float: return HashInt(h, BitConverter.SingleToInt32Bits(v.AsFloat()));
                case AGISParamType.String: return HashString(h, v.AsString());
                case AGISParamType.Vector2:
                    {
                        var x = v.AsVector2();
                        h = HashInt(h, BitConverter.SingleToInt32Bits(x.x));
                        h = HashInt(h, BitConverter.SingleToInt32Bits(x.y));
                        return h;
                    }
                case AGISParamType.Vector3:
                    {
                        var x = v.AsVector3();
                        h = HashInt(h, BitConverter.SingleToInt32Bits(x.x));
                        h = HashInt(h, BitConverter.SingleToInt32Bits(x.y));
                        h = HashInt(h, BitConverter.SingleToInt32Bits(x.z));
                        return h;
                    }
                case AGISParamType.Guid:
                    return HashString(h, v.AsGuid(AGISGuid.Empty).ToString());
                default:
                    return h;
            }
        }

        private static ulong HashString(ulong h, string s)
        {
            if (s == null) s = "";
            h = HashInt(h, s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                unchecked
                {
                    h ^= (byte)(s[i] & 0xFF);
                    h *= Prime;
                    h ^= (byte)((s[i] >> 8) & 0xFF);
                    h *= Prime;
                }
            }
            return h;
        }

        private static ulong HashInt(ulong h, int x)
        {
            unchecked
            {
                h ^= (byte)x; h *= Prime;
                h ^= (byte)(x >> 8); h *= Prime;
                h ^= (byte)(x >> 16); h *= Prime;
                h ^= (byte)(x >> 24); h *= Prime;
                return h;
            }
        }

        private static ulong HashByte(ulong h, byte b)
        {
            unchecked
            {
                h ^= b;
                h *= Prime;
                return h;
            }
        }
    }
}
