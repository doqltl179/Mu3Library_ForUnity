namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// Who receives a message. The values travel between players, so a value is never renumbered
    /// or reused.
    /// </summary>
    public enum MessageTargetKind : byte
    {
        /// <summary>No target; what an unset <see cref="MessageTarget"/> holds. Sending to it is refused.</summary>
        None = 0,
        /// <summary>The host only.</summary>
        Host = 1,
        /// <summary>One player.</summary>
        Player = 2,
        /// <summary>Every player, the sender included.</summary>
        All = 3,
        /// <summary>Every player except the sender.</summary>
        Others = 4,
        /// <summary>Every player except one, the sender included unless it is that one.</summary>
        AllExcept = 5,
    }
}
