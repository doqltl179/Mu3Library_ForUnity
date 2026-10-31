using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Local.Machine
{
    /// <summary>
    /// A member of a lobby on a machine-wide network. A player is known by its ID and the port it
    /// listens on, since the same account may run in two processes.
    /// </summary>
    internal sealed class MachineMember
    {
        public PlayerId Id;
        public int Port;
        public string Name;

        /// <summary>The owner's connection from this member; null for the owner itself.</summary>
        public MachineLink Link;

        /// <summary>
        /// Set when this player took the lobby over: until then the member may come back to it,
        /// and after it the member counts as gone. 0 once it is back.
        /// </summary>
        public double RejoinDeadline;
    }
}
