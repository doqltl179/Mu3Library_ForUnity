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
    /// <remarks>
    /// What the main thread queues waits in <see cref="Pending"/> until it goes to the peer's
    /// sending thread, which takes it from <see cref="Outbox"/> into <see cref="Sending"/> and
    /// hands the socket what it takes. The three buffers change places rather than copy, and the
    /// outbox and the sending thread's fields are shared under the peer's send gate.
    /// </remarks>
    internal sealed class MachineLink
    {
        public MachineLinkRole Role;
        public MachineLinkState State;
        public Socket Socket;
        public IAsyncResult ConnectResult;

        /// <summary>A send failed, on the main thread or the sending thread; the next update reports the connection lost.</summary>
        public volatile bool HasFailed;

        /// <summary>When the connection must have opened or answered; 0 when nothing is awaited.</summary>
        public double Deadline;
        public double LastReceived;
        public double LastSent;

        public byte[] Received;
        public int ReceivedCount;

        // Main thread: what the session and the lobbies queued and the sending thread has not got yet.
        public byte[] Pending;
        public int PendingCount;

        // Under the send gate: what went to the sending thread and the socket has not taken yet.
        public byte[] Outbox;
        public int OutboxCount;
        public byte[] Sending;
        public int SendingStart;
        public int SendingCount;

        /// <summary>The bytes in the outbox and the sending buffer, written under the send gate and read by the main thread without it.</summary>
        public int HandedBytes;

        /// <summary>Under the send gate: on the sending thread's list.</summary>
        public bool IsSendListed;

        /// <summary>Under the send gate: the sending thread is in a socket call for this link, outside the gate.</summary>
        public bool IsInSocketCall;

        /// <summary>Under the send gate: how the sending thread lets the link go once its socket call returns.</summary>
        public MachineSendEnd SendEnd;

        // Session
        public ConnectionId Connection;
        public PlayerId Remote;

        /// <summary>The lobby owner's port that a join or rejoin connected to; 0 on other connections.</summary>
        public int RemotePort;
        public string RemoteName;
        public bool IsIncoming;
        /// <summary>The listener heard <see cref="IMultiplayerBackendListener.OnConnected"/> for it.</summary>
        public bool IsReported;

        // Quality and simulation, on a session connection
        /// <summary>Set once the connection is reported.</summary>
        public ConnectionMeter Meter;

        /// <summary>Frames the simulated network holds back before they go out; null until one is.</summary>
        public DelayQueue<MachineDelayedFrame> Delayed;
        public int DelayedBytes;

        /// <summary>When the last reliable frame held back goes out; the next one goes no earlier.</summary>
        public double LastOrderedDue;

        /// <summary>Packets this side sent on the connection, and how many of them the simulated network lost.</summary>
        public long PacketsOut;
        public long PacketsLostOut;

        /// <summary>The other side's counts as its newest probe told them.</summary>
        public long PacketsIn;
        public long PacketsLostIn;

        // Lobby
        public LobbyId Lobby;
        public bool IsRejoin;
        public List<Action<MultiplayerReason>> JoinCallbacks;
    }
}
