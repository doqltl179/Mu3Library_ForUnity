using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;

namespace Mu3Library.Multiplayer.Local.Machine
{
    internal sealed partial class MachinePeer
    {
        // How often an owner rewrites its lobby file, well inside MachineDirectory.StaleSeconds;
        // how often at most when its lobbies keep changing, as when a game sets lobby data every
        // frame; and how soon it tries again when the file could not be written.
        private const double PublishIntervalSeconds = 2.0;
        private const double PublishMinIntervalSeconds = 0.25;
        private const double PublishRetrySeconds = 0.5;

        // After a lobby passes on, how long its members have to reach the new owner, and how long
        // the new owner keeps their places; and how soon a member asks again when the new owner
        // has not noticed yet.
        private const double RejoinGraceSeconds = 5.0;
        private const double RejoinRetrySeconds = 0.1;

        private readonly Dictionary<LobbyId, MachineLobby> _lobbies = new Dictionary<LobbyId, MachineLobby>();

        // Joins waiting for the owner's answer; a second join of the same lobby waits with the first.
        private readonly Dictionary<LobbyId, MachineLink> _joins = new Dictionary<LobbyId, MachineLink>();

        private readonly List<MachineLobby> _lobbyScratch = new List<MachineLobby>();
        private bool _isDirectoryDirty = false;
        private double _nextPublish = 0.0;
        private double _nextPublishAttempt = 0.0;

        public void CreateLobby(LobbyVisibility visibility, int capacity, IReadOnlyDictionary<string, string> data, Action<MultiplayerReason, LobbyId> onCompleted)
        {
            Dictionary<string, string> snapshot = new Dictionary<string, string>();
            if (data != null)
            {
                foreach (KeyValuePair<string, string> pair in data)
                {
                    snapshot[pair.Key] = pair.Value ?? string.Empty;
                }
            }

            Defer(() =>
            {
                MachineLobby lobby = new MachineLobby
                {
                    Id = new LobbyId(NewId(0xFFFFFFFFFFFFUL)),
                    IsOwned = true,
                    OwnerId = LocalPlayerId,
                    OwnerPort = Port,
                    Visibility = visibility,
                    Capacity = capacity,
                    IsDirty = true,
                };
                lobby.Members.Add(new MachineMember { Id = LocalPlayerId, Port = Port, Name = PlayerName });
                foreach (KeyValuePair<string, string> pair in snapshot)
                {
                    lobby.Data[pair.Key] = pair.Value;
                }

                _lobbies.Add(lobby.Id, lobby);
                onCompleted?.Invoke(MultiplayerReason.None, lobby.Id);
            });
        }

        public void SearchLobbies(LobbySearchQuery query, Action<MultiplayerReason, IReadOnlyList<BackendLobby>> onCompleted)
        {
            LobbySearchQuery snapshot = query != null ? query.Clone() : new LobbySearchQuery();

            Defer(() =>
            {
                List<BackendLobby> results = new List<BackendLobby>();

                // This player's own lobbies as they are now, the others as their owners last listed them.
                foreach (MachineLobby lobby in _lobbies.Values)
                {
                    if (results.Count < snapshot.MaxResults
                        && lobby.IsOwned
                        && IsSearchable(lobby.Visibility, lobby.IsJoinable, lobby.Members.Count, lobby.Capacity)
                        && LobbyFilter.Matches(lobby.Data, snapshot))
                    {
                        results.Add(new BackendLobby(lobby.Id, lobby.Members.Count, lobby.Capacity, new Dictionary<string, string>(lobby.Data, StringComparer.OrdinalIgnoreCase)));
                    }
                }

                List<MachineDirectoryEntry> entries = MachineDirectory.Read(_network.MachineFolder);
                for (int i = 0; i < entries.Count && results.Count < snapshot.MaxResults; i++)
                {
                    MachineDirectoryEntry entry = entries[i];
                    if (entry.Port != Port
                        && IsSearchable(entry.Visibility, entry.IsJoinable, entry.MemberCount, entry.Capacity)
                        && LobbyFilter.Matches(entry.Data, snapshot))
                    {
                        results.Add(new BackendLobby(entry.Lobby, entry.MemberCount, entry.Capacity, entry.Data));
                    }
                }

                onCompleted?.Invoke(MultiplayerReason.None, results);
            });
        }

        public void JoinLobby(LobbyId lobbyId, Action<MultiplayerReason> onCompleted)
        {
            Defer(() => StartJoin(lobbyId, onCompleted));
        }

