using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using Steamworks;
using UnityEngine;

namespace Mu3Library.Multiplayer.Steam
{
    public sealed partial class SteamBackend : ILobbyInviteBackend
    {
        private sealed class PendingSearch
        {
            public LobbySearchQuery Query;
            public Action<MultiplayerReason, IReadOnlyList<BackendLobby>> OnCompleted;
            public bool IsAnswered;
        }

        /// <summary>A request Steam answers later through a call result.</summary>
        private sealed class ActiveCall
        {
            public IDisposable Result;

            /// <summary>
            /// Answers the request when Steam never will, since it is gone; null for a search,
            /// which <see cref="FailSearches"/> answers.
            /// </summary>
            public Action<MultiplayerReason> OnAbandoned;
        }

        /// <summary>An entry Steam refused, waiting for the lobby's data that tells why.</summary>
        private sealed class PendingRefusal
        {
            public ulong Lobby;
            public double Deadline;
            public Action<MultiplayerReason> OnCompleted;
        }

        // Steam gives up on a lobby search after about 20 seconds; past this the backend answers
        // itself, so a search Steam never answers cannot hold up the ones behind it.
        private const double SearchTimeoutSeconds = 25d;

        // How long a refused entry waits for the lobby's data before it answers with what it knows.
        private const double RefusalDataTimeoutSeconds = 5d;

        // Lobbies this player is in, with the owner last seen, so an owner change can be told.
        private readonly Dictionary<ulong, PlayerId> _lobbyOwners = new Dictionary<ulong, PlayerId>();

        // Steam runs one lobby search at a time and cancels the older one, so searches wait in line.
        private readonly Queue<PendingSearch> _searches = new Queue<PendingSearch>();
        private PendingSearch _currentSearch = null;
        private ActiveCall _currentSearchCall = null;
        private double _currentSearchStartedAt = 0d;

        // Call results stay referenced until Steam answers, then are disposed on the next update.
        private readonly List<ActiveCall> _activeCalls = new List<ActiveCall>();
        private readonly List<IDisposable> _finishedCalls = new List<IDisposable>();
        private readonly List<PendingRefusal> _refusals = new List<PendingRefusal>();

        private Callback<LobbyDataUpdate_t> _lobbyDataUpdated;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdated;

        /// <summary>How many lobbies the backend is in, for the tests.</summary>
        internal int HeldLobbyCount => _lobbyOwners.Count;

        /// <summary>How many requests wait for Steam's answer, for the integration tests.</summary>
        internal int PendingLobbyCallCount => _activeCalls.Count;



        private void InitializeLobbies()
        {
            _lobbyDataUpdated = Callback<LobbyDataUpdate_t>.Create(update =>
            {
                // Dispatched late, once Steam cannot be used: the next update answers the
                // entries waiting for the data, and nothing here calls Steam.
                if (!IsAvailable)
                {
                    return;
                }

                // The lobby's own data, rather than a member's, answers the entries waiting for it.
                if (update.m_ulSteamIDMember == update.m_ulSteamIDLobby)
                {
                    AnswerRefusals(update.m_ulSteamIDLobby, onlyExpired: false);
                }

                CheckLobbyOwner(update.m_ulSteamIDLobby);
            });
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

            SteamAPICall_t call = SteamAPICall_t.Invalid;
            if (IsAvailable)
            {
                try
                {
                    call = SteamMatchmaking.CreateLobby(SteamReasons.ToLobbyType(visibility), capacity);
                }
                catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
                {
                    Break(cause);
                }
            }

            if (call == SteamAPICall_t.Invalid)
            {
                MultiplayerReason failure = InvalidCreateCallReason(IsAvailable);
                EnqueueCallback(() => onCompleted?.Invoke(failure, LobbyId.None));
                return;
            }

            Track<LobbyCreated_t>(call, (created, ioFailure) =>
            {
                MultiplayerReason reason = CreatedLobbyReason(created.m_eResult, ioFailure);
                if (reason != MultiplayerReason.None)
                {
                    EnqueueCallback(() => onCompleted?.Invoke(reason, LobbyId.None));
                    return;
                }

                CSteamID lobby = new CSteamID(created.m_ulSteamIDLobby);
                try
                {
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
                }
                catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
                {
                    // Inside Steam's callbacks, which may be the game's own run of them. Steam
                    // made the lobby, so it is left with the others the backend is in.
                    _lobbyOwners[lobby.m_SteamID] = LocalPlayerId;
                    Break(cause);
                    EnqueueCallback(() => onCompleted?.Invoke(MultiplayerReason.SteamNotRunning, LobbyId.None));
                    return;
                }

                _lobbyOwners[lobby.m_SteamID] = LocalPlayerId;
                EnqueueCallback(() => onCompleted?.Invoke(MultiplayerReason.None, new LobbyId(lobby.m_SteamID)));
            }, reason => onCompleted?.Invoke(reason, LobbyId.None));
        }

