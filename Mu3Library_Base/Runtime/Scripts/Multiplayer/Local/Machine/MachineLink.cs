using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Local.Machine
{
    /// <summary>
    /// One TCP connection on the loopback address, with what is still to send on it and what
    /// arrived but does not make a whole frame yet.
    /// </summary>
    internal sealed class MachineLink
    {
        public MachineLinkRole Role;
        public MachineLinkState State;
        public Socket Socket;
        public IAsyncResult ConnectResult;

        /// <summary>A send failed; the next update reports the connection lost.</summary>
        public bool HasFailed;

        /// <summary>When the connection must have opened or answered; 0 when nothing is awaited.</summary>
        public double Deadline;
        public double LastReceived;
        public double LastSent;

        public byte[] Received;
        public int ReceivedCount;
        public byte[] Pending;
        public int PendingStart;
        public int PendingCount;

        // Session
        public ConnectionId Connection;
        public PlayerId Remote;

        /// <summary>The lobby owner's port that a join or rejoin connected to; 0 on other connections.</summary>
        public int RemotePort;
        public string RemoteName;
        public bool IsIncoming;
        /// <summary>The listener heard <see cref="IMultiplayerBackendListener.OnConnected"/> for it.</summary>
        public bool IsReported;

        // Lobby
        public LobbyId Lobby;
        public bool IsRejoin;
        public List<Action<MultiplayerReason>> JoinCallbacks;
    }
}
