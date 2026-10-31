using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using Steamworks;
using UnityEngine;

namespace Mu3Library.Multiplayer.Steam
{
    public sealed partial class SteamBackend
    {
        private sealed class PendingSearch
        {
            public LobbySearchQuery Query;
            public Action<MultiplayerReason, IReadOnlyList<BackendLobby>> OnCompleted;
            public bool IsAnswered;
        }

        // Steam gives up on a lobby search after about 20 seconds; past this the backend answers
        // itself, so a search Steam never answers cannot hold up the ones behind it.
        private const double SearchTimeoutSeconds = 25d;

        // Lobbies this player is in, with the owner last seen, so an owner change can be told.
        private readonly Dictionary<ulong, PlayerId> _lobbyOwners = new Dictionary<ulong, PlayerId>();

        // Steam runs one lobby search at a time and cancels the older one, so searches wait in line.
        private readonly Queue<PendingSearch> _searches = new Queue<PendingSearch>();
        private PendingSearch _currentSearch = null;
        private IDisposable _currentSearchCall = null;
        private double _currentSearchStartedAt = 0d;

        // Call results stay referenced until Steam answers, then are disposed on the next update.
        private readonly List<IDisposable> _activeCalls = new List<IDisposable>();
        private readonly List<IDisposable> _finishedCalls = new List<IDisposable>();

        private Callback<LobbyDataUpdate_t> _lobbyDataUpdated;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdated;



        private void InitializeLobbies()
        {
            _lobbyDataUpdated = Callback<LobbyDataUpdate_t>.Create(update => CheckLobbyOwner(update.m_ulSteamIDLobby));
            _lobbyChatUpdated = Callback<LobbyChatUpdate_t>.Create(update => CheckLobbyOwner(update.m_ulSteamIDLobby));
        }

        public void CreateLobby(LobbyVisibility visibility, int capacity, IReadOnlyDictionary<string, string> data, Action<MultiplayerReason, LobbyId> onCompleted)
        {
            Dictionary<string, string> snapshot = new Dictionary<string, string>();
            if (data != null)
            {
                foreach (KeyValuePair<string, string> pair in data)
                {
                    snapshot[pair.Key] = pair.Value;
                }
            }

            SteamAPICall_t call = IsAvailable
                ? SteamMatchmaking.CreateLobby(SteamReasons.ToLobbyType(visibility), capacity)
                : SteamAPICall_t.Invalid;
            if (call == SteamAPICall_t.Invalid)
            {
                MultiplayerReason failure = UnavailableOr(MultiplayerReason.BackendError);
                EnqueueCallback(() => onCompleted?.Invoke(failure, LobbyId.None));
                return;
            }

            Track<LobbyCreated_t>(call, (created, ioFailure) =>
            {
                MultiplayerReason reason = ioFailure ? MultiplayerReason.BackendError : SteamReasons.FromCreateResult(created.m_eResult);
                if (reason != MultiplayerReason.None)
                {
                    EnqueueCallback(() => onCompleted?.Invoke(reason, LobbyId.None));
                    return;
                }

                CSteamID lobby = new CSteamID(created.m_ulSteamIDLobby);
                foreach (KeyValuePair<string, string> pair in snapshot)
                {
                    // Without its reserved keys a lobby can be neither found nor joined, so a key
                    // Steam refuses, too long or with too long a value, fails the whole lobby.
                    if (!SteamMatchmaking.SetLobbyData(lobby, pair.Key, pair.Value ?? string.Empty))
                    {
                        Debug.LogWarning($"[Multiplayer] Steam refused lobby data '{pair.Key}'; the lobby is closed again.");
                        SteamMatchmaking.LeaveLobby(lobby);
                        EnqueueCallback(() => onCompleted?.Invoke(MultiplayerReason.BackendError, LobbyId.None));
                        return;
                    }
                }

                _lobbyOwners[lobby.m_SteamID] = LocalPlayerId;
                EnqueueCallback(() => onCompleted?.Invoke(MultiplayerReason.None, new LobbyId(lobby.m_SteamID)));
            });
        }

