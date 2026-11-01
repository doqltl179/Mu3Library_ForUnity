using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using UnityEngine;

namespace Mu3Library.Multiplayer
{
    public partial class MultiplayerManager
    {
        // The game's own lobby data, without the library's reserved keys: on the host, what it
        // also wrote to the lobby; on a client, what the host reported from the welcome on.
        private readonly Dictionary<string, string> _lobbyData = new Dictionary<string, string>();
        private ReadOnlyDictionary<string, string> _lobbyDataView = null;

        // On a client, this player's own participant data as the host holds it once it has taken
        // every request this player sent. Only this player changes its own data, and the host
        // applies the same rules in the same order, so a request that passes against this copy is
        // one the host takes, while the copy the host reported back may still lag behind.
        private readonly Dictionary<string, string> _requestedData = new Dictionary<string, string>();

        public IReadOnlyDictionary<string, string> LobbyData => _lobbyDataView ??= new ReadOnlyDictionary<string, string>(_lobbyData);

        // The UTF-8 bytes of the longest key a data request may hold: three for each character.
        private const int MaxDataKeyBytes = MultiplayerLimits.MaxDataKeyLength * 3;



        #region Requests
        public MultiplayerResult SetLobbyData(string key, string value)
        {
            MultiplayerReason check = CheckHostRequest(null);
            if (check == MultiplayerReason.None)
            {
                check = CheckDataEntry(key, value, _lobbyData, MultiplayerLimits.MaxLobbyDataSize);
                WarnIfInvalid(check, key);
            }

            if (check != MultiplayerReason.None)
            {
                return MultiplayerResult.Failure(check);
            }

            if (_lobbyData.TryGetValue(key, out string current) && current == value)
            {
                return MultiplayerResult.Success;
            }

            // Lobby search reads the lobby's own copy, so the lobby takes the value first.
            if (!_backend.SetLobbyData(_lobbyId, key, value))
            {
                return MultiplayerResult.Failure(MultiplayerReason.BackendError);
            }

            _lobbyData[key] = value;

            _writer.Begin(PacketKind.LobbyDataChanged);
            _writer.WriteString(key);
            _writer.WriteString(value);

            int generation = _sessionGeneration;
            BroadcastToWelcomed(ConnectionId.None);
            if (generation == _sessionGeneration)
            {
                Raise(OnLobbyDataChanged, key, value);
            }

            return MultiplayerResult.Success;
        }

        public MultiplayerResult SetParticipantData(string key, string value)
        {
            if (!_isInitialized || _isDisposed)
            {
                return MultiplayerResult.Failure(MultiplayerReason.NotInitialized);
            }

            if (_state == SessionState.Reconnecting)
            {
                return MultiplayerResult.Failure(MultiplayerReason.ConnectionLost);
            }

            int index = IndexOfParticipant(LocalPlayerId);
            if ((_state != SessionState.Lobby && _state != SessionState.InGame) || index < 0)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidState);
            }

            IEnumerable<KeyValuePair<string, string>> current = _isHost ? _participants[index].Data : _requestedData;
            MultiplayerReason check = CheckDataEntry(key, value, current, MultiplayerLimits.MaxParticipantDataSize);
            WarnIfInvalid(check, key);
            if (check != MultiplayerReason.None)
            {
                return MultiplayerResult.Failure(check);
            }

            if (_isHost)
            {
                HostApplyParticipantData(LocalPlayerId, key, value);
                return MultiplayerResult.Success;
            }

            // The host decides; this player's own data changes when the host reports it back.
            _writer.Begin(PacketKind.SetParticipantData);
            _writer.WriteString(key);
            _writer.WriteString(value);
            if (!SendToHost(DeliveryMode.Reliable))
            {
                return MultiplayerResult.Failure(MultiplayerReason.ConnectionLost);
            }

