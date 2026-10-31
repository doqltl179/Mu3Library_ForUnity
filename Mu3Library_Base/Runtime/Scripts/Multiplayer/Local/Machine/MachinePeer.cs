using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using Debug = UnityEngine.Debug;

namespace Mu3Library.Multiplayer.Local.Machine
{
    /// <summary>
    /// What a <see cref="LocalBackend"/> on a machine-wide <see cref="LocalNetwork"/> runs on.
    /// Each peer listens on a port of its own on the loopback address. The process that creates a
    /// lobby keeps it: it lists the lobby in a file of the network's folder for the others to
    /// search (<see cref="MachineDirectory"/>), takes their joins over a connection that stays open
    /// while they are members, and sends each change to them. The members keep a copy, so when the
    /// owner leaves or its process ends, the first remaining member takes the lobby over and the
    /// others reconnect to it, as a Steam lobby passes to another member.
    /// </summary>
    /// <remarks>
    /// Everything runs on the main thread inside <see cref="Update"/>, on non-blocking sockets;
    /// only a closed connection finishes in the background (<see cref="LingeringSockets"/>).
    /// Every packet travels over TCP, so none is lost or reordered, whichever
    /// <see cref="DeliveryMode"/> it uses, unless the network simulates a worse one: then this
    /// side drops or holds back the session packets it sends before they reach the socket, and its
    /// quality probes ride on ping frames, which an older version of this protocol ignores.
    /// </remarks>
    internal sealed partial class MachinePeer
    {
        public const int MaxPacketBytes = 512 * 1024;

        // A connection that has sent nothing for this long sends a ping, so the other side can
        // tell a quiet connection from a stalled one; a shorter timeout pings more often.
        private const double MaxPingIntervalSeconds = 1.0;

        // A connection holds at most this much that the other side has not read yet; past it a
        // send fails, as a full send buffer does on Steam.
        private const int MaxPendingBytes = 4 * 1024 * 1024;

        // One update reads at most this much from one connection, so a fast sender cannot keep it
        // from returning.
        private const int MaxReceiveBytesPerUpdate = 4 * 1024 * 1024;

        private const int InitialBufferBytes = 16 * 1024;
        private const int MinReceiveSpace = 4 * 1024;

        private static readonly Predicate<MachineLink> IsDead = link => link.State == MachineLinkState.Dead;

        // Frames the simulated network holds back borrow their buffers from a pool of this type's
        // own, as the in-process backend's packets do.
        private static readonly ArrayPool<byte> DelayedFrames = ArrayPool<byte>.Create(MaxPacketBytes, 64);

        private readonly LocalNetwork _network;
        private readonly Socket _acceptor;
        private readonly MachineDirectory _directory;
        private readonly PacketWriter _writer = new PacketWriter();

        private readonly List<MachineLink> _links = new List<MachineLink>();
        private readonly Dictionary<ConnectionId, MachineLink> _sessions = new Dictionary<ConnectionId, MachineLink>();
        private readonly Queue<Action> _deferred = new Queue<Action>();

        private IMultiplayerBackendListener _listener;
        private bool _isDisposed = false;
        private bool _isListening = false;
        private uint _nextConnectionId = 1;

        // A join request that arrived while nothing listened, as one from a launch does before
        // the game initializes; it goes to the next listener.
        private LobbyId _heldJoinRequest = LobbyId.None;

        public PlayerId LocalPlayerId { get; }
        public string PlayerName { get; }

        /// <summary>The loopback port this player listens on; it also tells two processes of one account apart.</summary>
        public int Port { get; }

        public bool IsAvailable => !_isDisposed;
        public bool IsListening => _isListening && !_isDisposed;

        /// <summary>
        /// Test switch: this many of the next sends report failure while the connection stays
        /// open, as a full send buffer on Steam does.
        /// </summary>
        public int FailingSends { get; set; }

        // Until when SimulateOutage cuts this peer's session connections off.
        private double _outageEnd = double.NegativeInfinity;
        private bool IsCutOff => Now < _outageEnd;