        public void SearchLobbies(LobbySearchQuery query, Action<MultiplayerReason, IReadOnlyList<BackendLobby>> onCompleted)
        {
            _searches.Enqueue(new PendingSearch
            {
                Query = query != null ? query.Clone() : new LobbySearchQuery(),
                OnCompleted = onCompleted,
            });
            StartNextSearch();
        }

        public void JoinLobby(LobbyId lobbyId, Action<MultiplayerReason> onCompleted)
        {
            CSteamID lobby = new CSteamID(lobbyId.Value);
            SteamAPICall_t call = IsAvailable ? SteamMatchmaking.JoinLobby(lobby) : SteamAPICall_t.Invalid;
            if (call == SteamAPICall_t.Invalid)
            {
                MultiplayerReason failure = UnavailableOr(MultiplayerReason.BackendError);
                EnqueueCallback(() => onCompleted?.Invoke(failure));
                return;
            }

            Track<LobbyEnter_t>(call, (entered, ioFailure) =>
            {
                MultiplayerReason reason = MultiplayerReason.BackendError;
                if (!ioFailure)
                {
                    // Known only for a lobby this player saw in search; otherwise a refusal reads as not found.
                    bool isInGame = SteamMatchmaking.GetLobbyData(lobby, LobbyDataKeys.State) == LobbyDataKeys.StateInGame;
                    reason = SteamReasons.FromEnterResponse((EChatRoomEnterResponse)entered.m_EChatRoomEnterResponse, isInGame);
                }

                if (reason == MultiplayerReason.None)
                {
                    _lobbyOwners[lobby.m_SteamID] = new PlayerId(SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID);
                }

                EnqueueCallback(() => onCompleted?.Invoke(reason));
            });
        }

        public void LeaveLobby(LobbyId lobby)
        {
            if (!IsAvailable)
            {
                return;
            }

            _lobbyOwners.Remove(lobby.Value);
            SteamMatchmaking.LeaveLobby(new CSteamID(lobby.Value));
        }

        public string GetLobbyData(LobbyId lobby, string key)
        {
            if (!IsAvailable || string.IsNullOrEmpty(key))
            {
                return null;
            }

            // Steam answers an empty string for a key or lobby it does not know.
            string value = SteamMatchmaking.GetLobbyData(new CSteamID(lobby.Value), key);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public bool SetLobbyData(LobbyId lobby, string key, string value)
        {
            return IsAvailable
                && !string.IsNullOrEmpty(key)
                && SteamMatchmaking.SetLobbyData(new CSteamID(lobby.Value), key, value ?? string.Empty);
        }

        public bool SetLobbyJoinable(LobbyId lobby, bool joinable)
        {
            return IsAvailable && SteamMatchmaking.SetLobbyJoinable(new CSteamID(lobby.Value), joinable);
        }

        public bool SetLobbyCapacity(LobbyId lobby, int capacity)
        {
            return IsAvailable && SteamMatchmaking.SetLobbyMemberLimit(new CSteamID(lobby.Value), capacity);
        }

        public PlayerId GetLobbyOwner(LobbyId lobby)
        {
            return IsAvailable
                ? new PlayerId(SteamMatchmaking.GetLobbyOwner(new CSteamID(lobby.Value)).m_SteamID)
                : PlayerId.None;
        }

        public bool IsLobbyMember(LobbyId lobby, PlayerId player)
        {
            if (!IsAvailable || !player.IsValid)
            {
                return false;
            }

            CSteamID steamLobby = new CSteamID(lobby.Value);
            int count = SteamMatchmaking.GetNumLobbyMembers(steamLobby);
            for (int i = 0; i < count; i++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(steamLobby, i).m_SteamID == player.Value)
                {
                    return true;
                }
            }

            return false;
        }