        public void LeaveLobby(LobbyId lobbyId)
        {
            if (_isDisposed || !_lobbies.TryGetValue(lobbyId, out MachineLobby lobby))
            {
                return;
            }

            _lobbies.Remove(lobbyId);
            if (lobby.IsOwned)
            {
                HandOver(lobby);
                _isDirectoryDirty = true;
                return;
            }

            if (lobby.OwnerLink != null)
            {
                CloseLink(lobby.OwnerLink, sendClose: true, 0);
            }

            if (lobby.RejoinLink != null)
            {
                CloseLink(lobby.RejoinLink, sendClose: false, 0);
            }
        }

        public string GetLobbyData(LobbyId lobbyId, string key)
        {
            return _lobbies.TryGetValue(lobbyId, out MachineLobby lobby) && key != null && lobby.Data.TryGetValue(key, out string value) ? value : null;
        }

        public bool SetLobbyData(LobbyId lobbyId, string key, string value)
        {
            MachineLobby lobby = FindOwnedLobby(lobbyId);
            if (lobby == null || string.IsNullOrEmpty(key))
            {
                return false;
            }

            lobby.Data[key] = value ?? string.Empty;
            lobby.IsDirty = true;
            return true;
        }

        public bool DeleteLobbyData(LobbyId lobbyId, string key)
        {
            MachineLobby lobby = FindOwnedLobby(lobbyId);
            if (lobby == null || string.IsNullOrEmpty(key))
            {
                return false;
            }

            // The members and the lobby file take the whole data again, so the key leaves both.
            if (lobby.Data.Remove(key))
            {
                lobby.IsDirty = true;
            }

            return true;
        }

        public bool SetLobbyJoinable(LobbyId lobbyId, bool joinable)
        {
            MachineLobby lobby = FindOwnedLobby(lobbyId);
            if (lobby == null)
            {
                return false;
            }

            lobby.IsJoinable = joinable;
            lobby.IsDirty = true;
            return true;
        }

        public bool SetLobbyCapacity(LobbyId lobbyId, int capacity)
        {
            MachineLobby lobby = FindOwnedLobby(lobbyId);
            if (lobby == null || capacity < 1)
            {
                return false;
            }

            lobby.Capacity = capacity;
            lobby.IsDirty = true;
            return true;
        }

        public PlayerId GetLobbyOwner(LobbyId lobbyId)
        {
            return _lobbies.TryGetValue(lobbyId, out MachineLobby lobby) ? lobby.OwnerId : PlayerId.None;
        }

        public bool IsLobbyMember(LobbyId lobbyId, PlayerId player)
        {
            return _lobbies.TryGetValue(lobbyId, out MachineLobby lobby) && lobby.HasMember(player);
        }

        private MachineLobby FindOwnedLobby(LobbyId lobbyId)
        {
            return !_isDisposed && _lobbies.TryGetValue(lobbyId, out MachineLobby lobby) && lobby.IsOwned ? lobby : null;
        }

        private static bool IsSearchable(LobbyVisibility visibility, bool isJoinable, int memberCount, int capacity)
        {
            return visibility == LobbyVisibility.Public && isJoinable && memberCount < capacity;
        }

        #region Joining
        private void StartJoin(LobbyId lobbyId, Action<MultiplayerReason> onCompleted)
        {
            if (_lobbies.ContainsKey(lobbyId))
            {
                onCompleted?.Invoke(MultiplayerReason.None);
                return;
            }

            if (_joins.TryGetValue(lobbyId, out MachineLink pending))
            {
                pending.JoinCallbacks.Add(onCompleted);
                return;
            }

            MachineDirectoryEntry entry = null;
            List<MachineDirectoryEntry> entries = MachineDirectory.Read(_network.MachineFolder);
            for (int i = 0; i < entries.Count && entry == null; i++)
            {
                if (entries[i].Lobby == lobbyId)
                {
                    entry = entries[i];
                }
            }

            if (entry == null || entry.Port == Port)
            {
                onCompleted?.Invoke(MultiplayerReason.LobbyNotFound);
                return;
            }

            MachineLink link = StartJoinLink(lobbyId, entry.Owner, entry.Port, isRejoin: false);
            link.JoinCallbacks = new List<Action<MultiplayerReason>> { onCompleted };
            _joins[lobbyId] = link;
        }

