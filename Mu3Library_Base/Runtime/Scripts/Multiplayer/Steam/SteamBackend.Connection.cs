using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Steamworks;

namespace Mu3Library.Multiplayer.Steam
{
    public sealed partial class SteamBackend
    {
        private sealed class SteamConnection
        {
            public PlayerId Remote;
            public bool IsIncoming;

            /// <summary>True once the listener heard <see cref="IMultiplayerBackendListener.OnConnected"/> for it.</summary>
            public bool IsReported;

            /// <summary>True once Steam reported the connection closed or lost, before the close is handed on.</summary>
            public bool IsClosed;

            // What Steam last said of the connection's quality, and when, in Stopwatch ticks.
            public bool HasQualityRead;
            public long QualityReadAt;
            public bool IsQualityKnown;
            public ConnectionQuality Quality;
        }

        private struct ReceivedMessage
        {
            public uint Connection;
            public int Offset;
            public int Length;
        }

        private const int MessagesPerReceive = 64;
        private const int InitialBufferSize = 4 * 1024;

        // A receive buffer that grew past this for a burst of large messages shrinks back afterwards.
        private const int RetainedReceiveBufferSize = 1024 * 1024;

        private static readonly double TicksPerSecond = Stopwatch.Frequency;

        // Where Steam keeps a message's data, length, and connection, read straight from native
        // memory so receiving allocates nothing.
        private static readonly int DataOffset = Marshal.OffsetOf(typeof(SteamNetworkingMessage_t), nameof(SteamNetworkingMessage_t.m_pData)).ToInt32();
        private static readonly int SizeOffset = Marshal.OffsetOf(typeof(SteamNetworkingMessage_t), nameof(SteamNetworkingMessage_t.m_cbSize)).ToInt32();
        private static readonly int ConnectionOffset = Marshal.OffsetOf(typeof(SteamNetworkingMessage_t), nameof(SteamNetworkingMessage_t.m_conn)).ToInt32();

        private readonly Dictionary<uint, SteamConnection> _connections = new Dictionary<uint, SteamConnection>();
        private readonly IntPtr[] _messagePointers = new IntPtr[MessagesPerReceive];
        private readonly ReceivedMessage[] _received = new ReceivedMessage[MessagesPerReceive];
        private byte[] _receiveBuffer = new byte[InitialBufferSize];

        // Steam takes a pointer to the data it sends, so outgoing data is copied into a pinned buffer.
        private byte[] _sendBuffer;
        private GCHandle _sendHandle;

        private int _virtualPort;
        private SteamNetworkingConfigValue_t[] _connectionOptions;
        private HSteamListenSocket _listenSocket = HSteamListenSocket.Invalid;
        private HSteamNetPollGroup _pollGroup = HSteamNetPollGroup.Invalid;
        private bool _isListening = false;

        private Callback<SteamNetConnectionStatusChangedCallback_t> _connectionStatusChanged;

        /// <summary>
        /// How often Steam is asked for a connection's quality, in seconds; reads in between answer
        /// what it said last. Each answer allocates, since Steamworks.NET copies arrays of its
        /// status out of native memory. Tests lengthen it to hold an answer still.
        /// </summary>
        internal double QualityRefreshSeconds { get; set; } = 0.5;

        // Reused for every quality read; Steam fills them in.
        private SteamNetConnectionRealTimeStatus_t _realTimeStatus;
        private SteamNetConnectionRealTimeLaneStatus_t _laneStatus;



        private void InitializeConnections(SteamBackendOptions options)
        {
            _virtualPort = options.VirtualPort;
            _connectionOptions = new[]
            {
                // ICE finds direct routes between players; turned off, every connection stays on
                // Valve's relays, which keep addresses hidden. Allowed, it follows the player's
                // own Steam setting for sharing addresses.
                Int32Option(
                    ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_P2P_Transport_ICE_Enable,
                    options.AllowDirectConnections
                        ? Constants.k_nSteamNetworkingConfig_P2P_Transport_ICE_Enable_Default
                        : Constants.k_nSteamNetworkingConfig_P2P_Transport_ICE_Enable_Disable),
                Int32Option(
                    ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected,
                    (int)Math.Min(options.ConnectionTimeoutSeconds * 1000d, int.MaxValue)),
            };

            EnsureSendBuffer(InitialBufferSize);
            SteamNetworkingUtils.InitRelayNetworkAccess();
            _pollGroup = SteamNetworkingSockets.CreatePollGroup();
            _connectionStatusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(HandleConnectionStatusChanged);
        }

        public bool StartListening()
        {
            if (!IsAvailable)
            {
                return false;
            }

            if (_listenSocket == HSteamListenSocket.Invalid)
            {
                _listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(_virtualPort, _connectionOptions.Length, _connectionOptions);
                if (_listenSocket == HSteamListenSocket.Invalid)
                {
                    return false;
                }
            }

            _isListening = true;
            return true;
        }

