using System;

namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// Identifies one lobby: a Steam lobby ID on the Steam backend, a number the local network
    /// hands out on the local backend.
    /// </summary>
    public readonly struct LobbyId : IEquatable<LobbyId>
    {
        public static readonly LobbyId None = default;

        public ulong Value { get; }
        public bool IsValid => Value != 0;

        public LobbyId(ulong value)
        {
            Value = value;
        }

        public bool Equals(LobbyId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is LobbyId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(LobbyId left, LobbyId right) => left.Value == right.Value;
        public static bool operator !=(LobbyId left, LobbyId right) => left.Value != right.Value;
    }
}