        private double Now => _network.Now;

        /// <summary>How long a quiet connection or an unanswered request may wait; 0 forever.</summary>
        private double TimeoutSeconds => _network.ConnectionTimeoutSeconds > 0f ? _network.ConnectionTimeoutSeconds : 0.0;

        /// <summary>Several pings fit in the timeout, so a connection that only waits for frames never runs out.</summary>
        private double PingIntervalSeconds
        {
            get
            {
                double timeout = TimeoutSeconds;
                return timeout > 0.0 ? Math.Min(MaxPingIntervalSeconds, timeout / 4.0) : MaxPingIntervalSeconds;
            }
        }



        public MachinePeer(LocalNetwork network, string playerName, PlayerId playerId)
        {
            _network = network;
            LocalPlayerId = playerId.IsValid ? playerId : new PlayerId(NewId(0x7FFFFFFFUL));
            PlayerName = string.IsNullOrEmpty(playerName)
                ? $"Player {LocalPlayerId.Value.ToString(CultureInfo.InvariantCulture)}"
                : playerName;

            try
            {
                _acceptor = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _acceptor.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                _acceptor.Listen(64);

                // A connection that resets between Poll and Accept leaves the queue on some
                // systems; a blocking Accept would then hold the main thread until the next one.
                _acceptor.Blocking = false;
                Port = ((IPEndPoint)_acceptor.LocalEndPoint).Port;
            }
            catch (Exception e) when (e is SocketException || e is NotSupportedException)
            {
                // A platform without sockets, such as WebGL.
                Debug.LogError($"[Multiplayer] The local backend cannot listen on the loopback address: {e.Message}");
                _acceptor?.Close();
                _acceptor = null;
                _isDisposed = true;
            }

            _directory = new MachineDirectory(network.MachineFolder, Port);
        }

        public void SetListener(IMultiplayerBackendListener listener)
        {
            _listener = listener;

            if (listener != null && _heldJoinRequest.IsValid && !_isDisposed)
            {
                LobbyId lobby = _heldJoinRequest;
                _heldJoinRequest = LobbyId.None;
                Defer(() => DeliverJoinRequest(lobby));
            }
        }

        public void Update()
        {
            if (_isDisposed)
            {
                return;
            }

            // Whatever a callback queues waits for the next Update, as an answer from another
            // process would.
            int count = _deferred.Count;
            for (int i = 0; i < count && !_isDisposed; i++)
            {
                _deferred.Dequeue()();
            }

            double now = Now;
            AcceptIncoming(now);

            int linkCount = _links.Count;
            for (int i = 0; i < linkCount && !_isDisposed; i++)
            {
                UpdateLink(_links[i], now);
            }

            if (_isDisposed)
            {
                return;
            }

            UpdateLobbies(now);
            _links.RemoveAll(IsDead);
        }

        public void RequestJoin(LobbyId lobby)
        {
            Defer(() => DeliverJoinRequest(lobby));
        }

        /// <summary>
        /// Ends every connection the way a process that crashed does: the sockets reset, nobody
        /// hears why, and the lobby file stays behind until it goes stale.
        /// </summary>
        public void SimulateCrash()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            for (int i = 0; i < _links.Count; i++)
            {
                Abort(_links[i].Socket);
                _links[i].State = MachineLinkState.Dead;
            }

            Abort(_acceptor);
            Clear();
        }

        /// <summary>
        /// Cuts this peer's session connections off: nothing is sent or read on them, so each side
        /// times out on its own, and none opens either way. Lobby links go on as before.
        /// </summary>
        public void SimulateOutage(float seconds)
        {
            if (!_isDisposed)
            {
                _outageEnd = Math.Max(_outageEnd, Now + seconds);
            }
        }

        /// <summary>
        /// Stops the way the in-process backend does: the lobbies this player owns pass to their
        /// next member, and the other players see their connections drop without a reason.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            HandOverOwnedLobbies();
            _isDisposed = true;

