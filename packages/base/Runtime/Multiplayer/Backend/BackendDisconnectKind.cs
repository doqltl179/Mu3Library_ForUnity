namespace Mu3Library.Multiplayer.Backend
{
    public enum BackendDisconnectKind
    {
        /// <summary>The connection was never established.</summary>
        ConnectFailed = 0,
        /// <summary>The other side closed the connection, with the reason code it passed.</summary>
        ClosedByPeer = 1,
        /// <summary>The connection dropped: a timeout, a crash, or a network problem.</summary>
        ConnectionLost = 2,
    }
}