        /// <summary>
        /// The reason for Steam's answer to a lobby creation (<see cref="SteamReasons.FromCreateResult"/>),
        /// or <see cref="MultiplayerReason.BackendError"/> for a call that ended in an I/O failure.
        /// Every failure logs a warning with what Steam answered, whichever reason it maps to: one
        /// reason stands for several of Steam's answers (<see cref="MultiplayerReason.BackendError"/>
        /// for most, <see cref="MultiplayerReason.SteamNotRunning"/> also for a Steam that went
        /// away), and only Steam's own result tells a developer which limit or error Steam meant.
        /// A lobby creation is rare and the player's own request, so the warning does not repeat
        /// every frame.
        /// </summary>
        internal static MultiplayerReason CreatedLobbyReason(EResult result, bool ioFailure)
        {
            if (ioFailure)
            {
                Debug.LogWarning($"[Multiplayer] Steam could not create a lobby (I/O failure); the lobby creation answers {MultiplayerReason.BackendError}.");
                return MultiplayerReason.BackendError;
            }

            MultiplayerReason reason = SteamReasons.FromCreateResult(result);
            if (reason != MultiplayerReason.None)
            {
                Debug.LogWarning($"[Multiplayer] Steam could not create a lobby ({result}); the lobby creation answers {reason}.");
            }

            return reason;
        }

