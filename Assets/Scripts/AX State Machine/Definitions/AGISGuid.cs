// File: AGISGuid.cs
// Folder: Assets/Scripts/AX State Machine/Definitions/

using System;
using UnityEngine;

namespace AGIS.ESM.UGC
{
    /// <summary>
    /// Unity-serializable GUID wrapper (stores as string).
    /// Canonical format: "N" (32 hex, no dashes) by default.
    /// </summary>
    [Serializable]
    public struct AGISGuid : IEquatable<AGISGuid>
    {
        [SerializeField] private string _value;

        public string Value => _value;

        public bool IsValid => !string.IsNullOrWhiteSpace(_value);

        public static AGISGuid Empty => new AGISGuid(string.Empty);

        public AGISGuid(string value)
        {
            _value = value ?? string.Empty;
        }

        public static AGISGuid New()
        {
            return new AGISGuid(Guid.NewGuid().ToString("N"));
        }

        public Guid ToGuid()
        {
            if (Guid.TryParse(_value, out var g))
                return g;

            return Guid.Empty;
        }

        public override string ToString() => _value ?? string.Empty;

        public bool Equals(AGISGuid other) => string.Equals(_value, other._value, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object obj) => obj is AGISGuid other && Equals(other);

        public override int GetHashCode() => (_value ?? string.Empty).ToLowerInvariant().GetHashCode();

        public static bool operator ==(AGISGuid a, AGISGuid b) => a.Equals(b);
        public static bool operator !=(AGISGuid a, AGISGuid b) => !a.Equals(b);

        public static implicit operator string(AGISGuid id) => id.Value;
        public static explicit operator AGISGuid(string value) => new AGISGuid(value);
    }
}