            for (int i = 0; i < _links.Count; i++)
            {
                CloseLink(_links[i], sendClose: false, 0);
            }

            try
            {
                _acceptor.Close();
            }
            catch (ObjectDisposedException)
            {
                // Already closed.
            }

            _directory.Remove();
            Clear();
        }

        private void Clear()
        {
            _links.Clear();
            _sessions.Clear();
            _lobbies.Clear();
            _joins.Clear();
            _deferred.Clear();
            _listener = null;
        }

        private void Defer(Action action)
        {
            if (!_isDisposed)
            {
                _deferred.Enqueue(action);
            }
        }

        private void DeliverJoinRequest(LobbyId lobby)
        {
            if (_listener != null)
            {
                _listener.OnLobbyJoinRequested(lobby);
            }
            else
            {
                _heldJoinRequest = lobby;
            }
        }

        /// <summary>
        /// A random ID up to <paramref name="mask"/>. It comes from a GUID rather than
        /// <see cref="Random"/>, whose seed comes from the clock: processes that start in the same
        /// moment, as several players launched together do, would draw the same numbers.
        /// </summary>
        private static ulong NewId(ulong mask)
        {
            ulong value = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0) & mask;
            return value != 0 ? value : 1;
        }

        #region Links
        private void AcceptIncoming(double now)
        {
            while (!_isDisposed)
            {
                Socket socket;
                try
                {
                    if (!_acceptor.Poll(0, SelectMode.SelectRead))
                    {
                        return;
                    }

                    socket = _acceptor.Accept();
                    socket.Blocking = false;
                    socket.NoDelay = true;
                }
                catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
                {
                    return;
                }

                double timeout = TimeoutSeconds;
                _links.Add(new MachineLink
                {
                    Role = MachineLinkRole.Unknown,
                    State = MachineLinkState.Open,
                    Socket = socket,
                    Received = new byte[InitialBufferBytes],
                    LastReceived = now,
                    LastSent = now,
                    Deadline = timeout > 0.0 ? now + timeout : 0.0,
                });
            }
        }

        /// <summary>
        /// Starts a connection to another player's port. What it sends first goes into the queue
        /// at once, so it leaves first even when the caller sends more before the connection opens.
        /// </summary>
        private MachineLink StartLink(MachineLinkRole role, int port)
        {
            double now = Now;
            double timeout = TimeoutSeconds;
            MachineLink link = new MachineLink
            {
                Role = role,
                State = MachineLinkState.Connecting,
                Received = new byte[InitialBufferBytes],
                Deadline = timeout > 0.0 ? now + timeout : 0.0,
                LastReceived = now,
                LastSent = now,
            };
            _links.Add(link);

            try
            {
                link.Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                link.ConnectResult = link.Socket.BeginConnect(new IPEndPoint(IPAddress.Loopback, port), null, null);
            }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException || e is ArgumentException)
            {
                // Reported by the next update, never from inside the request.
                link.HasFailed = true;
            }

            return link;
        }

        private void UpdateLink(MachineLink link, double now)
        {
            if (link.State == MachineLinkState.Connecting)
            {
                FinishConnecting(link, now);
            }

            if (link.State != MachineLinkState.Open)
            {
                return;
            }

            // An outage stops everything on a session connection, pings included, so the other
            // side's timeout runs as this side's does, and what is queued waits for the end.
            if (link.Role == MachineLinkRole.Session && IsCutOff)
            {
                double silence = TimeoutSeconds;
                if (silence > 0.0 && now - link.LastReceived > silence)
                {
                    EndLink(link, LinkEnd.Lost, 0);
                }

                return;
            }

            ReleaseDelayed(link, now);
            Flush(link);
            if (link.HasFailed)
            {
                EndLink(link, LinkEnd.Lost, 0);
                return;
            }

            Receive(link, now);
            if (link.State != MachineLinkState.Open)
            {
                return;
            }

            if (link.Deadline > 0.0 && now >= link.Deadline)
            {
                EndLink(link, LinkEnd.TimedOut, 0);
                return;
            }

            double timeout = TimeoutSeconds;
            if (timeout > 0.0 && now - link.LastReceived > timeout)
            {
                // A member that only stalled reads the close once it runs again, and learns that
                // it was dropped rather than that the owner left. The lingering socket delivers
                // it even when the member's receive window is full by now.
                if (link.Role == MachineLinkRole.OwnerToMember)
                {
                    CloseLink(link, sendClose: true, 0);
                    EndMemberLink(link);
                    return;
                }

                EndLink(link, LinkEnd.Lost, 0);
                return;
            }

            if (link.Role == MachineLinkRole.Session && link.IsReported)
            {
                link.Meter.Update(now);
                if (link.Meter.TakeProbe(now))
                {
                    SendProbe(link, MachineProtocol.ProbeRequest, 0.0);
                }
            }

            if (now - link.LastSent >= PingIntervalSeconds)
            {
                QueueFrame(link, MachineFrameKind.Ping, ReadOnlySpan<byte>.Empty, force: true);
                Flush(link);
            }
        }

        private void FinishConnecting(MachineLink link, double now)
        {
            if (link.HasFailed)
            {
                EndLink(link, LinkEnd.ConnectFailed, 0);
                return;
            }

            if (!link.ConnectResult.IsCompleted)
            {
                if (link.Deadline > 0.0 && now >= link.Deadline)
                {
                    EndLink(link, LinkEnd.TimedOut, 0);
                }

                return;
            }

            try
            {
                link.Socket.EndConnect(link.ConnectResult);
                link.Socket.Blocking = false;
                link.Socket.NoDelay = true;
            }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
            {
                EndLink(link, LinkEnd.ConnectFailed, 0);
                return;
            }

            link.State = MachineLinkState.Open;
            link.LastReceived = now;
        }

        /// <summary>
        /// Queues one frame. False when the connection already holds too much the other side has
        /// not read; <paramref name="force"/> queues control frames anyway.
        /// </summary>
        private bool QueueFrame(MachineLink link, MachineFrameKind kind, ReadOnlySpan<byte> payload, bool force)
        {
            int frameBytes = MachineProtocol.HeaderBytes + 1 + payload.Length;
            if (!force && link.PendingCount + frameBytes > MaxPendingBytes)
            {
                return false;
            }

            EnsurePending(link, frameBytes);
            int at = link.PendingStart + link.PendingCount;
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(link.Pending, at, MachineProtocol.HeaderBytes), 1 + payload.Length);
            link.Pending[at + MachineProtocol.HeaderBytes] = (byte)kind;
            payload.CopyTo(new Span<byte>(link.Pending, at + MachineProtocol.HeaderBytes + 1, payload.Length));
            link.PendingCount += frameBytes;
            link.LastSent = Now;
            return true;
        }

        private static void EnsurePending(MachineLink link, int additional)
        {
            if (link.Pending == null)
            {
                link.Pending = new byte[Math.Max(InitialBufferBytes, additional)];
                return;
            }

            if (link.PendingStart + link.PendingCount + additional <= link.Pending.Length)
            {
                return;
            }

            if (link.PendingStart > 0)
            {
                Buffer.BlockCopy(link.Pending, link.PendingStart, link.Pending, 0, link.PendingCount);
                link.PendingStart = 0;
            }

            int required = link.PendingCount + additional;
            if (required > link.Pending.Length)
            {
                int size = link.Pending.Length * 2;
                while (size < required)
                {
                    size *= 2;
                }

                Array.Resize(ref link.Pending, size);
            }
        }

        /// <summary>
        /// Sends what the socket of an open connection takes now; a failure waits for the next
        /// update to be reported.
        /// </summary>
        private static void Flush(MachineLink link)
        {
            while (link.PendingCount > 0 && !link.HasFailed)
            {
                int sent = link.Socket.Send(link.Pending, link.PendingStart, link.PendingCount, SocketFlags.None, out SocketError error);
                if (error == SocketError.WouldBlock)
                {
                    return;
                }

                if (error != SocketError.Success)
                {
                    link.HasFailed = true;
                    return;
                }

                link.PendingStart += sent;
                link.PendingCount -= sent;
            }

            if (link.PendingCount == 0)
            {
                link.PendingStart = 0;
            }
        }

        private void Receive(MachineLink link, double now)
        {
            // Whole frames left over when a listener threw during the last update.
            if (link.ReceivedCount > 0)
            {
                ReadFrames(link);
            }

            int total = 0;
            while (link.State == MachineLinkState.Open && !_isDisposed && total < MaxReceiveBytesPerUpdate)
            {
                EnsureReceiveSpace(link);
                int received = link.Socket.Receive(link.Received, link.ReceivedCount, link.Received.Length - link.ReceivedCount, SocketFlags.None, out SocketError error);
                if (error == SocketError.WouldBlock)
                {
                    return;
                }

                // Zero bytes without an error: the other side closed, without the close frame
                // when it ended or crashed before sending one.
                if (error != SocketError.Success || received == 0)
                {
                    EndLink(link, LinkEnd.Lost, 0);
                    return;
                }

                link.LastReceived = now;
                link.ReceivedCount += received;
                total += received;
                ReadFrames(link);
            }
        }

        private static void EnsureReceiveSpace(MachineLink link)
        {
            if (link.Received.Length - link.ReceivedCount >= MinReceiveSpace)
            {
                return;
            }

            // The buffer holds at most one frame that has not arrived whole; make room for all of it.
            int required = link.ReceivedCount + MinReceiveSpace;
            if (link.ReceivedCount >= MachineProtocol.HeaderBytes)
            {
                int length = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(link.Received, 0, MachineProtocol.HeaderBytes));
                required = Math.Max(required, MachineProtocol.HeaderBytes + length);
            }

            int size = link.Received.Length * 2;
            while (size < required)
            {
                size *= 2;
            }

            Array.Resize(ref link.Received, size);
        }

        private void ReadFrames(MachineLink link)
        {
            int offset = 0;
            try
            {
                while (link.State == MachineLinkState.Open && !_isDisposed)
                {
                    int available = link.ReceivedCount - offset;
                    if (available < MachineProtocol.HeaderBytes)
                    {
                        break;
                    }

                    int length = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(link.Received, offset, MachineProtocol.HeaderBytes));
                    if (length < 1 || length > MachineProtocol.MaxFrameBytes)
                    {
                        // Not this protocol, or a version of it that frames differently.
                        EndLink(link, LinkEnd.Lost, 0);
                        return;
                    }

                    if (available < MachineProtocol.HeaderBytes + length)
                    {
                        break;
                    }

                    // Counted as read before the listener sees it, so a listener that throws
                    // does not get the same frame again.
                    int start = offset + MachineProtocol.HeaderBytes;
                    offset = start + length;
                    HandleFrame(link, (MachineFrameKind)link.Received[start], new ReadOnlySpan<byte>(link.Received, start + 1, length - 1));
                }
            }
            finally
            {
                if (link.State == MachineLinkState.Open)
                {
                    int remaining = link.ReceivedCount - offset;
                    if (offset > 0 && remaining > 0)
                    {
                        Buffer.BlockCopy(link.Received, offset, link.Received, 0, remaining);
                    }

                    link.ReceivedCount = remaining;
                }
            }
        }

        private void HandleFrame(MachineLink link, MachineFrameKind kind, ReadOnlySpan<byte> payload)
        {
            if (kind == MachineFrameKind.Ping)
            {
                if (!payload.IsEmpty && link.Role == MachineLinkRole.Session && link.IsReported)
                {
                    HandleProbe(link, payload);
                }

                return;
            }

            switch (link.Role)
            {
                case MachineLinkRole.Unknown:
                    if (kind == MachineFrameKind.SessionHello)
                    {
                        AcceptSession(link, payload);
                    }
                    else if (kind == MachineFrameKind.LobbyJoin)
                    {
                        AcceptJoin(link, payload);
                    }
                    else
                    {
                        EndLink(link, LinkEnd.Lost, 0);
                    }
                    break;

                case MachineLinkRole.Session:
                    HandleSessionFrame(link, kind, payload);
                    break;

                case MachineLinkRole.Joining:
                    HandleJoinReply(link, kind, payload);
                    break;

                case MachineLinkRole.MemberToOwner:
                    HandleOwnerFrame(link, kind, payload);
                    break;

                case MachineLinkRole.OwnerToMember:
                    // A member says nothing but its close, which ends the link.
                    if (kind == MachineFrameKind.Close)
                    {
                        EndLink(link, LinkEnd.Closed, 0);
                    }
                    break;
            }
        }

        /// <summary>
        /// Closes a connection from this side: the close frame, when asked for, goes out after
        /// everything already queued, and nothing about it is reported here.
        /// </summary>
        private void CloseLink(MachineLink link, bool sendClose, int reasonCode)
        {
            if (link.State == MachineLinkState.Dead)
            {
                return;
            }

            bool isOpen = link.State == MachineLinkState.Open && !link.HasFailed;
            link.State = MachineLinkState.Dead;

            // Nothing leaves a peer cut off by an outage: the reset tells the other side only
            // that the connection is gone.
            if (!isOpen || (link.Role == MachineLinkRole.Session && IsCutOff))
            {
                DiscardDelayed(link);
                Abort(link.Socket);
                link.Socket = null;
                return;
            }

            // What the simulated network still holds back goes out now, ahead of the close and
            // without the rest of its delay, since the close follows the data already sent. A
            // backend disposed without a close sends it too, as a process that ends delivers
            // what it already put on the wire, and the in-process backend does the same.
            ReleaseDelayed(link, double.PositiveInfinity);
            if (sendClose)
            {
                _writer.Clear().WriteInt(reasonCode);
                QueueFrame(link, MachineFrameKind.Close, _writer.Written, force: true);
            }

            // What the socket does not take now, the lingering socket sends.
            Flush(link);
            LingeringSockets.Close(link.Socket, link.Pending, link.PendingStart, link.PendingCount);
            link.Socket = null;
            link.PendingCount = 0;
        }

        private enum LinkEnd
        {
            /// <summary>The connection never opened.</summary>
            ConnectFailed,
            /// <summary>The other side sent its close frame.</summary>
            Closed,
            /// <summary>The connection dropped, went quiet, or broke the protocol.</summary>
            Lost,
            /// <summary>The answer it waited for did not come in time.</summary>
            TimedOut,
        }

        /// <summary>
        /// The connection ended without this side closing it: report it for its role, then let
        /// the socket go.
        /// </summary>
        private void EndLink(MachineLink link, LinkEnd end, int reasonCode)
        {
            if (link.State == MachineLinkState.Dead)
            {
                return;
            }

            link.State = MachineLinkState.Dead;
            Close(link.Socket);
            link.Socket = null;
            link.PendingCount = 0;
            DiscardDelayed(link);

            switch (link.Role)
            {
                case MachineLinkRole.Session:
                    EndSessionLink(link, end, reasonCode);
                    break;

                case MachineLinkRole.Joining:
                    EndJoinLink(link, end);
                    break;

                case MachineLinkRole.MemberToOwner:
                    EndOwnerLink(link);
                    break;

                case MachineLinkRole.OwnerToMember:
                    EndMemberLink(link);
                    break;
            }
        }

        private static void Close(Socket socket)
        {
            try
            {
                socket?.Close();
            }
            catch (ObjectDisposedException)
            {
                // Already closed.
            }
        }

        /// <summary>Closes with a reset, as a process that ends without closing its sockets does.</summary>
        private static void Abort(Socket socket)
        {
            if (socket == null)
            {
                return;
            }

            try
            {
                socket.LingerState = new LingerOption(true, 0);
            }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
            {
                // Not connected; a plain close does.
            }

            Close(socket);
        }
        #endregion
    }
}
