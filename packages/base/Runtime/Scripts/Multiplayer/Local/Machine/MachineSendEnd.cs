namespace Mu3Library.Multiplayer.Local.Machine
{
    /// <summary>How a peer's sending thread lets a connection go once its socket call returns.</summary>
    internal enum MachineSendEnd
    {
        /// <summary>The connection is open: the thread goes on sending.</summary>
        None,
        /// <summary>The connection closed during the call: what is left goes to a lingering socket.</summary>
        Linger,
        /// <summary>The connection ended, or the main thread already passed on what was left: the thread drops it.</summary>
        Drop,
    }
}