        /// <summary>
        /// Refuses new connections. The listen socket stays open until the backend is disposed,
        /// since closing it would cut the accepted connections off before their last packets,
        /// such as the reason the host closed them, went out.
        /// </summary>
        public void StopListening()
        {
            _isListening = false;
        }

        public ConnectionId Connect(PlayerId host)
        {
            if (!IsAvailable || !host.IsValid)
            {
                return ConnectionId.None;
            }

            SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(host.Value));
            HSteamNetConnection connection = SteamNetworkingSockets.ConnectP2P(ref identity, _virtualPort, _connectionOptions.Length, _connectionOptions);
            if (connection == HSteamNetConnection.Invalid)
            {
                return ConnectionId.None;
            }

            SteamNetworkingSockets.SetConnectionPollGroup(connection, _pollGroup);
            _connections[connection.m_HSteamNetConnection] = new SteamConnection { Remote = host, IsIncoming = false };
            return new ConnectionId(connection.m_HSteamNetConnection);
        }

        public bool Send(ConnectionId connection, ReadOnlySpan<byte> data, DeliveryMode mode)
        {
            if (!IsAvailable || data.Length > MaxPacketSize || !_connections.ContainsKey(connection.Value))
            {
                return false;
            }

            EnsureSendBuffer(data.Length);
            data.CopyTo(_sendBuffer);

            int flags = mode == DeliveryMode.Reliable
                ? Constants.k_nSteamNetworkingSend_Reliable
                : Constants.k_nSteamNetworkingSend_Unreliable;
            EResult result = SteamNetworkingSockets.SendMessageToConnection(
                new HSteamNetConnection(connection.Value),
                _sendHandle.AddrOfPinnedObject(),
                (uint)data.Length,
                flags,
                out long _);

            switch (result)
            {
                case EResult.k_EResultOK:
                    return true;

                case EResult.k_EResultNoConnection:
                    // Steam marks a connection closed or lost the moment that happens, before its
                    // callback runs; the close follows with the other side's reason.
                    return true;

                default:
                    return false;
            }
        }

        public void Disconnect(ConnectionId connection, int reasonCode)
        {
            if (!IsAvailable || !_connections.Remove(connection.Value))
            {
                return;
            }

            // Lingering sends what is still queued, the close reason included, before the connection goes.
            SteamNetworkingSockets.CloseConnection(new HSteamNetConnection(connection.Value), reasonCode, null, true);
        }

        /// <remarks>
        /// Steam's own measure of the connection: its ping, the share of packets from the other
        /// side that did not arrive in order, and its data rates from recent history. Steam reports
        /// the share and the rates once it has measured a few seconds of traffic, and 0 before.
        /// The backend asks Steam at most every half second per connection and answers from that
        /// in between: a read that asks Steam allocates, since Steamworks.NET copies Steam's status
        /// into new arrays, and the reads in between allocate nothing. A connection that Steam
        /// reports closed answers false at once, even before the close is handed on.
        /// </remarks>
        public bool TryGetConnectionQuality(ConnectionId connection, out ConnectionQuality quality)
        {
            quality = default;
            if (!IsAvailable || !_connections.TryGetValue(connection.Value, out SteamConnection steam) || !steam.IsReported || steam.IsClosed)
            {
                return false;
            }

            long now = Stopwatch.GetTimestamp();
            if (!steam.HasQualityRead || now - steam.QualityReadAt >= QualityRefreshSeconds * TicksPerSecond)
            {
                steam.HasQualityRead = true;
                steam.QualityReadAt = now;
                steam.IsQualityKnown = ReadQuality(connection.Value, out steam.Quality);
            }

            quality = steam.Quality;
            return steam.IsQualityKnown;
        }

        /// <summary>Asks Steam how the connection performs; false unless Steam holds it connected.</summary>
        private bool ReadQuality(uint connection, out ConnectionQuality quality)
        {
            quality = default;
            if (SteamNetworkingSockets.GetConnectionRealTimeStatus(new HSteamNetConnection(connection), ref _realTimeStatus, 0, ref _laneStatus) != EResult.k_EResultOK
                || _realTimeStatus.m_eState != ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                return false;
            }

            // Below zero, Steam has not measured the share yet.
            float delivered = _realTimeStatus.m_flConnectionQualityLocal;
            float loss = delivered >= 0f ? Math.Min(1f, Math.Max(0f, 1f - delivered)) : 0f;
            quality = new ConnectionQuality(
                Math.Max(0, _realTimeStatus.m_nPing),
                loss,
                _realTimeStatus.m_flOutBytesPerSec,
                _realTimeStatus.m_flInBytesPerSec);
            return true;
        }