        /// <summary>
        /// The reason for a lobby creation that Steam gave no call to wait on
        /// (<c>SteamMatchmaking.CreateLobby</c> returned <c>SteamAPICall_t.Invalid</c>, or was not
        /// called). While Steam can be used (<paramref name="steamAvailable"/>) it is
        /// <see cref="MultiplayerReason.BackendError"/> with a warning in the format
        /// <see cref="CreatedLobbyReason"/> logs, since nothing else tells this failure from the
        /// other answers that reason stands for (#434). Once Steam cannot be used it is
        /// <see cref="MultiplayerReason.SteamNotRunning"/> without a warning: that reason names the
        /// cause, and when a Steam call failed, <see cref="Break"/> has already logged one.
        /// </summary>
        internal static MultiplayerReason InvalidCreateCallReason(bool steamAvailable)
        {
            if (!steamAvailable)
            {
                return MultiplayerReason.SteamNotRunning;
            }

            Debug.LogWarning($"[Multiplayer] Steam could not create a lobby (CreateLobby returned an invalid call handle); the lobby creation answers {MultiplayerReason.BackendError}.");
            return MultiplayerReason.BackendError;
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
            SteamAPICall_t call = SteamAPICall_t.Invalid;
            if (IsAvailable)
            {
                try
                {
                    call = SteamMatchmaking.JoinLobby(lobby);
                }
                catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
                {
                    Break(cause);
                }
            }

            if (call == SteamAPICall_t.Invalid)
            {
                MultiplayerReason failure = InvalidJoinCallReason(IsAvailable);
                EnqueueCallback(() => onCompleted?.Invoke(failure));
                return;
            }

            Track<LobbyEnter_t>(call, (entered, ioFailure) =>
            {
                MultiplayerReason reason = MultiplayerReason.BackendError;
                try
                {
                    if (!ioFailure)
                    {
                        EChatRoomEnterResponse response = (EChatRoomEnterResponse)entered.m_EChatRoomEnterResponse;
                        if (response == EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed && SteamMatchmaking.RequestLobbyData(lobby))
                        {
                            // Steam refuses a lobby that is not joinable and one that takes this
                            // player only by invite with the same answer, and knows the data of a
                            // lobby this player is not in only after asking, unless a search found
                            // the lobby. The data tells whether the session's game runs, which
                            // the refusal then names GameInProgress, whether that game refuses
                            // late joiners, and who hosts it, so a player whose slot the host
                            // keeps can reach the host past the lobby (FR-21). So the answer
                            // waits for the data.
                            _refusals.Add(new PendingRefusal
                            {
                                Lobby = lobby.m_SteamID,
                                Deadline = Time.realtimeSinceStartupAsDouble + RefusalDataTimeoutSeconds,
                                OnCompleted = onCompleted,
                            });
                            return;
                        }

                        reason = SteamReasons.FromEnterResponse(response, IsLobbyInGame(lobby));
                    }

                    if (reason == MultiplayerReason.None)
                    {
                        _lobbyOwners[lobby.m_SteamID] = new PlayerId(SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID);
                    }
                }
                catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
                {
                    // Inside Steam's callbacks, which may be the game's own run of them. A lobby
                    // Steam let this player into is left with the others the backend is in.
                    if (!ioFailure && (EChatRoomEnterResponse)entered.m_EChatRoomEnterResponse == EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                    {
                        _lobbyOwners[lobby.m_SteamID] = PlayerId.None;
                    }

                    Break(cause);
                    reason = MultiplayerReason.SteamNotRunning;
                }

                EnqueueCallback(() => onCompleted?.Invoke(reason));
            }, reason => onCompleted?.Invoke(reason));
        }

        /// <summary>
        /// The reason for a lobby entry that Steam gave no call to wait on
        /// (<c>SteamMatchmaking.JoinLobby</c> returned <c>SteamAPICall_t.Invalid</c>, or was not
        /// called), decided as <see cref="InvalidCreateCallReason"/> decides it for a lobby
        /// creation (#465). While Steam can be used (<paramref name="steamAvailable"/>) it is
        /// <see cref="MultiplayerReason.BackendError"/> with a warning in that format, since
        /// nothing else tells this failure from the other answers that reason stands for. Once
        /// Steam cannot be used it is <see cref="MultiplayerReason.SteamNotRunning"/> without a
        /// warning: that reason names the cause, and when a Steam call failed,
        /// <see cref="Break"/> has already logged one.
        /// </summary>
        internal static MultiplayerReason InvalidJoinCallReason(bool steamAvailable)
        {
            if (!steamAvailable)
            {
                return MultiplayerReason.SteamNotRunning;
            }

            Debug.LogWarning($"[Multiplayer] Steam could not join a lobby (JoinLobby returned an invalid call handle); the lobby entry answers {MultiplayerReason.BackendError}.");
            return MultiplayerReason.BackendError;
        }

        public void LeaveLobby(LobbyId lobby)
        {
            if (!IsAvailable)
            {
                return;
            }

            _lobbyOwners.Remove(lobby.Value);
            try
            {
                SteamMatchmaking.LeaveLobby(new CSteamID(lobby.Value));
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
            }
        }

        public string GetLobbyData(LobbyId lobby, string key)
        {
            if (!IsAvailable || string.IsNullOrEmpty(key))
            {
                return null;
            }

            // Steam answers an empty string for a key or lobby it does not know.
            string value;
            try
            {
                value = SteamMatchmaking.GetLobbyData(new CSteamID(lobby.Value), key);
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
                return null;
            }

            return string.IsNullOrEmpty(value) ? null : value;
        }

        public bool SetLobbyData(LobbyId lobby, string key, string value)
        {
            try
            {
                return IsAvailable
                    && !string.IsNullOrEmpty(key)
                    && SteamMatchmaking.SetLobbyData(new CSteamID(lobby.Value), key, value ?? string.Empty);
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
                return false;
            }
        }

        public bool DeleteLobbyData(LobbyId lobby, string key)
        {
            try
            {
                return IsAvailable
                    && !string.IsNullOrEmpty(key)
                    && SteamMatchmaking.DeleteLobbyData(new CSteamID(lobby.Value), key);
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
                return false;
            }
        }

        public bool SetLobbyJoinable(LobbyId lobby, bool joinable)
        {
            try
            {
                return IsAvailable && SteamMatchmaking.SetLobbyJoinable(new CSteamID(lobby.Value), joinable);
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
                return false;
            }
        }

        public bool SetLobbyCapacity(LobbyId lobby, int capacity)
        {
            try
            {
                return IsAvailable && SteamMatchmaking.SetLobbyMemberLimit(new CSteamID(lobby.Value), capacity);
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
                return false;
            }
        }

        public PlayerId GetLobbyOwner(LobbyId lobby)
        {
            try
            {
                return IsAvailable
                    ? new PlayerId(SteamMatchmaking.GetLobbyOwner(new CSteamID(lobby.Value)).m_SteamID)
                    : PlayerId.None;
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
                return PlayerId.None;
            }
        }

        public bool IsLobbyMember(LobbyId lobby, PlayerId player)
        {
            if (!IsAvailable || !player.IsValid)
            {
                return false;
            }

            CSteamID steamLobby = new CSteamID(lobby.Value);
            try
            {
                int count = SteamMatchmaking.GetNumLobbyMembers(steamLobby);
                for (int i = 0; i < count; i++)
                {
                    if (SteamMatchmaking.GetLobbyMemberByIndex(steamLobby, i).m_SteamID == player.Value)
                    {
                        return true;
                    }
                }
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
            }

            return false;
        }

        bool ILobbyInviteBackend.InviteToLobby(LobbyId lobby, PlayerId player)
        {
            return InviteFriend(lobby, player).IsSuccess;
        }

        #region Search
        private void StartNextSearch()
        {
            while (_currentSearch == null && IsAvailable && _searches.Count > 0)
            {
                PendingSearch search = _searches.Dequeue();
                SteamAPICall_t call;
                try
                {
                    ApplyFilters(search.Query);
                    call = SteamMatchmaking.RequestLobbyList();
                }
                catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
                {
                    // The searches behind it answer on the next update, with the others waiting on Steam.
                    Break(cause);
                    AnswerSearch(search, MultiplayerReason.SteamNotRunning, null);
                    return;
                }

                if (call == SteamAPICall_t.Invalid)
                {
                    AnswerSearch(search, InvalidSearchCallReason(), null);
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
                    catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
                    {
                        // Inside Steam's callbacks, which may be the game's own run of them.
                        Break(cause);
                        reason = MultiplayerReason.SteamNotRunning;
                    }
                    finally
                    {
                        // Whatever reading the results did, this search answers and the next one starts.
                        FinishCurrentSearch(reason, lobbies);
                    }
                });
            }
        }