        #region Search
        private void StartNextSearch()
        {
            while (_currentSearch == null && IsAvailable && _searches.Count > 0)
            {
                PendingSearch search = _searches.Dequeue();
                ApplyFilters(search.Query);

                SteamAPICall_t call = SteamMatchmaking.RequestLobbyList();
                if (call == SteamAPICall_t.Invalid)
                {
                    AnswerSearch(search, MultiplayerReason.BackendError, null);
                    continue;
                }

                _currentSearch = search;
                _currentSearchStartedAt = Time.realtimeSinceStartupAsDouble;
                _currentSearchCall = Track<LobbyMatchList_t>(call, (matches, ioFailure) =>
                {
                    if (_currentSearch != search)
                    {
                        // Answered already, by the timeout.
                        return;
                    }

                    List<BackendLobby> lobbies = null;
                    MultiplayerReason reason = MultiplayerReason.BackendError;
                    try
                    {
                        if (!ioFailure)
                        {
                            lobbies = ReadSearchResults((int)matches.m_nLobbiesMatching);
                            reason = MultiplayerReason.None;
                        }
                    }
                    finally
                    {
                        // Whatever reading the results did, this search answers and the next one starts.
                        FinishCurrentSearch(reason, lobbies);
                    }
                });
            }
        }

        /// <summary>Answers the running search and every waiting one with the reason.</summary>
        private void FailSearches(MultiplayerReason reason)
        {
            if (_currentSearch != null)
            {
                if (_currentSearchCall != null)
                {
                    _activeCalls.Remove(_currentSearchCall);
                    _currentSearchCall.Dispose();
                }

                AnswerSearch(_currentSearch, reason, null);
                _currentSearch = null;
                _currentSearchCall = null;
            }

            while (_searches.Count > 0)
            {
                AnswerSearch(_searches.Dequeue(), reason, null);
            }
        }

        /// <summary>The reason a request fails when Steam is gone, or <paramref name="otherwise"/>.</summary>
        private MultiplayerReason UnavailableOr(MultiplayerReason otherwise)
        {
            return IsAvailable ? otherwise : MultiplayerReason.SteamNotRunning;
        }

        private void CheckSearchTimeout()
        {
            if (_currentSearch != null && Time.realtimeSinceStartupAsDouble - _currentSearchStartedAt > SearchTimeoutSeconds)
            {
                if (_currentSearchCall != null)
                {
                    _activeCalls.Remove(_currentSearchCall);
                    _currentSearchCall.Dispose();
                }

                FinishCurrentSearch(MultiplayerReason.Timeout, null);
            }
        }

        private void FinishCurrentSearch(MultiplayerReason reason, List<BackendLobby> lobbies)
        {
            PendingSearch search = _currentSearch;
            _currentSearch = null;
            _currentSearchCall = null;
            AnswerSearch(search, reason, lobbies);
            StartNextSearch();
        }

        private void AnswerSearch(PendingSearch search, MultiplayerReason reason, IReadOnlyList<BackendLobby> lobbies)
        {
            if (search == null || search.IsAnswered)
            {
                return;
            }

            search.IsAnswered = true;
            EnqueueCallback(() => search.OnCompleted?.Invoke(reason, lobbies));
        }

        private static void ApplyFilters(LobbySearchQuery query)
        {
            // Steam compares and orders these the way the library's enums are numbered. A filter
            // without a key would hand Steam a null key, so it is skipped.
            for (int i = 0; i < query.StringFilters.Count; i++)
            {
                LobbyStringFilter filter = query.StringFilters[i];
                if (!string.IsNullOrEmpty(filter.Key))
                {
                    SteamMatchmaking.AddRequestLobbyListStringFilter(filter.Key, filter.Value ?? string.Empty, (ELobbyComparison)(int)filter.Comparison);
                }
            }

            for (int i = 0; i < query.NumericFilters.Count; i++)
            {
                LobbyNumericFilter filter = query.NumericFilters[i];
                if (!string.IsNullOrEmpty(filter.Key))
                {
                    SteamMatchmaking.AddRequestLobbyListNumericalFilter(filter.Key, filter.Value, (ELobbyComparison)(int)filter.Comparison);
                }
            }

            SteamMatchmaking.AddRequestLobbyListDistanceFilter((ELobbyDistanceFilter)(int)query.Distance);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(Mathf.Clamp(query.MaxResults, 1, LobbySearchQuery.MaxResultLimit));
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
        }

