using System;
using System.Collections.Generic;
using System.Threading;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using Debug = UnityEngine.Debug;

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

        // The start of the warning for a join that would not fit a frame (#352), and of the one
        // for a lobby or a change that would not fit a frame or the lobby file (#346).
        private static readonly string FrameSizeWarning =
            $"[Multiplayer] A lobby on the local network takes at most {MachineProtocol.MaxSnapshotBytes} bytes in the frames that bring it to its members";
        private static readonly string SizeWarning =
            FrameSizeWarning + $", and a backend's lobbies at most {MachineDirectory.MaxFileBytes} bytes in its lobby file; ";

        private readonly Dictionary<LobbyId, MachineLobby> _lobbies = new Dictionary<LobbyId, MachineLobby>();

        // Joins waiting for the owner's answer; a second join of the same lobby waits with the first.
        private readonly Dictionary<LobbyId, MachineLink> _joins = new Dictionary<LobbyId, MachineLink>();

        // The last lobby whose owner refused a first join because its game runs, with the data the
        // folder listed for it, which this player reads though it is no member, as Steam lets it;
        // the owner's port joins the ports known, so a session connection reaches the owner.
        private LobbyId _refusedLobby = LobbyId.None;
        private IReadOnlyDictionary<string, string> _refusedLobbyData = null;

        private readonly List<MachineLobby> _lobbyScratch = new List<MachineLobby>();

        // The MachineLobby.OwnedOrder of the lobby this player came to own last.
        private long _lastOwnedOrder = 0;

        private bool _isDirectoryDirty = false;
        private double _nextPublish = 0.0;
        private double _nextPublishAttempt = 0.0;

        /// <summary>
        /// A read of the lobby files on a thread of the pool, which the main thread goes on with
        /// once it is done.
        /// </summary>
        private sealed class DirectoryRead
        {
            public static readonly WaitCallback Run = state => ((DirectoryRead)state).ReadFolder();

            private readonly string _folder;
            private List<MachineDirectoryEntry> _entries = null;
            private int _isDone = 0;

            public DirectoryRead(string folder)
            {
                _folder = folder;
            }

            public bool IsDone => Volatile.Read(ref _isDone) != 0;

            /// <summary>What the folder listed, once <see cref="IsDone"/>.</summary>
            public List<MachineDirectoryEntry> Entries => _entries;

            private void ReadFolder()
            {
                try
                {
                    _entries = MachineDirectory.Read(_folder);
                }
                catch (Exception e) when (!(e is ThreadAbortException))
                {
                    // Nothing on a thread of the pool catches it, and the search or join still answers.
                    Debug.LogException(e);
                    _entries = new List<MachineDirectoryEntry>();
                }

                Volatile.Write(ref _isDone, 1);
            }
        }

        public void CreateLobby(LobbyVisibility visibility, int capacity, IReadOnlyDictionary<string, string> data, Action<MultiplayerReason, LobbyId> onCompleted)
        {
            // Keys in any case, as the lobby keeps them, so it counts the entries the lobby gets.
            // Each entry is written as SetLobbyData writes it, so an empty value writes no key the
            // lobby lacks, as a Steam lobby writes none (#343), and its key and value are taken as
            // Steamworks.NET hands them to Steam, and UTF-8 to the other processes, so this
            // process reads them as they do (#372, #387); the limits count them so taken, as
            // Steam's count what reaches Steam.
            Dictionary<string, string> snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool isWithinLimits = true;
            if (data != null)
            {
                foreach (KeyValuePair<string, string> pair in data)
                {
                    string key = Utf8Text.AsLobbyText(pair.Key);
                    string value = Utf8Text.AsLobbyText(pair.Value);
                    if (key.Length > 0 && (!string.IsNullOrEmpty(value) || snapshot.ContainsKey(key)))
                    {
                        snapshot[key] = value ?? string.Empty;
                    }

                    isWithinLimits &= MachineProtocol.IsWithinLimits(key, value);
                }
            }

            Defer(() =>
            {
                // The other processes would drop the lobby as malformed, so it fails here, as a
                // Steam lobby fails when Steam refuses one of its keys.
                if (!isWithinLimits)
                {
                    Debug.LogWarning($"[Multiplayer] The local network carries lobby data keys of at most {MachineProtocol.MaxDataKeyBytes} and values of at most {MachineProtocol.MaxDataValueBytes} UTF-8 bytes; the lobby was not created.");
                    onCompleted?.Invoke(MultiplayerReason.BackendError, LobbyId.None);
                    return;
                }

                // A lobby file lists no more data entries for a lobby, nor more lobbies, so the
                // other processes would not find the lobby; it fails here as well (#309).
                if (snapshot.Count > MachineDirectory.MaxLobbyDataEntries)
                {
                    Debug.LogWarning($"[Multiplayer] A lobby on the local network holds at most {MachineDirectory.MaxLobbyDataEntries} data entries; the lobby was not created.");
                    onCompleted?.Invoke(MultiplayerReason.BackendError, LobbyId.None);
                    return;
                }

                // InvalidState, as the manager answers a creation while it has a session (#435):
                // only leaving a lobby clears this limit, never waiting, while RateLimited, which
                // Steam answers to its creation limit, promises that creating the lobby again
                // later can succeed.
                if (CountOwnedLobbies() >= MachineDirectory.MaxListedLobbies)
                {
                    Debug.LogWarning($"[Multiplayer] A backend on the local network owns at most {MachineDirectory.MaxListedLobbies} lobbies at a time; the lobby was not created.");
                    onCompleted?.Invoke(MultiplayerReason.InvalidState, LobbyId.None);
                    return;
                }

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
                lobby.Members.Add(new MachineMember { Id = LocalPlayerId, Port = Port, Name = _wireName });
                foreach (KeyValuePair<string, string> pair in snapshot)
                {
                    lobby.Data[pair.Key] = pair.Value;
                }

                // Its members would drop the frames that bring a lobby too large for them, and the
                // lobby file leaves out what does not fit beside this player's other lobbies, so
                // the other processes would not find it either; it fails here as well (#346).
                if (MachineProtocol.SnapshotBytes(lobby) > MachineProtocol.MaxSnapshotBytes
                    || OwnedListingBytes() + MachineDirectory.ListedBytes(lobby) > MachineDirectory.MaxFileBytes)
                {
                    Debug.LogWarning(SizeWarning + "the lobby was not created.");
                    onCompleted?.Invoke(MultiplayerReason.BackendError, LobbyId.None);
                    return;
                }

                lobby.OwnedOrder = ++_lastOwnedOrder;
                _lobbies.Add(lobby.Id, lobby);
                onCompleted?.Invoke(MultiplayerReason.None, lobby.Id);
            });
        }

        public void SearchLobbies(LobbySearchQuery query, Action<MultiplayerReason, IReadOnlyList<BackendLobby>> onCompleted)
        {
            LobbySearchQuery snapshot = query != null ? query.Clone() : new LobbySearchQuery();

            ReadDirectory(entries =>
            {
                List<BackendLobby> results = new List<BackendLobby>();

                // This player's own lobbies as they are now, the others as their owners last listed
                // them, each with the capacity its lobby file lists (#368).
                foreach (MachineLobby lobby in _lobbies.Values)
                {
                    if (results.Count >= snapshot.MaxResults || !lobby.IsOwned)
                    {
                        continue;
                    }

                    int capacity = MachineDirectory.ListedCapacity(lobby);
                    if (IsSearchable(lobby.Visibility, lobby.IsJoinable, lobby.Members.Count, capacity)
                        && LobbyFilter.Matches(lobby.Data, snapshot))
                    {
                        results.Add(new BackendLobby(lobby.Id, lobby.Members.Count, capacity, new Dictionary<string, string>(lobby.Data, StringComparer.OrdinalIgnoreCase)));
                    }
                }

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
            ReadDirectory(entries => StartJoin(lobbyId, onCompleted, entries));
        }

        /// <summary>
        /// Reads the lobby files on a thread of the pool, and goes on with what they list in the
        /// next update, or in a later one when the read is not done by then. A read waits a moment
        /// for a file whose owner is replacing it (<see cref="MachineDirectory.Read"/>), which the
        /// main thread should not, and opening the files takes milliseconds of its own.
        /// </summary>
        /// <remarks>
        /// On an AMD Ryzen 7 5800X3D with Windows 11 Pro and Unity 6000.6.7f1, a search that read
        /// the files in its update held that update for 4.7 ms with the owner's file in place,
        /// 21 ms when it met a replacement that finished 10 ms later, and 59 ms when it met what a
        /// process that ended two seconds earlier had left; the slowest update of a search that
        /// reads on a thread of the pool takes about 0.02 ms in each case (medians of 30 searches).
        /// </remarks>
        private void ReadDirectory(Action<List<MachineDirectoryEntry>> then)
        {
            if (_isDisposed)
            {
                return;
            }

            DirectoryRead read = new DirectoryRead(_network.MachineFolder);
            ThreadPool.QueueUserWorkItem(DirectoryRead.Run, read);
            Defer(() => AfterRead(read, then));
        }

        private void AfterRead(DirectoryRead read, Action<List<MachineDirectoryEntry>> then)
        {
            if (read.IsDone)
            {
                then(read.Entries);
            }
            else
            {
                Defer(() => AfterRead(read, then));
            }
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
            IReadOnlyDictionary<string, string> data = _lobbies.TryGetValue(lobbyId, out MachineLobby lobby)
                ? lobby.Data
                : lobbyId == _refusedLobby ? _refusedLobbyData : null;

            // An empty value reads as none, as the Steam backend reads it (#343).
            return data != null && key != null && data.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value) ? value : null;
        }

        public bool SetLobbyData(LobbyId lobbyId, string key, string value)
        {
            MachineLobby lobby = FindOwnedLobby(lobbyId);
            if (lobby == null || string.IsNullOrEmpty(key))
            {
                return false;
            }

            // The other processes would drop the lobby as malformed, so the entry is refused, as
            // Steam refuses a key or a value too long for it, with the warning CreateLobby gives (#345).
            if (!MachineProtocol.IsWithinLimits(key, value))
            {
                Debug.LogWarning($"[Multiplayer] The local network carries lobby data keys of at most {MachineProtocol.MaxDataKeyBytes} and values of at most {MachineProtocol.MaxDataValueBytes} UTF-8 bytes; key '{key}' was not set on lobby {lobbyId}.");
                return false;
            }

            // An empty value writes no key the lobby lacks and answers true, and leaves a key it
            // holds with the empty value, as a Steam lobby did (#343).
            bool holds = lobby.Data.TryGetValue(key, out string current);
            if (string.IsNullOrEmpty(value) && !holds)
            {
                return true;
            }

            // Past the entries a lobby file lists and a frame carries, the lobby would leave search
            // and joins by its ID, and its members would drop the frames that bring it (#309).
            if (lobby.Data.Count >= MachineDirectory.MaxLobbyDataEntries && !holds)
            {
                Debug.LogWarning($"[Multiplayer] A lobby on the local network holds at most {MachineDirectory.MaxLobbyDataEntries} data entries; key '{key}' was not set on lobby {lobbyId}.");
                return false;
            }

            // Past the bytes a frame carries or the lobby file holds, the same (#346). A change
            // that does not grow the lobby goes through, so one taken over past them can shrink.
            // A key the lobby holds keeps the spelling it has, so its own bytes cancel out.
            int frameGrowth = MachineProtocol.DataEntryBytes(key, value) - (holds ? MachineProtocol.DataEntryBytes(key, current) : 0);
            int fileGrowth = MachineDirectory.ListedEntryBytes(key, value) - (holds ? MachineDirectory.ListedEntryBytes(key, current) : 0);
            if ((frameGrowth > 0 && MachineProtocol.SnapshotBytes(lobby) + frameGrowth > MachineProtocol.MaxSnapshotBytes)
                || (fileGrowth > 0 && OwnedListingBytes() + fileGrowth > MachineDirectory.MaxFileBytes))
            {
                Debug.LogWarning(SizeWarning + $"key '{key}' was not set on lobby {lobbyId}.");
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

            // False for a key the lobby lacks, as Steam answers (#343).
            if (!lobby.Data.Remove(key))
            {
                return false;
            }

            // The members and the lobby file take the whole data again, so the key leaves both.
            lobby.IsDirty = true;
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

        /// <summary>Whether this peer owns the lobby, so it may write the lobby's data.</summary>
        public bool IsOwnedLobby(LobbyId lobbyId) => FindOwnedLobby(lobbyId) != null;

        private MachineLobby FindOwnedLobby(LobbyId lobbyId)
        {
            return !_isDisposed && _lobbies.TryGetValue(lobbyId, out MachineLobby lobby) && lobby.IsOwned ? lobby : null;
        }

        private int CountOwnedLobbies()
        {
            int count = 0;
            foreach (MachineLobby lobby in _lobbies.Values)
            {
                if (lobby.IsOwned)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// The bytes of this player's lobby file with every lobby it owns listed, also those that
        /// taking lobbies over brings past <see cref="MachineDirectory.MaxListedLobbies"/>.
        /// </summary>
        private int OwnedListingBytes()
        {
            int bytes = MachineDirectory.ListingFixedBytes;
            foreach (MachineLobby lobby in _lobbies.Values)
            {
                if (lobby.IsOwned)
                {
                    bytes += MachineDirectory.ListedBytes(lobby);
                }
            }

            return bytes;
        }

        private static bool IsSearchable(LobbyVisibility visibility, bool isJoinable, int memberCount, int capacity)
        {
            return visibility == LobbyVisibility.Public && isJoinable && memberCount < capacity;
        }

        #region Joining
        private void StartJoin(LobbyId lobbyId, Action<MultiplayerReason> onCompleted, List<MachineDirectoryEntry> entries)
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
            link.ListedData = entry.Data;
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
            _writer.WriteString(_wireName);
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
                name = MachineProtocol.ReadName(ref reader);
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
                    else if (MachineProtocol.SnapshotBytes(lobby) + MachineProtocol.MemberBytes(name) > MachineProtocol.MaxSnapshotBytes)
                    {
                        // The answer to this join, the largest frame there is, would carry the
                        // lobby past what a frame carries, so the player would drop it; a few bytes
                        // more, and every member would drop the next frame that brings the lobby,
                        // losing its link to this player (#352). The join is refused as CreateLobby
                        // and SetLobbyData refuse such a lobby (#346), with the reason a full lobby
                        // gives, since the lobby has no room left for this player: its lobby file
                        // lists it as full by now (MachineDirectory.ListedCapacity), so only a join
                        // by its ID or one that read an older file comes here (#368). The lobby
                        // file lists no members, so a join never grows it.
                        Debug.LogWarning(FrameSizeWarning + $"; player {player} was not let into lobby {lobbyId}, which it would take past them.");
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
                    MultiplayerReason reason = code > 0 ? (MultiplayerReason)code : MultiplayerReason.LobbyNotFound;
                    if (reason == MultiplayerReason.GameInProgress)
                    {
                        // A player whose slot the owner keeps reaches it past the lobby (FR-21).
                        _refusedLobby = link.Lobby;
                        _refusedLobbyData = link.ListedData;
                        _knownPorts[link.Remote] = link.RemotePort;
                    }

                    CompleteJoin(link, reason);
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

        /// <summary>
        /// This player becomes the owner. It takes the lobby over also when it already owns as
        /// many lobbies as a lobby file lists, since refusing would end the lobby for its members;
        /// the file then leaves out the lobby taken over last (<see cref="PublishDirectory"/>).
        /// The lobby brings no more data entries than a file lists, as many as a frame carries.
        /// </summary>
        private void TakeOver(MachineLobby lobby, double now)
        {
            lobby.IsOwned = true;
            lobby.OwnedOrder = ++_lastOwnedOrder;
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
            // The last listing could not be written, for example while another program held the file.
            if (_directory.TakeFailedWrite())
            {
                _isDirectoryDirty = true;
                _nextPublishAttempt = now + PublishRetrySeconds;
            }

            _lobbyScratch.Clear();
            foreach (MachineLobby lobby in _lobbies.Values)
            {
                if (lobby.IsOwned)
                {
                    _lobbyScratch.Add(lobby);
                }
            }

            // A lobby created or taken over is listed at once, so a process that learns its ID can
            // join it; any other change waits out the shortest interval.
            bool addsLobby = _directory.AddsLobby(_lobbyScratch);
            bool isDue = addsLobby || _isDirectoryDirty || (_lobbyScratch.Count > 0 && now >= _nextPublish);
            if (!isDue || (now < _nextPublishAttempt && !addsLobby))
            {
                return;
            }

            // Past the lobbies a file lists, which only a lobby taken over brings, the file lists
            // those owned longest and leaves out the newest, so a lobby in search stays there.
            if (_lobbyScratch.Count > MachineDirectory.MaxListedLobbies)
            {
                SortByOwnedOrder(_lobbyScratch);
            }

            // Unless it adds a lobby, the file is written in the background, so this frame does not wait for it.
            _directory.Publish(LocalPlayerId, Port, _lobbyScratch);
            _isDirectoryDirty = false;
            _nextPublish = now + PublishIntervalSeconds;
            _nextPublishAttempt = now + PublishMinIntervalSeconds;
        }

        /// <summary>An insertion sort, which allocates nothing, by <see cref="MachineLobby.OwnedOrder"/>.</summary>
        private static void SortByOwnedOrder(List<MachineLobby> lobbies)
        {
            for (int i = 1; i < lobbies.Count; i++)
            {
                MachineLobby lobby = lobbies[i];
                int j = i - 1;
                for (; j >= 0 && lobbies[j].OwnedOrder > lobby.OwnedOrder; j--)
                {
                    lobbies[j + 1] = lobbies[j];
                }

                lobbies[j + 1] = lobby;
            }
        }
        #endregion
    }
}