        /// <summary>
        /// The reason for a lobby search that Steam gave no call to wait on
        /// (<c>SteamMatchmaking.RequestLobbyList</c> returned <c>SteamAPICall_t.Invalid</c>):
        /// <see cref="MultiplayerReason.BackendError"/> with a warning in the format
        /// <see cref="InvalidCreateCallReason"/> logs, since nothing else tells this failure from
        /// the other answers that reason stands for (#465). Unlike a lobby creation or entry, a
        /// search gets here only while Steam can be used, so this takes no availability:
        /// <see cref="StartNextSearch"/> calls Steam only then, and a call that throws answers
        /// <see cref="MultiplayerReason.SteamNotRunning"/> after <see cref="Break"/> has logged it.
        /// </summary>
        internal static MultiplayerReason InvalidSearchCallReason()
        {
            Debug.LogWarning($"[Multiplayer] Steam could not search for lobbies (RequestLobbyList returned an invalid call handle); the lobby search answers {MultiplayerReason.BackendError}.");
            return MultiplayerReason.BackendError;
        }

        /// <summary>Answers the running search and every waiting one with the reason.</summary>
        private void FailSearches(MultiplayerReason reason)
        {
            if (_currentSearch != null)
            {
                if (_currentSearchCall != null)
                {
                    _activeCalls.Remove(_currentSearchCall);
                    _currentSearchCall.Result.Dispose();
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

        /// <summary>
        /// Answers every lobby creation and entry Steam has not answered with the reason, as
        /// Steam will not answer them now, and lets go of their call results, so an answer that
        /// arrives after all is not handed on twice. A refused entry waiting for the lobby's data
        /// answers the reason too.
        /// </summary>
        private void FailLobbyCalls(MultiplayerReason reason)
        {
            int i = 0;
            while (i < _activeCalls.Count)
            {
                ActiveCall call = _activeCalls[i];
                if (call.OnAbandoned == null)
                {
                    i++;
                    continue;
                }

                _activeCalls.RemoveAt(i);
                call.Result.Dispose();
                Action<MultiplayerReason> onAbandoned = call.OnAbandoned;
                EnqueueCallback(() => onAbandoned(reason));
            }

            for (int j = 0; j < _refusals.Count; j++)
            {
                Action<MultiplayerReason> onCompleted = _refusals[j].OnCompleted;
                EnqueueCallback(() => onCompleted?.Invoke(reason));
            }

            _refusals.Clear();
        }

        /// <summary>
        /// Answers the refused entries into the lobby, or, with <paramref name="onlyExpired"/>,
        /// those of any lobby that waited too long for its data, from the data Steam knows now:
        /// <see cref="MultiplayerReason.GameInProgress"/> when it shows the game running, and
        /// <see cref="MultiplayerReason.LobbyNotFound"/> otherwise.
        /// </summary>
        private void AnswerRefusals(ulong lobby, bool onlyExpired)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            int i = 0;
            while (i < _refusals.Count)
            {
                PendingRefusal refusal = _refusals[i];
                if (onlyExpired ? now < refusal.Deadline : refusal.Lobby != lobby)
                {
                    i++;
                    continue;
                }

                _refusals.RemoveAt(i);
                MultiplayerReason reason = MultiplayerReason.SteamNotRunning;
                try
                {
                    if (IsAvailable)
                    {
                        reason = SteamReasons.FromEnterResponse(EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed, IsLobbyInGame(new CSteamID(refusal.Lobby)));
                    }
                }
                catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
                {
                    // Possibly inside Steam's callbacks, which may be the game's own run of them.
                    Break(cause);
                }

                Action<MultiplayerReason> onCompleted = refusal.OnCompleted;
                EnqueueCallback(() => onCompleted?.Invoke(reason));
            }
        }

        private static bool IsLobbyInGame(CSteamID lobby)
        {
            return SteamMatchmaking.GetLobbyData(lobby, LobbyDataKeys.State) == LobbyDataKeys.StateInGame;
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
                    _currentSearchCall.Result.Dispose();
                }

                FinishCurrentSearch(MultiplayerReason.Timeout, null);
            }
        }