        /// <summary>
        /// Test hook: two connected connections inside this process, through Steam's own
        /// networking, as if two players had connected. Both report to this backend's listener.
        /// <paramref name="throughNetwork"/> sends the packets over the loopback address, where
        /// Steam measures the connection both ways; otherwise they skip the network, and Steam
        /// measures only what is sent.
        /// </summary>
        internal bool CreateLoopbackPair(out ConnectionId first, out ConnectionId second, bool throughNetwork = false)
        {
            first = ConnectionId.None;
            second = ConnectionId.None;
            if (!IsAvailable)
            {
                return false;
            }

            SteamNetworkingIdentity identity1 = new SteamNetworkingIdentity();
            SteamNetworkingIdentity identity2 = new SteamNetworkingIdentity();
            identity1.SetSteamID(new CSteamID(LocalPlayerId.Value));
            identity2.SetSteamID(new CSteamID(LocalPlayerId.Value));
            if (!SteamNetworkingSockets.CreateSocketPair(out HSteamNetConnection one, out HSteamNetConnection two, throughNetwork, ref identity1, ref identity2))
            {
                return false;
            }

            foreach (HSteamNetConnection connection in new[] { one, two })
            {
                SteamNetworkingSockets.SetConnectionPollGroup(connection, _pollGroup);
                _connections[connection.m_HSteamNetConnection] = new SteamConnection { Remote = LocalPlayerId, IsReported = true };
            }

            first = new ConnectionId(one.m_HSteamNetConnection);
            second = new ConnectionId(two.m_HSteamNetConnection);
            return true;
        }

