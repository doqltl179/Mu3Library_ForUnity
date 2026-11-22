using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Save;

namespace Mu3Library.SessionSave
{
    public partial class SessionSaveManager
    {
        // What the host sends, on _hostMessageType.
        private const byte OfferKind = 1;
        private const byte ChunkKind = 2;
        private const byte HostEndKind = 3;

        // What the receiving player answers, on _playerMessageType.
        private const byte AcceptKind = 1;
        private const byte PlayerEndKind = 2;

        // A chunk's kind, share ID, offset, and the length of its bytes.
        private const int ChunkOverhead = 1 + 4 + 4 + 4;

        private readonly ushort _hostMessageType;
        private readonly ushort _playerMessageType;
        private readonly MessageWriter _writer = new MessageWriter();
        private readonly List<OutgoingShare> _outgoing = new();
        private readonly List<OutgoingShare> _outgoingScratch = new();
        private IncomingShare _incoming;
        private uint _lastShareId;
        private int _nextOutgoing;
        private double _allowance;
        private double _lastPaced = double.NaN;



        #region Host
        public SessionSaveResult Share(string slotId, PlayerId player, out uint shareId)
        {
            ThrowIfDisposed();
            shareId = 0;
            if (!IsHosting)
            {
                return SessionSaveResult.Failure(SessionSaveFailureReason.NotHost);
            }

            if (player == _session.LocalPlayerId ||
                !_session.TryGetParticipant(player, out Participant participant) ||
                !participant.IsConnected)
            {
                return SessionSaveResult.Failure(SessionSaveFailureReason.PlayerNotInSession);
            }

            foreach (OutgoingShare running in _outgoing)
            {
                if (running.Player == player)
                {
                    return SessionSaveResult.Failure(SessionSaveFailureReason.ShareInProgress);
                }
            }

            SaveResult exported = Saves.ExportSlot(slotId, out byte[] data);
            if (!exported.IsSuccess)
            {
                return SessionSaveResult.FromSave(exported);
            }

            string sessionName = null;
            if (Saves.TryGetSlotInfo(slotId, out SaveSlotInfo slot))
            {
                slot.TryGetMetadata(SessionSaveKeys.SessionName, out sessionName);
            }

            uint id = NextShareId();
            _writer.Clear();
            _writer.WriteByte(OfferKind);
            _writer.WriteUInt(id);
            _writer.WriteInt(data.Length);
            _writer.WriteString(slotId);
            _writer.WriteString(sessionName);
            MultiplayerResult sent = _messenger.Send(_hostMessageType, _writer.Written, MessageTarget.To(player));
            if (!sent.IsSuccess)
            {
                return SessionSaveResult.FromSend(sent.Reason);
            }

            _outgoing.Add(new OutgoingShare
            {
                Id = id,
                Player = player,
                SlotId = slotId,
                Data = data,
                LastHeard = Now,
                Lobby = _session.CurrentLobbyId,
            });
            shareId = id;
            return SessionSaveResult.Success;
        }

        private uint NextShareId()
        {
            _lastShareId = _lastShareId == uint.MaxValue ? 1 : _lastShareId + 1;
            return _lastShareId;
        }

        private void OnPlayerMessage(MessageContext context, ReadOnlySpan<byte> payload)
        {
            if (_isDisposed || !_session.IsHost)
            {
                return;
            }

            var reader = new MessageReader(payload);
            byte kind = reader.ReadByte();
            uint id = reader.ReadUInt();
            OutgoingShare share = FindOutgoing(id);
            if (share == null || share.Player != context.Sender)
            {
                return;
            }

            if (kind == AcceptKind)
            {
                if (!share.Accepted)
                {
                    share.Accepted = true;
                    share.LastHeard = Now;
                }
            }
            else if (kind == PlayerEndKind)
            {
                SaveShareFailureReason reason = ReadReason(ref reader);
                if (reason == SaveShareFailureReason.None && !(share.Accepted && share.Sent == share.Data.Length))
                {
                    // The player cannot have stored what has not all left.
                    reason = SaveShareFailureReason.Corrupted;
                }

                EndOutgoing(share, reason, tellPlayer: false);
            }
        }

