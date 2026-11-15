using System;

namespace Mu3Library.Multiplayer.Backend
{
    /// <summary>
    /// Identifies one connection a backend holds. The same account may hold two connections at
    /// once, so the session tells connections apart by this rather than by <see cref="PlayerId"/>.
    /// </summary>
    public readonly struct ConnectionId : IEquatable<ConnectionId>
    {
        public static readonly ConnectionId None = default;

        public uint Value { get; }
        public bool IsValid => Value != 0;

        public ConnectionId(uint value)
        {
            Value = value;
        }

        public bool Equals(ConnectionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ConnectionId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(ConnectionId left, ConnectionId right) => left.Value == right.Value;
        public static bool operator !=(ConnectionId left, ConnectionId right) => left.Value != right.Value;
    }
}
