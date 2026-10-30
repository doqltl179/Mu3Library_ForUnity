using System;

namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// Identifies one player account: a Steam ID on the Steam backend, a number the local network
    /// hands out on the local backend. One account takes one slot in a session.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public static readonly PlayerId None = default;

        public ulong Value { get; }
        public bool IsValid => Value != 0;

        public PlayerId(ulong value)
        {
            Value = value;
        }

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(PlayerId left, PlayerId right) => left.Value == right.Value;
        public static bool operator !=(PlayerId left, PlayerId right) => left.Value != right.Value;
    }
}