        private void UpdateOutgoing(double now)
        {
            if (_outgoing.Count > 0)
            {
                bool hosting = IsHosting;
                _outgoingScratch.Clear();
                _outgoingScratch.AddRange(_outgoing);
                foreach (OutgoingShare share in _outgoingScratch)
                {
                    if (share.Ended)
                    {
                        continue;
                    }

                    if (!hosting || _session.CurrentLobbyId != share.Lobby)
                    {
                        EndOutgoing(share, SaveShareFailureReason.SessionEnded, tellPlayer: false);
                    }
                    else if (!_session.TryGetParticipant(share.Player, out Participant participant) || !participant.IsConnected)
                    {
                        EndOutgoing(share, SaveShareFailureReason.PlayerLeft, tellPlayer: false);
                    }
                    else if ((!share.Accepted || share.Sent == share.Data.Length) && now - share.LastHeard > _shareTimeoutSeconds)
                    {
                        EndOutgoing(share, SaveShareFailureReason.Timeout, tellPlayer: true);
                    }
                }

                _outgoingScratch.Clear();
            }

            double burst = Math.Max(_shareChunkSize, _shareBytesPerSecond / 4.0);
            if (double.IsNaN(_lastPaced))
            {
                _allowance = burst;
            }
            else
            {
                _allowance = Math.Min(burst, _allowance + Math.Max(0d, now - _lastPaced) * _shareBytesPerSecond);
            }

            _lastPaced = now;
            SendChunks(now);
        }

        private void SendChunks(double now)
        {
            int chunkSize = Math.Max(1, Math.Min(_shareChunkSize, _messenger.MaxMessageSize - ChunkOverhead));
            while (_allowance > 0d)
            {
                OutgoingShare share = NextSendable();
                if (share == null)
                {
                    return;
                }

                int length = Math.Min(chunkSize, share.Data.Length - share.Sent);
                _writer.Clear();
                _writer.WriteByte(ChunkKind);
                _writer.WriteUInt(share.Id);
                _writer.WriteInt(share.Sent);
                _writer.WriteBytes(new ReadOnlySpan<byte>(share.Data, share.Sent, length));
                MultiplayerResult sent = _messenger.Send(_hostMessageType, _writer.Written, MessageTarget.To(share.Player));
                if (share.Ended)
                {
                    // An event handler of the session, raised during the send, ended the share.
                    continue;
                }

                if (!sent.IsSuccess)
                {
                    bool gone = sent.Reason == MultiplayerReason.InvalidArgument || sent.Reason == MultiplayerReason.ConnectionLost;
                    EndOutgoing(share, gone ? SaveShareFailureReason.PlayerLeft : SaveShareFailureReason.SendFailed, tellPlayer: false);
                    continue;
                }

                share.Sent += length;
                share.LastHeard = now;
                _allowance -= length + ChunkOverhead;
                OnShareProgress?.Invoke(Transfer(share));
            }
        }

        private OutgoingShare NextSendable()
        {
            int count = _outgoing.Count;
            for (int i = 0; i < count; i++)
            {
                int index = (_nextOutgoing + i) % count;
                OutgoingShare share = _outgoing[index];
                if (share.Accepted && share.Sent < share.Data.Length)
                {
                    _nextOutgoing = index + 1;
                    return share;
                }
            }

            return null;
        }

        private OutgoingShare FindOutgoing(uint id)
        {
            foreach (OutgoingShare share in _outgoing)
            {
                if (share.Id == id)
                {
                    return share;
                }
            }

            return null;
        }

        private void EndOutgoing(OutgoingShare share, SaveShareFailureReason reason, bool tellPlayer, bool raise = true)
        {
            if (share.Ended)
            {
                return;
            }

            share.Ended = true;
            _outgoing.Remove(share);
            if (tellPlayer)
            {
                _writer.Clear();
                _writer.WriteByte(HostEndKind);
                _writer.WriteUInt(share.Id);
                _writer.WriteByte((byte)reason);
                _messenger.Send(_hostMessageType, _writer.Written, MessageTarget.To(share.Player));
            }

            SaveShareTransfer transfer = Transfer(share);
            share.Data = null;
            if (!raise)
            {
                return;
            }

            if (reason == SaveShareFailureReason.None)
            {
                OnShareCompleted?.Invoke(transfer);
            }
            else
            {
                OnShareFailed?.Invoke(transfer, reason);
            }
        }

        private static SaveShareTransfer Transfer(OutgoingShare share) =>
            new SaveShareTransfer(share.Id, share.Player, true, share.SlotId, share.Sent, share.Data?.Length ?? share.Sent);
        #endregion

        #region Receiving Player
        public SessionSaveResult AcceptShare(uint shareId, string slotId)
        {
            ThrowIfDisposed();
            IncomingShare share = _incoming;
            if (share == null || share.Id != shareId || share.SlotId != null)
            {
                return SessionSaveResult.Failure(SessionSaveFailureReason.ShareNotFound);
            }

            if (!SaveLimits.IsValidSlotId(slotId))
            {
                return SessionSaveResult.FromSave(SaveResult.Failure(SaveFailureReason.InvalidKey));
            }

            share.SlotId = slotId;
            share.Buffer = new byte[share.Size];
            share.LastHeard = Now;
            _writer.Clear();
            _writer.WriteByte(AcceptKind);
            _writer.WriteUInt(share.Id);
            MultiplayerResult sent = _messenger.Send(_playerMessageType, _writer.Written, MessageTarget.Host);
            if (!sent.IsSuccess)
            {
                EndIncoming(share, SaveShareFailureReason.SendFailed, tellHost: false);
                return SessionSaveResult.FromSend(sent.Reason);
            }

            return SessionSaveResult.Success;
        }

