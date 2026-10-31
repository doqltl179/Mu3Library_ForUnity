using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using UnityEngine;

namespace Mu3Library.Multiplayer
{
    public partial class MultiplayerManager
    {
        private sealed class MessageRegistration
        {
            public MessageHandler Handler;
            public MessagePolicy Policy;

            // What GetMessageCounts reports, since the type was registered.
            public long Sent;
            public long Received;
        }

        private enum DropReason : byte
        {
            TooLarge,
            NotRegistered,
            HostOnly,
            NoClientRelay,
        }

        /// <summary>Why the manager dropped a packet below the game messages.</summary>
        private enum PacketDrop : byte
        {
            /// <summary>The packet ended early or held an invalid value.</summary>
            Malformed,
            /// <summary>A host's packet before the client's hello was taken, or a client's before the host's welcome.</summary>
            BeforeHandshake,
            /// <summary>A kind that only the other side sends.</summary>
            WrongDirection,
            /// <summary>
            /// Not a packet: a connection past <see cref="MultiplayerLimits.MaxPendingConnectionsPerAccount"/>,
            /// which the host closed. Its code names no packet kind.
            /// </summary>
            ExtraConnection,
        }

        private const MessagePolicy KnownPolicies = MessagePolicy.HostOnly | MessagePolicy.NoClientRelay;

        // Distinct causes logged per session: a dropped message (type and reason), refused
        // participant data, a dropped packet (kind and reason), or a handler's exception (message
        // type and exception type). A flood of one cause logs it once, and a flood of distinct
        // causes stops logging at this count.
        private const int MaxLoggedCauses = 32;

        // The codes of those causes: 0 to 0xFFFFFF for dropped messages, negative ones for
        // refused participant data, and these two ranges above for packets and handlers.
        private const int PacketDropCodes = 1 << 24;
        private const int HandlerFailureCodes = 2 << 24;

        private readonly Dictionary<ushort, MessageRegistration> _messages = new Dictionary<ushort, MessageRegistration>();
        private readonly HashSet<(int Code, Type Exception)> _loggedCauses = new HashSet<(int Code, Type Exception)>();

        // Players a reliable message could not reach. Reused, so finding none costs nothing.
        private readonly List<PeerConnection> _unreachablePeers = new List<PeerConnection>();

        private int _maxMessageSize = MultiplayerConfig.DefaultMaxMessageSize;

        // The host's own limit, from its welcome; 0 outside a session joined as a client.
        private int _hostMaxMessageSize = 0;

        public int MaxMessageSize => _hostMaxMessageSize > 0 ? Math.Min(_maxMessageSize, _hostMaxMessageSize) : _maxMessageSize;



        public MultiplayerResult RegisterMessage(ushort messageType, MessageHandler handler, MessagePolicy policy = MessagePolicy.Default)
        {
            if (_isDisposed)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidState);
            }