        private MachineLink StartJoinLink(LobbyId lobbyId, PlayerId owner, int port, bool isRejoin)
        {
            MachineLink link = StartLink(MachineLinkRole.Joining, port);
            link.Lobby = lobbyId;
            link.IsRejoin = isRejoin;
            link.Remote = owner;
            link.RemotePort = port;

            _writer.Clear();
            _writer.WriteInt(MachineProtocol.Magic);
            _writer.WriteInt(MachineProtocol.Version);
            _writer.WriteULong(lobbyId.Value);
            _writer.WriteULong(LocalPlayerId.Value);
            _writer.WriteInt(Port);
            _writer.WriteString(PlayerName);
            _writer.WriteBool(isRejoin);
            QueueFrame(link, MachineFrameKind.LobbyJoin, _writer.Written, force: true);
            return link;
        }

        /// <summary>The first frame of an incoming connection asks to enter a lobby this player owns.</summary>
        private void AcceptJoin(MachineLink link, ReadOnlySpan<byte> payload)
        {
            LobbyId lobbyId;
            PlayerId player;
            int port;
            string name;
            bool isRejoin;
            try
            {
                PacketReader reader = new PacketReader(payload);
                ReadVersion(ref reader);
                lobbyId = new LobbyId(reader.ReadULong());
                player = new PlayerId(reader.ReadULong());
                port = reader.ReadInt();
                name = reader.ReadString();
                isRejoin = reader.ReadBool();
            }
            catch (FormatException)
            {
                EndLink(link, LinkEnd.Lost, 0);
                return;
            }

            int code = (int)MultiplayerReason.None;
            MachineMember member = null;
            if (!_lobbies.TryGetValue(lobbyId, out MachineLobby lobby))
            {
                code = (int)MultiplayerReason.LobbyNotFound;
            }
            else if (!lobby.IsOwned)
            {
                // A member that reached this player as the next owner before this player noticed
                // the owner leave asks again in a moment.
                code = isRejoin ? MachineProtocol.NotYet : (int)MultiplayerReason.LobbyNotFound;
            }
            else
            {
                member = lobby.FindMember(player, port);
                if (member == null)
                {
                    if (!lobby.IsJoinable)
                    {
                        code = (int)MultiplayerReason.GameInProgress;
                    }
                    else if (!_network.IgnoreLobbyCapacity && lobby.Members.Count >= lobby.Capacity)
                    {
                        code = (int)MultiplayerReason.SessionFull;
                    }
                    else
                    {
                        member = new MachineMember { Id = player, Port = port, Name = name };
                        lobby.Members.Add(member);
                    }
                }
            }

            if (member == null)
            {
                _writer.Clear().WriteInt(code);
                QueueFrame(link, MachineFrameKind.LobbyReply, _writer.Written, force: true);
                CloseLink(link, sendClose: false, 0);
                return;
            }

            // A member that comes back, after the lobby passed to this player or over a new
            // connection, replaces its old connection without leaving.
            if (member.Link != null && member.Link != link)
            {
                MachineLink old = member.Link;
                old.Role = MachineLinkRole.Unknown;
                CloseLink(old, sendClose: false, 0);
            }

            member.Link = link;
            member.Name = name;
            member.RejoinDeadline = 0.0;
            link.Role = MachineLinkRole.OwnerToMember;
            link.Lobby = lobbyId;
            link.Remote = player;
            link.RemoteName = name;
            link.Deadline = 0.0;

            _writer.Clear().WriteInt(code);
            MachineProtocol.WriteSnapshot(_writer, lobby);
            QueueFrame(link, MachineFrameKind.LobbyReply, _writer.Written, force: true);
            Flush(link);
            lobby.IsDirty = true;
        }