        private static List<BackendLobby> ReadSearchResults(int count)
        {
            List<BackendLobby> lobbies = new List<BackendLobby>(count);
            for (int i = 0; i < count; i++)
            {
                CSteamID lobby = SteamMatchmaking.GetLobbyByIndex(i);
                Dictionary<string, string> data = new Dictionary<string, string>();

                int dataCount = SteamMatchmaking.GetLobbyDataCount(lobby);
                for (int j = 0; j < dataCount; j++)
                {
                    if (SteamMatchmaking.GetLobbyDataByIndex(lobby, j, out string key, Constants.k_nMaxLobbyKeyLength, out string value, Constants.k_cubChatMetadataMax))
                    {
                        data[key] = value;
                    }
                }

                lobbies.Add(new BackendLobby(
                    new LobbyId(lobby.m_SteamID),
                    SteamMatchmaking.GetNumLobbyMembers(lobby),
                    SteamMatchmaking.GetLobbyMemberLimit(lobby),
                    data));
            }

            return lobbies;
        }
        #endregion

        #region Steam Callbacks
        /// <summary>
        /// Steam passes a lobby on when its owner leaves and reports that only as a data or
        /// member change, so every such change compares the owner with the one last seen.
        /// </summary>
        private void CheckLobbyOwner(ulong lobby)
        {
            if (_isDisposed || !_lobbyOwners.TryGetValue(lobby, out PlayerId known))
            {
                return;
            }

            PlayerId owner = new PlayerId(SteamMatchmaking.GetLobbyOwner(new CSteamID(lobby)).m_SteamID);
            if (!owner.IsValid || owner == known)
            {
                return;
            }

            _lobbyOwners[lobby] = owner;
            _events.Enqueue(new PendingEvent { Kind = EventKind.OwnerChanged, Lobby = new LobbyId(lobby), Player = owner });
        }

        private IDisposable Track<T>(SteamAPICall_t call, Action<T, bool> onResult)
        {
            CallResult<T> result = CallResult<T>.Create();
            _activeCalls.Add(result);
            result.Set(call, (value, ioFailure) =>
            {
                _activeCalls.Remove(result);
                _finishedCalls.Add(result);
                if (!_isDisposed)
                {
                    onResult(value, ioFailure);
                }
            });
            return result;
        }

        private void DisposeFinishedCalls()
        {
            for (int i = 0; i < _finishedCalls.Count; i++)
            {
                _finishedCalls[i].Dispose();
            }

            _finishedCalls.Clear();
        }
        #endregion

        private void ShutdownLobbies(bool steamRuns)
        {
            if (steamRuns)
            {
                foreach (ulong lobby in _lobbyOwners.Keys)
                {
                    SteamMatchmaking.LeaveLobby(new CSteamID(lobby));
                }
            }

            _lobbyOwners.Clear();
            _searches.Clear();
            _currentSearch = null;
            _currentSearchCall = null;

            for (int i = 0; i < _activeCalls.Count; i++)
            {
                _activeCalls[i].Dispose();
            }

            _activeCalls.Clear();
            DisposeFinishedCalls();

            _lobbyDataUpdated?.Dispose();
            _lobbyChatUpdated?.Dispose();
            _lobbyDataUpdated = null;
            _lobbyChatUpdated = null;
        }
    }
}