        public SessionSaveResult DeclineShare(uint shareId)
        {
            ThrowIfDisposed();
            IncomingShare share = _incoming;
            if (share == null || share.Id != shareId || share.SlotId != null)
            {
                return SessionSaveResult.Failure(SessionSaveFailureReason.ShareNotFound);
            }

            EndIncoming(share, SaveShareFailureReason.Declined, tellHost: true);
            return SessionSaveResult.Success;
        }

        public bool CancelShare(uint shareId)
        {
            ThrowIfDisposed();
            OutgoingShare outgoing = FindOutgoing(shareId);
            if (outgoing != null)
            {
                EndOutgoing(outgoing, SaveShareFailureReason.Cancelled, tellPlayer: true);
                return true;
            }

            if (_incoming != null && _incoming.Id == shareId)
            {
                EndIncoming(_incoming, SaveShareFailureReason.Cancelled, tellHost: true);
                return true;
            }

            return false;
        }

        public void GetShares(List<SaveShareTransfer> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            foreach (OutgoingShare share in _outgoing)
            {
                results.Add(Transfer(share));
            }

            if (_incoming != null)
            {
                results.Add(Transfer(_incoming));
            }
        }

        private void OnHostMessage(MessageContext context, ReadOnlySpan<byte> payload)
        {
            if (_isDisposed || _session.IsHost || context.Sender != _session.HostId)
            {
                return;
            }

            var reader = new MessageReader(payload);
            byte kind = reader.ReadByte();
            uint id = reader.ReadUInt();
            switch (kind)
            {
                case OfferKind:
                {
                    int size = reader.ReadInt();
                    string sourceSlotId = reader.ReadString();
                    string sessionName = reader.ReadString();
                    HandleOffer(context.Sender, id, size, sourceSlotId, sessionName);
                    break;
                }
                case ChunkKind:
                {
                    int offset = reader.ReadInt();
                    HandleChunk(id, offset, reader.ReadBytesSpan());
                    break;
                }
                case HostEndKind:
                {
                    SaveShareFailureReason reason = ReadReason(ref reader);
                    if (_incoming != null && _incoming.Id == id)
                    {
                        EndIncoming(_incoming, reason == SaveShareFailureReason.None ? SaveShareFailureReason.Cancelled : reason, tellHost: false);
                    }

                    break;
                }
            }
        }

        private void HandleOffer(PlayerId from, uint id, int size, string sourceSlotId, string sessionName)
        {
            if (_incoming != null && _incoming.From == from && _incoming.Id != id)
            {
                // The host runs one share to a player at a time, so a new one from it means it ended
                // the last one without reaching this player, as when this player was away a moment.
                EndIncoming(_incoming, SaveShareFailureReason.Cancelled, tellHost: false);
            }

            if (_incoming != null)
            {
                SendPlayerEnd(id, SaveShareFailureReason.Busy);
                return;
            }

            if (size <= 0)
            {
                SendPlayerEnd(id, SaveShareFailureReason.Corrupted);
                return;
            }

            if (size > Saves.MaxSlotSize)
            {
                SendPlayerEnd(id, SaveShareFailureReason.TooLarge);
                return;
            }

            _incoming = new IncomingShare
            {
                Id = id,
                From = from,
                Size = size,
                LastHeard = Now,
                Lobby = _session.CurrentLobbyId,
            };

            _session.TryGetParticipant(from, out Participant host);
            OnShareOffered?.Invoke(new SaveShareOffer(id, from, host.Name, size, sourceSlotId, sessionName));
        }

        private void HandleChunk(uint id, int offset, ReadOnlySpan<byte> bytes)
        {
            IncomingShare share = _incoming;
            if (share == null || share.Id != id || share.SlotId == null)
            {
                return;
            }

            if (offset != share.Received || bytes.Length == 0 || bytes.Length > share.Size - share.Received)
            {
                EndIncoming(share, SaveShareFailureReason.Corrupted, tellHost: true);
                return;
            }

            bytes.CopyTo(new Span<byte>(share.Buffer, share.Received, bytes.Length));
            share.Received += bytes.Length;
            share.LastHeard = Now;
            if (share.Received < share.Size)
            {
                OnShareProgress?.Invoke(Transfer(share));
                return;
            }

            // The save is stored, and the share ends, even when a handler of the last progress throws.
            SaveResult imported = Saves.ImportSlot(share.SlotId, share.Buffer);
            SaveShareFailureReason reason = imported.IsSuccess ? SaveShareFailureReason.None : StoreFailure(imported.Reason);
            try
            {
                if (!share.Ended)
                {
                    OnShareProgress?.Invoke(Transfer(share));
                }
            }
            finally
            {
                EndIncoming(share, reason, tellHost: true);
            }
        }

