using System;

namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// Who may send a message type and where it may go. The host enforces it, so every player
    /// registers each message type with the same policy.
    /// </summary>
    [Flags]
    public enum MessagePolicy
    {
        /// <summary>Anyone may send it, and the host passes it on from one client to others.</summary>
        Default = 0,
        /// <summary>Only the host may send it; the host drops it when a client sends it.</summary>
        HostOnly = 1 << 0,
        /// <summary>A client may send it to the host only; the host never passes it on to other clients.</summary>
        NoClientRelay = 1 << 1,
    }
}