            if ((policy & ~KnownPolicies) != 0 || _messages.ContainsKey(messageType))
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            _messages.Add(messageType, new MessageRegistration { Handler = handler, Policy = policy });
            return MultiplayerResult.Success;
        }

        public bool UnregisterMessage(ushort messageType)
        {
            return _messages.Remove(messageType);
        }

        public MultiplayerResult Send(ushort messageType, ReadOnlySpan<byte> payload, MessageTarget target, DeliveryMode deliveryMode = DeliveryMode.Reliable)
        {
            if (!_isInitialized || _isDisposed)
            {
                return MultiplayerResult.Failure(MultiplayerReason.NotInitialized);
            }

            if (_state == SessionState.Reconnecting)
            {
                return MultiplayerResult.Failure(MultiplayerReason.ConnectionLost);
            }

            if (_state != SessionState.Lobby && _state != SessionState.InGame)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidState);
            }

            if (payload.Length > MaxMessageSize)
            {
                return MultiplayerResult.Failure(MultiplayerReason.MessageTooLarge);
            }

            if (!IsValidDeliveryMode(deliveryMode) || !IsValidTarget(target))
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            // The one player it is for is away while the host keeps their slot, so nothing reaches them.
            if (target.Kind == MessageTargetKind.Player && target.Player != LocalPlayerId
                && TryGetParticipant(target.Player, out Participant recipient) && !recipient.IsConnected)
            {
                return MultiplayerResult.Failure(MultiplayerReason.ConnectionLost);
            }

            MultiplayerResult result = _isHost
                ? HostSendMessage(messageType, payload, target, deliveryMode)
                : ClientSendMessage(messageType, payload, target, deliveryMode);

            if (result.IsSuccess && _messages.TryGetValue(messageType, out MessageRegistration registration))
            {
                registration.Sent++;
            }

            return result;
        }

        public void GetMessageCounts(List<MessageCounts> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            foreach (KeyValuePair<ushort, MessageRegistration> pair in _messages)
            {
                results.Add(new MessageCounts(pair.Key, pair.Value.Sent, pair.Value.Received));
            }
        }

        /// <summary>
        /// Whether a message that <paramref name="sender"/> addressed to <paramref name="target"/>
        /// reaches <paramref name="recipient"/>. Sender, host, and every relay decide with the same rule.
        /// </summary>
        private bool Reaches(MessageTarget target, PlayerId sender, PlayerId recipient)
        {
            switch (target.Kind)
            {
                case MessageTargetKind.Host: return recipient == _hostId;
                case MessageTargetKind.Player: return recipient == target.Player;
                case MessageTargetKind.All: return true;
                case MessageTargetKind.Others: return recipient != sender;
                case MessageTargetKind.AllExcept: return recipient != target.Player;
                default: return false;
            }
        }

        private bool IsValidTarget(MessageTarget target)
        {
            switch (target.Kind)
            {
                case MessageTargetKind.Host:
                case MessageTargetKind.All:
                case MessageTargetKind.Others:
                    return true;

                case MessageTargetKind.Player:
                    return IndexOfParticipant(target.Player) >= 0;

                case MessageTargetKind.AllExcept:
                    return target.Player.IsValid;

                default:
                    return false;
            }
        }

        private static bool IsValidDeliveryMode(DeliveryMode mode)
        {
            return mode == DeliveryMode.Reliable || mode == DeliveryMode.Unreliable;
        }

        #region Host
        private MultiplayerResult HostSendMessage(ushort messageType, ReadOnlySpan<byte> payload, MessageTarget target, DeliveryMode deliveryMode)
        {
            PlayerId self = LocalPlayerId;
            bool written = false;

            // A drop raised from an event handler of an outer send may still wait in the list.
            int unreachableBefore = _unreachablePeers.Count;
            foreach (PeerConnection peer in _peers.Values)
            {
                if (!peer.IsWelcomed || !Reaches(target, self, peer.Player))
                {
                    continue;
                }

                if (!written)
                {
                    WriteMessageFromHost(self, messageType, payload, deliveryMode);
                    written = true;
                }

                SendToPeer(peer, deliveryMode);
            }

            MultiplayerResult result = target.Kind == MessageTargetKind.Player && _unreachablePeers.Count > unreachableBefore
                ? MultiplayerResult.Failure(MultiplayerReason.ConnectionLost)
                : MultiplayerResult.Success;

            int generation = _sessionGeneration;
            DropUnreachablePeers();

            // Handled after the loop, since a handler may kick or leave and so change the peers a
            // loop walks; and only while the session the message belongs to still runs.
            if (generation == _sessionGeneration && Reaches(target, self, self))
            {
                DeliverMessage(self, messageType, deliveryMode, payload);
            }

            return result;
        }

        private void HostHandleMessage(PeerConnection sender, ref PacketReader reader)
        {
            DeliveryMode deliveryMode = ReadDeliveryMode(ref reader);
            MessageTargetKind kind = (MessageTargetKind)reader.ReadByte();
            if (kind == MessageTargetKind.None || kind > MessageTargetKind.AllExcept)
            {
                throw new FormatException($"Invalid message target {kind}.");
            }

            MessageTarget target = new MessageTarget(kind, new PlayerId(reader.ReadULong()));
            ushort messageType = reader.ReadUShort();
            ReadOnlySpan<byte> payload = reader.ReadRemaining();

            if (payload.Length > _maxMessageSize)
            {
                if (IsFirstDrop(messageType, DropReason.TooLarge))
                {
                    LogDroppedMessage(messageType, $"{payload.Length} bytes from {sender.Player} exceed the {_maxMessageSize}-byte limit");
                }

                return;
            }

            if (!_messages.TryGetValue(messageType, out MessageRegistration registration))
            {
                if (IsFirstDrop(messageType, DropReason.NotRegistered))
                {
                    LogDroppedMessage(messageType, "the host has not registered it");
                }

                return;
            }

            if ((registration.Policy & MessagePolicy.HostOnly) != 0)
            {
                if (IsFirstDrop(messageType, DropReason.HostOnly))
                {
                    LogDroppedMessage(messageType, $"only the host may send it, but {sender.Player} did");
                }

                return;
            }

            bool mayRelay = (registration.Policy & MessagePolicy.NoClientRelay) == 0;
            bool written = false;
            foreach (PeerConnection peer in _peers.Values)
            {
                // The sender handled its own copy before sending.
                if (!peer.IsWelcomed || peer.Connection == sender.Connection || !Reaches(target, sender.Player, peer.Player))
                {
                    continue;
                }

                if (!mayRelay)
                {
                    if (IsFirstDrop(messageType, DropReason.NoClientRelay))
                    {
                        LogDroppedMessage(messageType, $"it may not pass between clients, but {sender.Player} addressed {target}");
                    }

                    break;
                }

                if (!written)
                {
                    WriteMessageFromHost(sender.Player, messageType, payload, deliveryMode);
                    written = true;
                }

                SendToPeer(peer, deliveryMode);
            }

            int generation = _sessionGeneration;
            DropUnreachablePeers();

            if (generation == _sessionGeneration && Reaches(target, sender.Player, LocalPlayerId))
            {
                DeliverMessage(sender.Player, messageType, deliveryMode, payload);
            }
        }

        /// <summary>
        /// Sends what the writer holds. A reliable message that does not go out, for example
        /// because the player's send buffer is full, marks the player unreachable: its game
        /// would silently miss a message the others got.
        /// </summary>
        private void SendToPeer(PeerConnection peer, DeliveryMode deliveryMode)
        {
            if (!_backend.Send(peer.Connection, _writer.Written, deliveryMode) && deliveryMode == DeliveryMode.Reliable)
            {
                _unreachablePeers.Add(peer);
            }
        }

        private void DropUnreachablePeers()
        {
            // Taken from the end one at a time, since dropping a player raises events whose
            // handlers may send again and so use this list too.
            while (_unreachablePeers.Count > 0)
            {
                int last = _unreachablePeers.Count - 1;
                PeerConnection peer = _unreachablePeers[last];
                _unreachablePeers.RemoveAt(last);

                if (_peers.Remove(peer.Connection))
                {
                    _backend.Disconnect(peer.Connection, ReasonCodes.ToWire(MultiplayerReason.ConnectionLost));
                    LosePlayer(peer.Player);
                }
            }
        }

        private void WriteMessageFromHost(PlayerId sender, ushort messageType, ReadOnlySpan<byte> payload, DeliveryMode deliveryMode)
        {
            _writer.Begin(PacketKind.MessageFromHost);
            _writer.WriteByte((byte)deliveryMode);
            _writer.WriteULong(sender.Value);
            _writer.WriteUShort(messageType);
            _writer.WriteRemaining(payload);
        }
        #endregion

        #region Client
        private MultiplayerResult ClientSendMessage(ushort messageType, ReadOnlySpan<byte> payload, MessageTarget target, DeliveryMode deliveryMode)
        {
            PlayerId self = LocalPlayerId;

            if (_messages.TryGetValue(messageType, out MessageRegistration registration))
            {
                // The host would drop these; refusing here tells the sender why.
                if ((registration.Policy & MessagePolicy.HostOnly) != 0
                    || ((registration.Policy & MessagePolicy.NoClientRelay) != 0 && ReachesAnotherClient(target, self)))
                {
                    return MultiplayerResult.Failure(MultiplayerReason.NotHost);
                }
            }

            bool onlySelf = target.Kind == MessageTargetKind.Player && target.Player == self;
            if (!onlySelf)
            {
                _writer.Begin(PacketKind.MessageToHost);
                _writer.WriteByte((byte)deliveryMode);
                _writer.WriteByte((byte)target.Kind);
                _writer.WriteULong(target.Player.Value);
                _writer.WriteUShort(messageType);
                _writer.WriteRemaining(payload);

                if (!SendToHost(deliveryMode))
                {
                    return MultiplayerResult.Failure(MultiplayerReason.ConnectionLost);
                }
            }

            if (Reaches(target, self, self))
            {
                DeliverMessage(self, messageType, deliveryMode, payload);
            }

            return MultiplayerResult.Success;
        }

        /// <summary>
        /// Sends what the writer holds to the host. A reliable packet that does not go out loses
        /// the connection, since the host would go on without it: in a game this player starts
        /// coming back to its slot, and otherwise the session ends with
        /// <see cref="MultiplayerReason.ConnectionLost"/>. An unreliable one is only reported.
        /// </summary>
        private bool SendToHost(DeliveryMode deliveryMode)
        {
            if (_backend.Send(_hostConnection, _writer.Written, deliveryMode))
            {
                return true;
            }

            if (deliveryMode == DeliveryMode.Reliable)
            {
                LoseHost(MultiplayerReason.ConnectionLost);
            }

            return false;
        }

        private bool ReachesAnotherClient(MessageTarget target, PlayerId self)
        {
            for (int i = 0; i < _participants.Count; i++)
            {
                PlayerId player = _participants[i].Id;
                if (player != self && player != _hostId && Reaches(target, self, player))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClientHandleMessage(ref PacketReader reader)
        {
            DeliveryMode deliveryMode = ReadDeliveryMode(ref reader);
            PlayerId sender = new PlayerId(reader.ReadULong());
            ushort messageType = reader.ReadUShort();
            ReadOnlySpan<byte> payload = reader.ReadRemaining();

            if (_messages.TryGetValue(messageType, out MessageRegistration registration)
                && (registration.Policy & MessagePolicy.HostOnly) != 0
                && sender != _hostId)
            {
                if (IsFirstDrop(messageType, DropReason.HostOnly))
                {
                    LogDroppedMessage(messageType, $"only the host may send it, but it came from {sender}");
                }

                return;
            }

            DeliverMessage(sender, messageType, deliveryMode, payload);
        }
        #endregion

        /// <summary>
        /// Hands a message to its handler. A type this player has not registered is dropped with
        /// a warning; one registered without a handler is dropped quietly.
        /// </summary>
        private void DeliverMessage(PlayerId sender, ushort messageType, DeliveryMode deliveryMode, ReadOnlySpan<byte> payload)
        {
            if (!_messages.TryGetValue(messageType, out MessageRegistration registration))
            {
                if (IsFirstDrop(messageType, DropReason.NotRegistered))
                {
                    LogDroppedMessage(messageType, "this player has not registered it");
                }

                return;
            }

            if (sender != LocalPlayerId)
            {
                registration.Received++;
            }

            if (registration.Handler == null)
            {
                return;
            }

            try
            {
                registration.Handler(new MessageContext(sender, messageType, deliveryMode), payload);
            }
            catch (Exception e)
            {
                LogHandlerFailure(messageType, sender, e);
            }
        }

        /// <summary>
        /// Logs an exception that a message handler threw, the first time the handler of that type
        /// throws that exception type in a session. A <see cref="FormatException"/>, which
        /// <see cref="MessageReader"/> throws for a payload that does not hold what the handler
        /// reads, becomes a warning without a stack trace: the sender wrote another payload, so the
        /// stack of this player's handler does not show the cause, and the message names it.
        /// </summary>
        private void LogHandlerFailure(ushort messageType, PlayerId sender, Exception e)
        {
            Type type = e.GetType();
            if (!IsFirstCause(HandlerFailureCodes | messageType, type))
            {
                return;
            }

            if (e is FormatException)
            {
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",
                    $"[Multiplayer] The handler of message type {messageType} could not read a message from {sender}: {e.Message} Further {type.Name}s from this handler go unlogged until the session ends.");
                return;
            }

            Debug.LogException(e);
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",
                $"[Multiplayer] The handler of message type {messageType} threw {type.Name}. Further {type.Name}s from this handler go unlogged until the session ends.");
        }

        private static DeliveryMode ReadDeliveryMode(ref PacketReader reader)
        {
            DeliveryMode mode = (DeliveryMode)reader.ReadByte();
            if (!IsValidDeliveryMode(mode))
            {
                throw new FormatException($"Invalid delivery mode {mode}.");
            }

            return mode;
        }

        /// <summary>
        /// True the first time a type is dropped for a reason in this session, so the warning
        /// text is built only then and a flood of bad messages costs nothing more.
        /// </summary>
        private bool IsFirstDrop(ushort messageType, DropReason reason) => IsFirstCause((messageType << 8) | (int)reason, null);

        /// <summary>
        /// The same for a client's participant data that the host refused, once per reason. Its
        /// codes are negative, apart from those of the message drops.
        /// </summary>
        private bool IsFirstParticipantDataDrop(MultiplayerReason reason) => IsFirstCause(-1 - (int)reason, null);

        /// <summary>The same for a packet of a kind dropped for a reason, so a flood of bad packets logs once.</summary>
        private bool IsFirstPacketDrop(PacketKind kind, PacketDrop reason) => IsFirstCause(PacketDropCodes | ((int)kind << 8) | (int)reason, null);

        private bool IsFirstCause(int code, Type exception)
        {
            if (_loggedCauses.Count >= MaxLoggedCauses)
            {
                return false;
            }

            if (!_loggedCauses.Add((code, exception)))
            {
                return false;
            }

            if (_loggedCauses.Count == MaxLoggedCauses)
            {
                Debug.LogWarning($"[Multiplayer] {MaxLoggedCauses} causes of dropped packets, dropped messages, and handler exceptions logged; later ones go unlogged until the session ends.");
            }

            return true;
        }

        private static void LogDroppedMessage(ushort messageType, string reason)
        {
            Debug.LogWarning($"[Multiplayer] Dropped message type {messageType}: {reason}. Further drops of it for this reason go unlogged until the session ends.");
        }

        private void ResetMessageState()
        {
            _loggedCauses.Clear();
            _unreachablePeers.Clear();
            _hostMaxMessageSize = 0;
        }
    }
}