        #region Steam Callbacks
        /// <remarks>
        /// Steam raises this for every connection in the process, the game's own included, so a
        /// connection this backend does not hold is left alone.
        /// </remarks>
        private void HandleConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t status)
        {
            if (_isDisposed)
            {
                return;
            }

            uint id = status.m_hConn.m_HSteamNetConnection;
            switch (status.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    if (status.m_info.m_hListenSocket == _listenSocket
                        && _listenSocket != HSteamListenSocket.Invalid
                        && !_connections.ContainsKey(id))
                    {
                        AcceptIncoming(status);
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (_connections.ContainsKey(id))
                    {
                        _events.Enqueue(new PendingEvent { Kind = EventKind.Connected, Connection = id });
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    if (!_connections.TryGetValue(id, out SteamConnection closing))
                    {
                        break;
                    }

                    closing.IsClosed = true;

                    bool closedByPeer = status.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer;
                    bool wasConnected = status.m_eOldState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected;
                    _events.Enqueue(new PendingEvent
                    {
                        Kind = EventKind.Disconnected,
                        Connection = id,
                        DisconnectKind = closedByPeer
                            ? BackendDisconnectKind.ClosedByPeer
                            : wasConnected ? BackendDisconnectKind.ConnectionLost : BackendDisconnectKind.ConnectFailed,
                        ReasonCode = closedByPeer ? status.m_info.m_eEndReason : 0,
                    });
                    break;
            }
        }

        /// <summary>
        /// Accepts a connection to the listen socket. Whether its player may join is the
        /// session's decision, made once the player says hello and the lobby lists them.
        /// </summary>
        private void AcceptIncoming(SteamNetConnectionStatusChangedCallback_t status)
        {
            if (!_isListening || SteamNetworkingSockets.AcceptConnection(status.m_hConn) != EResult.k_EResultOK)
            {
                SteamNetworkingSockets.CloseConnection(status.m_hConn, 0, null, false);
                return;
            }

            SteamNetworkingSockets.SetConnectionPollGroup(status.m_hConn, _pollGroup);
            _connections[status.m_hConn.m_HSteamNetConnection] = new SteamConnection
            {
                Remote = new PlayerId(status.m_info.m_identityRemote.GetSteamID().m_SteamID),
                IsIncoming = true,
            };
        }
        #endregion

        #region Delivery
        private void ReportConnected(uint id)
        {
            if (_connections.TryGetValue(id, out SteamConnection connection) && !connection.IsReported)
            {
                connection.IsReported = true;
                _listener?.OnConnected(new ConnectionId(id), connection.Remote, connection.IsIncoming);
            }
        }

        private void ReportDisconnected(uint id, BackendDisconnectKind kind, int reasonCode)
        {
            if (!_connections.TryGetValue(id, out SteamConnection connection))
            {
                return;
            }

            // Everything that arrived before the close is handed on first, as it was sent.
            HSteamNetConnection handle = new HSteamNetConnection(id);
            int count;
            do
            {
                count = IsAvailable
                    ? SteamNetworkingSockets.ReceiveMessagesOnConnection(handle, _messagePointers, _messagePointers.Length)
                    : 0;
                DeliverReceived(count);
            }
            while (count == _messagePointers.Length && !_isDisposed && _connections.ContainsKey(id));

            if (_isDisposed || !_connections.Remove(id))
            {
                return;
            }

            if (IsAvailable)
            {
                SteamNetworkingSockets.CloseConnection(handle, 0, null, false);
            }

            // An incoming connection the listener never heard of ends as quietly as it began.
            if (connection.IsReported || !connection.IsIncoming)
            {
                _listener?.OnDisconnected(new ConnectionId(id), connection.Remote, kind, reasonCode);
            }
        }

        private void ReceiveMessages()
        {
            while (IsAvailable)
            {
                int count = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(_pollGroup, _messagePointers, _messagePointers.Length);
                DeliverReceived(count);
                if (count < _messagePointers.Length)
                {
                    break;
                }
            }

            if (_receiveBuffer.Length > RetainedReceiveBufferSize)
            {
                _receiveBuffer = new byte[InitialBufferSize];
            }
        }

        /// <summary>
        /// Copies the received messages out and releases them back to Steam before any is handed
        /// on, so a handler that disposes the backend cannot strand a message Steam still owns.
        /// </summary>
        private void DeliverReceived(int count)
        {
            if (count <= 0)
            {
                return;
            }

            int total = 0;
            for (int i = 0; i < count; i++)
            {
                IntPtr message = _messagePointers[i];
                int length = Marshal.ReadInt32(message, SizeOffset);
                EnsureReceiveBuffer(total + length);
                if (length > 0)
                {
                    Marshal.Copy(Marshal.ReadIntPtr(message, DataOffset), _receiveBuffer, total, length);
                }

                _received[i] = new ReceivedMessage
                {
                    Connection = (uint)Marshal.ReadInt32(message, ConnectionOffset),
                    Offset = total,
                    Length = length,
                };
                total += length;
                SteamNetworkingMessage_t.Release(message);
            }

            for (int i = 0; i < count && !_isDisposed; i++)
            {
                ReceivedMessage received = _received[i];

                // Data can come in before the game's own callback run reports the connection.
                ReportConnected(received.Connection);

                // Handling the connection report may have closed the connection.
                if (!_connections.TryGetValue(received.Connection, out SteamConnection connection))
                {
                    continue;
                }

                _listener?.OnReceived(
                    new ConnectionId(received.Connection),
                    connection.Remote,
                    new ReadOnlySpan<byte>(_receiveBuffer, received.Offset, received.Length));
            }
        }
        #endregion

        #region Buffers
        private void EnsureReceiveBuffer(int size)
        {
            if (size > _receiveBuffer.Length)
            {
                Array.Resize(ref _receiveBuffer, Math.Max(size, _receiveBuffer.Length * 2));
            }
        }

        private void EnsureSendBuffer(int size)
        {
            if (_sendBuffer != null && size <= _sendBuffer.Length)
            {
                return;
            }

            if (_sendHandle.IsAllocated)
            {
                _sendHandle.Free();
            }

            int capacity = _sendBuffer == null ? size : Math.Max(size, _sendBuffer.Length * 2);
            _sendBuffer = new byte[Math.Min(capacity, MaxPacketSize)];
            _sendHandle = GCHandle.Alloc(_sendBuffer, GCHandleType.Pinned);
        }

        private static SteamNetworkingConfigValue_t Int32Option(ESteamNetworkingConfigValue value, int number)
        {
            SteamNetworkingConfigValue_t option = new SteamNetworkingConfigValue_t
            {
                m_eValue = value,
                m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
            };
            option.m_val.m_int32 = number;
            return option;
        }
        #endregion

        private void ShutdownConnections(bool steamRuns)
        {
            if (steamRuns)
            {
                foreach (uint id in _connections.Keys)
                {
                    SteamNetworkingSockets.CloseConnection(new HSteamNetConnection(id), 0, null, false);
                }

                if (_listenSocket != HSteamListenSocket.Invalid)
                {
                    SteamNetworkingSockets.CloseListenSocket(_listenSocket);
                }

                if (_pollGroup != HSteamNetPollGroup.Invalid)
                {
                    SteamNetworkingSockets.DestroyPollGroup(_pollGroup);
                }
            }

            _connections.Clear();
            _isListening = false;
            _listenSocket = HSteamListenSocket.Invalid;
            _pollGroup = HSteamNetPollGroup.Invalid;

            _connectionStatusChanged?.Dispose();
            _connectionStatusChanged = null;

            if (_sendHandle.IsAllocated)
            {
                _sendHandle.Free();
            }
        }
    }
}