        private void HandleJoinReply(MachineLink link, MachineFrameKind kind, ReadOnlySpan<byte> payload)
        {
            int code;
            MachineLobby snapshot = null;
            try
            {
                if (kind != MachineFrameKind.LobbyReply)
                {
                    throw new FormatException($"Expected a lobby reply, not {kind}.");
                }

                PacketReader reader = new PacketReader(payload);
                code = reader.ReadInt();
                if (code == (int)MultiplayerReason.None)
                {
                    snapshot = MachineProtocol.ReadSnapshot(ref reader);
                    if (snapshot.Id != link.Lobby)
                    {
                        throw new FormatException("The reply is about another lobby.");
                    }
                }
            }
            catch (FormatException)
            {
                EndLink(link, LinkEnd.Lost, 0);
                return;
            }

            if (snapshot == null)
            {
                CloseLink(link, sendClose: false, 0);
                if (link.IsRejoin)
                {
                    RejoinRefused(link, code);
                }
                else
                {
                    // A first join never hears NotYet from an owner; anything else names the reason.
                    CompleteJoin(link, code > 0 ? (MultiplayerReason)code : MultiplayerReason.LobbyNotFound);
                }

                return;
            }

            link.Role = MachineLinkRole.MemberToOwner;
            link.Deadline = 0.0;

            if (link.IsRejoin)
            {
                if (!_lobbies.TryGetValue(link.Lobby, out MachineLobby lobby) || lobby.RejoinLink != link)
                {
                    CloseLink(link, sendClose: true, 0);
                    return;
                }

                lobby.RejoinLink = null;
                lobby.OwnerLink = link;
                lobby.Apply(snapshot);
                return;
            }

            if (_lobbies.TryGetValue(snapshot.Id, out MachineLobby previous) && previous.OwnerLink != null)
            {
                CloseLink(previous.OwnerLink, sendClose: true, 0);
            }

            snapshot.OwnerLink = link;
            _lobbies[snapshot.Id] = snapshot;
            CompleteJoin(link, MultiplayerReason.None);
        }

        private void CompleteJoin(MachineLink link, MultiplayerReason reason)
        {
            if (_joins.TryGetValue(link.Lobby, out MachineLink pending) && pending == link)
            {
                _joins.Remove(link.Lobby);
            }

            List<Action<MultiplayerReason>> callbacks = link.JoinCallbacks;
            link.JoinCallbacks = null;
            if (callbacks == null)
            {
                return;
            }

            for (int i = 0; i < callbacks.Count; i++)
            {
                callbacks[i]?.Invoke(reason);
            }
        }

        private void EndJoinLink(MachineLink link, LinkEnd end)
        {
            // Nothing listens on the port the lobby file names: that process ended without
            // deleting its file, and the lobby leaves search now rather than once the file goes stale.
            if (end == LinkEnd.ConnectFailed)
            {
                MachineDirectory.RemoveEnded(_network.MachineFolder, link.RemotePort, link.Remote);
            }

            if (!link.IsRejoin)
            {
                CompleteJoin(link, end == LinkEnd.TimedOut ? MultiplayerReason.Timeout : MultiplayerReason.LobbyNotFound);
                return;
            }

            // The player this one took for the next owner is gone or stalled as well.
            if (_lobbies.TryGetValue(link.Lobby, out MachineLobby lobby) && lobby.RejoinLink == link)
            {
                lobby.RejoinLink = null;
                OwnerGone(lobby, PlayerId.None, 0);
            }
        }

        private void RejoinRefused(MachineLink link, int code)
        {
            if (!_lobbies.TryGetValue(link.Lobby, out MachineLobby lobby) || lobby.RejoinLink != link)
            {
                return;
            }

            lobby.RejoinLink = null;
            if (code == MachineProtocol.NotYet)
            {
                lobby.RejoinAt = Now + RejoinRetrySeconds;
            }
            else if (code == (int)MultiplayerReason.LobbyNotFound)
            {
                // That player left the lobby as well; it passes on once more.
                OwnerGone(lobby, PlayerId.None, 0);
            }
            else
            {
                // The new owner does not count this player as a member.
                _lobbies.Remove(lobby.Id);
            }
        }
        #endregion

        #region Ownership
        private void HandleOwnerFrame(MachineLink link, MachineFrameKind kind, ReadOnlySpan<byte> payload)
        {
            if (!_lobbies.TryGetValue(link.Lobby, out MachineLobby lobby) || lobby.OwnerLink != link)
            {
                // A connection to an owner this player no longer follows.
                CloseLink(link, sendClose: false, 0);
                return;
            }

            switch (kind)
            {
                case MachineFrameKind.LobbyState:
                    MachineLobby snapshot;
                    try
                    {
                        PacketReader reader = new PacketReader(payload);
                        snapshot = MachineProtocol.ReadSnapshot(ref reader);
                    }
                    catch (FormatException)
                    {
                        EndLink(link, LinkEnd.Lost, 0);
                        return;
                    }

                    if (snapshot.Id == lobby.Id)
                    {
                        lobby.Apply(snapshot);
                    }
                    break;

                case MachineFrameKind.LobbyHandover:
                    PlayerId next;
                    int nextPort;
                    try
                    {
                        PacketReader reader = new PacketReader(payload);
                        next = new PlayerId(reader.ReadULong());
                        nextPort = reader.ReadInt();
                    }
                    catch (FormatException)
                    {
                        EndLink(link, LinkEnd.Lost, 0);
                        return;
                    }

                    lobby.OwnerLink = null;
                    CloseLink(link, sendClose: false, 0);
                    OwnerGone(lobby, next, nextPort);
                    break;

                case MachineFrameKind.Close:
                    // An owner closes a member's connection only when it no longer counts the
                    // member, as after the member stalled past the timeout: the lobby goes on
                    // without this player, and its owner did not leave.
                    _lobbies.Remove(lobby.Id);
                    EndLink(link, LinkEnd.Closed, 0);
                    break;

                default:
                    EndLink(link, LinkEnd.Lost, 0);
                    break;
            }
        }