        private void CheckRefusalTimeouts()
        {
            if (_refusals.Count > 0 && IsAvailable)
            {
                AnswerRefusals(0UL, onlyExpired: true);
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
                // Steam does not tell keys apart by case and may hand one back in another case.
                Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            // Dispatched late, once Steam cannot be used, it calls Steam no more.
            if (!IsAvailable || !_lobbyOwners.TryGetValue(lobby, out PlayerId known))
            {
                return;
            }

            PlayerId owner;
            try
            {
                owner = new PlayerId(SteamMatchmaking.GetLobbyOwner(new CSteamID(lobby)).m_SteamID);
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                // Inside Steam's callbacks, which may be the game's own run of them.
                Break(cause);
                return;
            }

            if (!owner.IsValid || owner == known)
            {
                return;
            }

            _lobbyOwners[lobby] = owner;
            _events.Enqueue(new PendingEvent { Kind = EventKind.OwnerChanged, Lobby = new LobbyId(lobby), Player = owner });
        }

        /// <param name="onAbandoned">
        /// Answers the request when Steam is gone before it answered; null for a search, which
        /// <see cref="FailSearches"/> answers.
        /// </param>
        /// <remarks>
        /// An answer that Steam dispatches once Steam cannot be used, later in the run of
        /// callbacks in which a call failed or in the game's own run of them, is not handed on:
        /// handling it would call Steam again, and a lobby creation or entry would answer
        /// success from a backend that is not available. The request answers
        /// <see cref="MultiplayerReason.SteamNotRunning"/> instead, as one that Steam never
        /// answered does; a search is still the running one, which <see cref="FailSearches"/>
        /// answers on the next update.
        /// </remarks>
        private ActiveCall Track<T>(SteamAPICall_t call, Action<T, bool> onResult, Action<MultiplayerReason> onAbandoned = null)
        {
            CallResult<T> result = CallResult<T>.Create();
            ActiveCall active = new ActiveCall { Result = result, OnAbandoned = onAbandoned };
            _activeCalls.Add(active);
            result.Set(call, (value, ioFailure) =>
            {
                _activeCalls.Remove(active);
                _finishedCalls.Add(result);
                if (_isDisposed)
                {
                    return;
                }

                if (!IsAvailable)
                {
                    if (onAbandoned != null)
                    {
                        EnqueueCallback(() => onAbandoned(MultiplayerReason.SteamNotRunning));
                    }

                    return;
                }

                onResult(value, ioFailure);
            });
            return active;
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
            LeaveLobbies(steamRuns);
            _searches.Clear();
            _currentSearch = null;
            _currentSearchCall = null;

            for (int i = 0; i < _activeCalls.Count; i++)
            {
                _activeCalls[i].Result.Dispose();
            }

            _activeCalls.Clear();
            _refusals.Clear();
            DisposeFinishedCalls();

            _lobbyDataUpdated?.Dispose();
            _lobbyChatUpdated?.Dispose();
            _lobbyDataUpdated = null;
            _lobbyChatUpdated = null;
        }

        /// <summary>
        /// Leaves every lobby the backend is in when <paramref name="steamRuns"/>, inside the
        /// guard, so a call that fails ends only this step; then forgets them either way. Steam
        /// passes a lobby this player owned on to another member.
        /// </summary>
        private void LeaveLobbies(bool steamRuns)
        {
            if (steamRuns)
            {
                try
                {
                    foreach (ulong lobby in _lobbyOwners.Keys)
                    {
                        SteamMatchmaking.LeaveLobby(new CSteamID(lobby));
                    }
                }
                catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
                {
                    Break(cause);
                }
            }

            _lobbyOwners.Clear();
        }
    }
}
