using System;
using System.Text;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;

namespace Mu3Library.Multiplayer
{
    public partial class MultiplayerManager
    {
        public MultiplayerResult SendChat(string text)
        {
            if (!_isInitialized || _isDisposed)
            {
                return MultiplayerResult.Failure(MultiplayerReason.NotInitialized);
            }

            if (_state == SessionState.Reconnecting)
            {
                return MultiplayerResult.Failure(MultiplayerReason.ConnectionLost);
            }

            if (_state != SessionState.Lobby)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidState);
            }

            // Text with an unpaired surrogate is refused rather than mended: UTF-8 would carry it
            // as a replacement character, so the clients would hear other text than the host's
            // own handlers, and the game learns that its text would not arrive as written.
            if (string.IsNullOrEmpty(text) || HasUnpairedSurrogate(text))
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            if (Encoding.UTF8.GetByteCount(text) > MultiplayerLimits.MaxChatMessageSize)
            {
                return MultiplayerResult.Failure(MultiplayerReason.MessageTooLarge);
            }

            if (_isHost)
            {
                HostBroadcastChat(LocalPlayerId, text);
                return MultiplayerResult.Success;
            }

            // This player hears its own message when the host passes it back, as everyone else
            // does, so every player hears the messages in the order the host passed them on.
            _writer.Begin(PacketKind.ChatToHost);
            _writer.WriteString(text);
            return SendToHost(DeliveryMode.Reliable)
                ? MultiplayerResult.Success
                : MultiplayerResult.Failure(MultiplayerReason.ConnectionLost);
        }

        private void HostHandleChat(PeerConnection sender, ref PacketReader reader)
        {
            // Checked again once decoded, since invalid UTF-8 decodes to replacement characters
            // that take more bytes than they replaced. The message text never reaches the log.
            string text = reader.ReadString(MultiplayerLimits.MaxChatMessageSize);
            if (text.Length == 0 || Encoding.UTF8.GetByteCount(text) > MultiplayerLimits.MaxChatMessageSize)
            {
                throw new FormatException("A chat message is empty or exceeds the size limit.");
            }

            // A message that crossed the start of the game on its way is too late for the lobby.
            if (_state != SessionState.Lobby)
            {
                return;
            }

            HostBroadcastChat(sender.Player, text);
        }

        /// <summary>
        /// Passes a message on to every accepted player, its sender included, and then hands it
        /// to this player's own handlers.
        /// </summary>
        private void HostBroadcastChat(PlayerId sender, string text)
        {
            if (!TryGetParticipant(sender, out Participant from))
            {
                return;
            }

            _writer.Begin(PacketKind.ChatFromHost);
            _writer.WriteULong(sender.Value);
            _writer.WriteString(text);

            int generation = _sessionGeneration;
            BroadcastToWelcomed(ConnectionId.None);
            if (generation == _sessionGeneration)
            {
                Raise(OnChatReceived, from, text);
            }
        }

        private void HandleChatFromHost(ref PacketReader reader)
        {
            PlayerId sender = new PlayerId(reader.ReadULong());
            string text = reader.ReadString(MultiplayerLimits.MaxChatMessageSize);

            // The host passes a message on before any news of its sender leaving, so a sender
            // missing from the roster means a packet this player cannot place.
            if (TryGetParticipant(sender, out Participant from))
            {
                Raise(OnChatReceived, from, text);
            }
        }
    }
}