        private void EndOwnerLink(MachineLink link)
        {
            if (_lobbies.TryGetValue(link.Lobby, out MachineLobby lobby) && lobby.OwnerLink == link)
            {
                // Without a hand-over or a close, the owner's process ended or stalled. Its lobby
                // file would list the lobby until it went stale, so it goes now; an owner that
                // only stalled writes it again when it runs.
                MachineDirectory.RemoveEnded(_network.MachineFolder, lobby.OwnerPort, lobby.OwnerId);
                lobby.OwnerLink = null;
                OwnerGone(lobby, PlayerId.None, 0);
            }
        }

        private void EndMemberLink(MachineLink link)
        {
            if (!_lobbies.TryGetValue(link.Lobby, out MachineLobby lobby) || !lobby.IsOwned)
            {
                return;
            }

            for (int i = 0; i < lobby.Members.Count; i++)
            {
                if (lobby.Members[i].Link == link)
                {
                    lobby.Members.RemoveAt(i);
                    lobby.IsDirty = true;
                    return;
                }
            }
        }

        /// <summary>
        /// The owner left or its process ended: the lobby passes to the member it named, or else
        /// to the first remaining member, as a Steam lobby does. Every member works out the same
        /// next owner from its copy, and the others reconnect to it.
        /// </summary>
        private void OwnerGone(MachineLobby lobby, PlayerId next, int nextPort)
        {
            int ownerIndex = lobby.IndexOfMember(lobby.OwnerId, lobby.OwnerPort);
            if (ownerIndex >= 0)
            {
                lobby.Members.RemoveAt(ownerIndex);
            }

            MachineMember owner = next.IsValid ? lobby.FindMember(next, nextPort) : null;
            if (owner == null && lobby.Members.Count > 0)
            {
                owner = lobby.Members[0];
            }

            if (owner == null || lobby.FindMember(LocalPlayerId, Port) == null)
            {
                _lobbies.Remove(lobby.Id);
                return;
            }

            lobby.OwnerId = owner.Id;
            lobby.OwnerPort = owner.Port;

            double now = Now;
            if (owner.Port == Port)
            {
                TakeOver(lobby, now);
            }
            else
            {
                lobby.RejoinAt = now;
                lobby.RejoinDeadline = now + RejoinGraceSeconds;
            }

            // Heard in the next update. An owner that leaves sends what its session connection
            // still carries, its close included, before its hand-over on the lobby connection,
            // and this update may read that session connection only after this one. What the
            // sockets between them still hold, or what the owner's lingering socket sends later,
            // can still arrive after the news, so a client's manager waits a moment for the
            // session connection's close before it ends the session.
            LobbyId lobbyId = lobby.Id;
            PlayerId ownerId = owner.Id;
            Defer(() => _listener?.OnLobbyOwnerChanged(lobbyId, ownerId));
        }

        private void TakeOver(MachineLobby lobby, double now)
        {
            lobby.IsOwned = true;
            lobby.OwnerLink = null;
            if (lobby.RejoinLink != null)
            {
                CloseLink(lobby.RejoinLink, sendClose: false, 0);
                lobby.RejoinLink = null;
            }

            for (int i = 0; i < lobby.Members.Count; i++)
            {
                MachineMember member = lobby.Members[i];
                if (member.Port != Port)
                {
                    member.Link = null;
                    member.RejoinDeadline = now + RejoinGraceSeconds;
                }
            }

            lobby.IsDirty = true;
        }

