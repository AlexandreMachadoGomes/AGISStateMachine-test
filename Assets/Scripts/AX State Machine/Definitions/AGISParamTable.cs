// File: AGISParamTable.cs
// Folder: Assets/Scripts/AX State Machine/Definitions/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGIS.ESM.UGC.Params
{
    /// <summary>
    /// Param type enum used by schemas + exposed params.
    /// Keep stable over time for migrations.
    /// </summary>
    public enum AGISParamType
    {
        Bool = 0,
        Int = 1,
        Float = 2,
        String = 3,
        Vector2 = 4,
        Vector3 = 5,
        Guid = 6,
    }

    /// <summary>
    /// Typed union used by ParamTables (UGC instance overrides).
    /// This is intentionally small; extend as you add needs (e.g., Color, AssetRef, Enum, etc.).
    /// </summary>
    [Serializable]
    public struct AGISValue
    {
        [SerializeField] private AGISParamType _type;

        [SerializeField] private bool _bool;
        [SerializeField] private int _int;
        [SerializeField] private float _float;
        [SerializeField] private string _string;
        [SerializeField] private Vector2 _vector2;
        [SerializeField] private Vector3 _vector3;
        [SerializeField] private AGIS.ESM.UGC.AGISGuid _guid;

        public AGISParamType Type => _type;

        public static AGISValue FromBool(bool v) => new AGISValue { _type = AGISParamType.Bool, _bool = v };
        public static AGISValue FromInt(int v) => new AGISValue { _type = AGISParamType.Int, _int = v };
        public static AGISValue FromFloat(float v) => new AGISValue { _type = AGISParamType.Float, _float = v };
        public static AGISValue FromString(string v) => new AGISValue { _type = AGISParamType.String, _string = v ?? string.Empty };
        public static AGISValue FromVector2(Vector2 v) => new AGISValue { _type = AGISParamType.Vector2, _vector2 = v };
        public static AGISValue FromVector3(Vector3 v) => new AGISValue { _type = AGISParamType.Vector3, _vector3 = v };
        public static AGISValue FromGuid(AGIS.ESM.UGC.AGISGuid v) => new AGISValue { _type = AGISParamType.Guid, _guid = v };

        public bool AsBool(bool fallback = default) => _type == AGISParamType.Bool ? _bool : fallback;
        public int AsInt(int fallback = default) => _type == AGISParamType.Int ? _int : fallback;
        public float AsFloat(float fallback = default) => _type == AGISParamType.Float ? _float : fallback;
        public string AsString(string fallback = "") => _type == AGISParamType.String ? (_string ?? string.Empty) : fallback;
        public Vector2 AsVector2(Vector2 fallback = default) => _type == AGISParamType.Vector2 ? _vector2 : fallback;
        public Vector3 AsVector3(Vector3 fallback = default) => _type == AGISParamType.Vector3 ? _vector3 : fallback;
        public AGIS.ESM.UGC.AGISGuid AsGuid(AGIS.ESM.UGC.AGISGuid fallback = default) => _type == AGISParamType.Guid ? _guid : fallback;
    }

    [Serializable]
    public class AGISParamValue
    {
        [SerializeField] public string key;
        [SerializeField] public AGISValue value;

        public AGISParamValue() { }

        public AGISParamValue(string key, AGISValue value)
        {
            this.key = key;
            this.value = value;
        }
    }

    /// <summary>
    /// Instance overrides only. Defaults come from schema later.
    /// </summary>
    [Serializable]
    public class AGISParamTable
    {
        [SerializeField] public List<AGISParamValue> values = new List<AGISParamValue>();

        public bool TryGet(string key, out AGISValue value)
        {
            if (values == null)
            {
                value = default;
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                var pv = values[i];
                if (pv != null && string.Equals(pv.key, key, StringComparison.Ordinal))
                {
                    value = pv.value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Sets (adds or overwrites) a value for the given key.
        /// Also removes any duplicate entries for the same key.
        /// </summary>
        public void Set(string key, AGISValue value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (values == null)
                values = new List<AGISParamValue>();

            int firstIndex = -1;
            for (int i = 0; i < values.Count; i++)
            {
                var pv = values[i];
                if (pv == null) continue;
                if (!string.Equals(pv.key, key, StringComparison.Ordinal))
                    continue;

                if (firstIndex < 0)
                {
                    firstIndex = i;
                    pv.value = value;
                }
                else
                {
                    // Remove duplicates (keep the first updated entry).
                    values.RemoveAt(i);
                    i--;
                }
            }

            if (firstIndex < 0)
                values.Add(new AGISParamValue(key, value));
        }

        /// <summary>
        /// Removes all entries for the given key.
        /// </summary>
        public void Remove(string key)
        {
            if (values == null || string.IsNullOrEmpty(key))
                return;

            for (int i = 0; i < values.Count; i++)
            {
                var pv = values[i];
                if (pv != null && string.Equals(pv.key, key, StringComparison.Ordinal))
                {
                    values.RemoveAt(i);
                    i--;
                }
            }
        }

        /// <summary>
        /// Ensures keys are unique by keeping the last occurrence of each key.
        /// Useful for validation/compilation steps.
        /// </summary>
        public void NormalizeKeepLast()
        {
            if (values == null || values.Count <= 1)
                return;

            var lastByKey = new Dictionary<string, AGISParamValue>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                var pv = values[i];
                if (pv == null || string.IsNullOrEmpty(pv.key))
                    continue;
                lastByKey[pv.key] = pv;
            }

            values.Clear();
            foreach (var kv in lastByKey)
                values.Add(kv.Value);
        }
    }
}