            _requestedData[key] = value;
            return MultiplayerResult.Success;
        }
        #endregion

        #region Host
        /// <summary>
        /// Takes a client's request for its own participant data. The packet names no player, so
        /// the player is always the one the connection belongs to.
        /// </summary>
        private void HostHandleParticipantData(PeerConnection peer, ref PacketReader reader)
        {
            ReadDataRequest(ref reader, out string key, out string value);
            HostTakeParticipantData(peer.Player, key, value);
        }

        /// <summary>
        /// Reads a client's data request. A key or value whose UTF-8 bytes could never fit the
        /// rules makes the packet malformed before it is decoded, so a request held back for the
        /// rate limits never holds more than its rules allow.
        /// </summary>
        private static void ReadDataRequest(ref PacketReader reader, out string key, out string value)
        {
            key = reader.ReadString(MaxDataKeyBytes);
            value = reader.ReadString(MultiplayerLimits.MaxParticipantDataSize);
        }

        /// <summary>Checks a client's request for its own participant data and applies it, now or once the rate limits have room.</summary>
        private void HostTakeParticipantData(PlayerId player, string key, string value)
        {
            int index = IndexOfParticipant(player);
            if (index < 0)
            {
                return;
            }

            // A client checks the same rules against its own requests before it sends one, so
            // only a client that skips the check gets here.
            MultiplayerReason check = CheckDataEntry(key, value, _participants[index].Data, MultiplayerLimits.MaxParticipantDataSize);
            if (check != MultiplayerReason.None)
            {
                if (IsFirstParticipantDataDrop(check))
                {
                    Debug.LogWarning($"[Multiplayer] Dropped participant data from {player}: {check}. Further refusals for this reason go unlogged until the session ends.");
                }

                return;
            }

            HostApplyParticipantData(player, key, value);
        }

        private void HostApplyParticipantData(PlayerId player, string key, string value)
        {
            int index = IndexOfParticipant(player);
            if (index < 0)
            {
                return;
            }

            Participant current = _participants[index];
            if (current.Data.TryGetValue(key, out string existing) && existing == value)
            {
                return;
            }

            Participant updated = current.WithData(WithDataEntry(current.Data, key, value));
            _participants[index] = updated;

            _writer.Begin(PacketKind.ParticipantDataChanged);
            _writer.WriteULong(player.Value);
            _writer.WriteString(key);
            _writer.WriteString(value);

            int generation = _sessionGeneration;
            BroadcastToWelcomed(ConnectionId.None);
            if (generation == _sessionGeneration)
            {
                Raise(OnParticipantDataChanged, updated, key);
            }
        }
        #endregion

        #region Client
        private void HandleLobbyDataChanged(ref PacketReader reader)
        {
            string key = reader.ReadString(MaxDataKeyBytes);
            string value = reader.ReadString(MultiplayerLimits.MaxLobbyDataSize);
            if (!IsValidDataKey(key))
            {
                throw new FormatException("The host changed lobby data under an invalid key.");
            }

            _lobbyData[key] = value;
            Raise(OnLobbyDataChanged, key, value);
        }

        private void HandleParticipantDataChanged(ref PacketReader reader)
        {
            PlayerId player = new PlayerId(reader.ReadULong());
            string key = reader.ReadString(MaxDataKeyBytes);
            string value = reader.ReadString(MultiplayerLimits.MaxParticipantDataSize);
            if (!IsValidDataKey(key))
            {
                throw new FormatException("The host changed participant data under an invalid key.");
            }

            int index = IndexOfParticipant(player);
            if (index < 0)
            {
                return;
            }

            Participant updated = _participants[index].WithData(WithDataEntry(_participants[index].Data, key, value));
            _participants[index] = updated;
            Raise(OnParticipantDataChanged, updated, key);
        }

        /// <summary>Takes this player's own data from the welcome, which its later requests build on.</summary>
        private void TakeRequestedData(IReadOnlyDictionary<string, string> own)
        {
            _requestedData.Clear();
            foreach (KeyValuePair<string, string> pair in own)
            {
                _requestedData.Add(pair.Key, pair.Value);
            }
        }

        private void ResetDataState()
        {
            _lobbyData.Clear();
            _requestedData.Clear();
        }
        #endregion

        #region Rules And Packets
        private static bool IsValidDataKey(string key)
        {
            return !string.IsNullOrEmpty(key)
                && key.Length <= MultiplayerLimits.MaxDataKeyLength
                && IsCleanText(key)
                && !LobbyDataKeys.IsReserved(key);
        }

        /// <summary>
        /// False for text that does not reach the other players unchanged: a NUL character, where
        /// Steam ends a string, or a surrogate without its pair, so the other players would hold
        /// another key or value than the sender.
        /// </summary>
        private static bool IsCleanText(string text) => text.IndexOf('\0') < 0 && !HasUnpairedSurrogate(text);

        /// <summary>
        /// True for text with a UTF-16 surrogate without its pair, as a text field that cuts an
        /// emoji in half leaves. UTF-8 turns it into a replacement character, so the other players
        /// would read other text than the sender.
        /// </summary>
        private static bool HasUnpairedSurrogate(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                    {
                        return true;
                    }

                    i++;
                }
                else if (char.IsLowSurrogate(c))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks one entry of lobby or participant data, and that <paramref name="data"/> stays
        /// within <paramref name="limit"/> once the entry takes the place of the one under the same key.
        /// </summary>
        private static MultiplayerReason CheckDataEntry(string key, string value, IEnumerable<KeyValuePair<string, string>> data, int limit)
        {
            if (!IsValidDataKey(key) || value == null || !IsCleanText(value))
            {
                return MultiplayerReason.InvalidArgument;
            }

            long size = (long)Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(value);
            foreach (KeyValuePair<string, string> pair in data)
            {
                if (size > limit)
                {
                    break;
                }

                if (!string.Equals(pair.Key, key, StringComparison.Ordinal))
                {
                    size += Encoding.UTF8.GetByteCount(pair.Key) + Encoding.UTF8.GetByteCount(pair.Value ?? string.Empty);
                }
            }

            return size > limit ? MultiplayerReason.MessageTooLarge : MultiplayerReason.None;
        }

        private static void WarnIfInvalid(MultiplayerReason check, string key)
        {
            if (check == MultiplayerReason.InvalidArgument)
            {
                Debug.LogWarning($"[Multiplayer] Data key '{key}' is empty, longer than {MultiplayerLimits.MaxDataKeyLength} characters, or starts with the reserved '{LobbyDataKeys.Prefix}' in any case, or the key or its value is null or holds a NUL character or an unpaired surrogate.");
            }
        }

        /// <summary>A copy of the data with one entry set, so a participant handed out before keeps its own.</summary>
        private static IReadOnlyDictionary<string, string> WithDataEntry(IReadOnlyDictionary<string, string> data, string key, string value)
        {
            Dictionary<string, string> copy = new Dictionary<string, string>(data.Count + 1);
            foreach (KeyValuePair<string, string> pair in data)
            {
                copy[pair.Key] = pair.Value;
            }

            copy[key] = value;
            return new ReadOnlyDictionary<string, string>(copy);
        }

        private static void WriteData(PacketWriter writer, IReadOnlyDictionary<string, string> data)
        {
            writer.WriteInt(data.Count);
            foreach (KeyValuePair<string, string> pair in data)
            {
                writer.WriteString(pair.Key);
                writer.WriteString(pair.Value);
            }
        }

        /// <summary>
        /// Reads what <see cref="WriteData"/> wrote. Every entry takes at least one byte of
        /// <paramref name="limit"/>, and no value more than all of it.
        /// </summary>
        private static Dictionary<string, string> ReadData(ref PacketReader reader, int limit)
        {
            int count = reader.ReadInt();
            if (count < 0 || count > limit)
            {
                throw new FormatException($"Invalid data entry count {count}.");
            }

            Dictionary<string, string> data = new Dictionary<string, string>(count);
            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString(MaxDataKeyBytes);
                string value = reader.ReadString(limit);
                if (!IsValidDataKey(key))
                {
                    throw new FormatException("Data under an invalid key.");
                }

                data[key] = value;
            }

            return data;
        }
        #endregion
    }
}