        /// <summary>The owner leaves: the lobby passes to the first other member, and every member hears which.</summary>
        private void HandOver(MachineLobby lobby)
        {
            MachineMember next = null;
            for (int i = 0; i < lobby.Members.Count && next == null; i++)
            {
                if (lobby.Members[i].Port != Port)
                {
                    next = lobby.Members[i];
                }
            }

            if (next == null)
            {
                return;
            }

            _writer.Clear();
            _writer.WriteULong(next.Id.Value);
            _writer.WriteInt(next.Port);
            for (int i = 0; i < lobby.Members.Count; i++)
            {
                MachineLink link = lobby.Members[i].Link;
                if (link != null && link.State == MachineLinkState.Open)
                {
                    QueueFrame(link, MachineFrameKind.LobbyHandover, _writer.Written, force: true);
                    CloseLink(link, sendClose: false, 0);
                }
            }
        }

        private void HandOverOwnedLobbies()
        {
            _lobbyScratch.Clear();
            foreach (MachineLobby lobby in _lobbies.Values)
            {
                if (lobby.IsOwned)
                {
                    _lobbyScratch.Add(lobby);
                }
            }

            for (int i = 0; i < _lobbyScratch.Count; i++)
            {
                _lobbies.Remove(_lobbyScratch[i].Id);
                HandOver(_lobbyScratch[i]);
            }
        }
        #endregion

        #region Update
        private void UpdateLobbies(double now)
        {
            _lobbyScratch.Clear();
            _lobbyScratch.AddRange(_lobbies.Values);
            for (int i = 0; i < _lobbyScratch.Count && !_isDisposed; i++)
            {
                MachineLobby lobby = _lobbyScratch[i];

                // A callback may have left it in the meantime.
                if (!_lobbies.TryGetValue(lobby.Id, out MachineLobby current) || current != lobby)
                {
                    continue;
                }

                if (lobby.IsOwned)
                {
                    UpdateOwnedLobby(lobby, now);
                }
                else
                {
                    UpdateFollowedLobby(lobby, now);
                }
            }

            if (!_isDisposed)
            {
                PublishDirectory(now);
            }
        }

        private void UpdateOwnedLobby(MachineLobby lobby, double now)
        {
            for (int i = lobby.Members.Count - 1; i >= 0; i--)
            {
                MachineMember member = lobby.Members[i];
                if (member.RejoinDeadline > 0.0 && now >= member.RejoinDeadline)
                {
                    lobby.Members.RemoveAt(i);
                    lobby.IsDirty = true;
                }
            }

            if (!lobby.IsDirty)
            {
                return;
            }

            lobby.IsDirty = false;
            _isDirectoryDirty = true;

            _writer.Clear();
            MachineProtocol.WriteSnapshot(_writer, lobby);
            for (int i = 0; i < lobby.Members.Count; i++)
            {
                MachineLink link = lobby.Members[i].Link;
                if (link != null && link.State == MachineLinkState.Open)
                {
                    QueueFrame(link, MachineFrameKind.LobbyState, _writer.Written, force: true);
                    Flush(link);
                }
            }
        }

        /// <summary>A lobby whose owner left: reconnect to the next owner while there is time.</summary>
        private void UpdateFollowedLobby(MachineLobby lobby, double now)
        {
            if (lobby.OwnerLink != null || lobby.RejoinLink != null)
            {
                return;
            }

            if (now >= lobby.RejoinDeadline)
            {
                _lobbies.Remove(lobby.Id);
                return;
            }

            if (now >= lobby.RejoinAt)
            {
                lobby.RejoinLink = StartJoinLink(lobby.Id, lobby.OwnerId, lobby.OwnerPort, isRejoin: true);
            }
        }

        private void PublishDirectory(double now)
        {
            _lobbyScratch.Clear();
            foreach (MachineLobby lobby in _lobbies.Values)
            {
                if (lobby.IsOwned)
                {
                    _lobbyScratch.Add(lobby);
                }
            }

            bool isDue = _isDirectoryDirty || (_lobbyScratch.Count > 0 && now >= _nextPublish);
            if (!isDue || now < _nextPublishAttempt)
            {
                return;
            }

            if (_directory.Publish(LocalPlayerId, Port, _lobbyScratch))
            {
                _isDirectoryDirty = false;
                _nextPublish = now + PublishIntervalSeconds;
                _nextPublishAttempt = now + PublishMinIntervalSeconds;
            }
            else
            {
                _nextPublishAttempt = now + PublishRetrySeconds;
            }
        }
        #endregion
    }
}