        private static SaveShareFailureReason StoreFailure(SaveFailureReason reason)
        {
            switch (reason)
            {
                case SaveFailureReason.Corrupted:
                case SaveFailureReason.VersionTooNew:
                    return SaveShareFailureReason.Corrupted;
                case SaveFailureReason.TooLarge:
                    return SaveShareFailureReason.TooLarge;
                default:
                    return SaveShareFailureReason.StoreFailed;
            }
        }

        private void UpdateIncoming(double now)
        {
            IncomingShare share = _incoming;
            if (share == null)
            {
                return;
            }

            if (_session.IsHost || _session.State == SessionState.Offline ||
                _session.HostId != share.From || _session.CurrentLobbyId != share.Lobby)
            {
                EndIncoming(share, SaveShareFailureReason.SessionEnded, tellHost: false);
            }
            else if (now - share.LastHeard > _shareTimeoutSeconds)
            {
                EndIncoming(share, SaveShareFailureReason.Timeout, tellHost: true);
            }
        }

        private void EndIncoming(IncomingShare share, SaveShareFailureReason reason, bool tellHost, bool raise = true)
        {
            if (share.Ended)
            {
                return;
            }

            share.Ended = true;
            if (_incoming == share)
            {
                _incoming = null;
            }

            if (tellHost)
            {
                SendPlayerEnd(share.Id, reason);
            }

            SaveShareTransfer transfer = Transfer(share);
            share.Buffer = null;
            if (!raise)
            {
                return;
            }

            if (reason == SaveShareFailureReason.None)
            {
                OnShareCompleted?.Invoke(transfer);
            }
            else
            {
                OnShareFailed?.Invoke(transfer, reason);
            }
        }

        private void SendPlayerEnd(uint id, SaveShareFailureReason reason)
        {
            _writer.Clear();
            _writer.WriteByte(PlayerEndKind);
            _writer.WriteUInt(id);
            _writer.WriteByte((byte)reason);
            _messenger.Send(_playerMessageType, _writer.Written, MessageTarget.Host);
        }

        private static SaveShareTransfer Transfer(IncomingShare share) =>
            new SaveShareTransfer(share.Id, share.From, false, share.SlotId, share.Received, share.Size);
        #endregion

        #region Shared
        private void RegisterMessages()
        {
            MultiplayerResult host = _messenger.RegisterMessage(_hostMessageType, OnHostMessage, MessagePolicy.HostOnly);
            if (!host.IsSuccess)
            {
                throw new InvalidOperationException($"Message type {_hostMessageType} could not be registered for the session save: {host.Reason}.");
            }

            MultiplayerResult player = _messenger.RegisterMessage(_playerMessageType, OnPlayerMessage, MessagePolicy.NoClientRelay);
            if (!player.IsSuccess)
            {
                _messenger.UnregisterMessage(_hostMessageType);
                throw new InvalidOperationException($"Message type {_playerMessageType} could not be registered for the session save: {player.Reason}.");
            }
        }

        private void CancelAllShares()
        {
            // A list of its own, since Dispose may run from a handler while Update walks the scratch list.
            foreach (OutgoingShare share in _outgoing.ToArray())
            {
                EndOutgoing(share, SaveShareFailureReason.Cancelled, tellPlayer: true, raise: false);
            }

            if (_incoming != null)
            {
                EndIncoming(_incoming, SaveShareFailureReason.Cancelled, tellHost: true, raise: false);
            }
        }

        private static SaveShareFailureReason ReadReason(ref MessageReader reader)
        {
            byte value = reader.ReadByte();
            return value <= (byte)SaveShareFailureReason.StoreFailed ? (SaveShareFailureReason)value : SaveShareFailureReason.Cancelled;
        }
        #endregion



        private sealed class OutgoingShare
        {
            public uint Id;
            public PlayerId Player;
            public string SlotId;
            public byte[] Data;
            public int Sent;
            public bool Accepted;
            public bool Ended;

            // When the share last heard from the player or sent to them: the offer, the
            // acceptance, or the last chunk.
            public double LastHeard;
            public LobbyId Lobby;
        }

        private sealed class IncomingShare
        {
            public uint Id;
            public PlayerId From;
            public int Size;
            public string SlotId;
            public byte[] Buffer;
            public int Received;
            public bool Ended;
            public double LastHeard;
            public LobbyId Lobby;
        }
    }
}
