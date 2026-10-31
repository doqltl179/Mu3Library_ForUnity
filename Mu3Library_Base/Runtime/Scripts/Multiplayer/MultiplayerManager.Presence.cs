using System;
using System.Text;
using Mu3Library.Multiplayer.Data;
using UnityEngine;

namespace Mu3Library.Multiplayer
{
    public partial class MultiplayerManager
    {
        // The status the game set for the current session; null for none.
        private string _presenceStatus = null;

        // What the backend was last asked to show, so it hears only of changes.
        private string _shownStatus = null;
        private LobbyId _shownGroup = LobbyId.None;
        private int _shownGroupSize = 0;

        // How often a removal the backend refused is asked again, after the first try on the next update.
        private const double PresenceRemovalRetrySeconds = 1d;

        // When to ask the backend again to remove what a session showed, after it refused; NaN for no removal to ask again.
        private double _presenceRemovalRetryAt = double.NaN;



        public MultiplayerResult SetPresenceStatus(string status)
        {
            if (!_isInitialized || _isDisposed)
            {
                return MultiplayerResult.Failure(MultiplayerReason.NotInitialized);
            }

            if (_state == SessionState.Offline)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidState);
            }

            if (string.IsNullOrEmpty(status))
            {
                status = null;
            }
            else if (!IsCleanText(status))
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }
            else if (Encoding.UTF8.GetByteCount(status) > MultiplayerLimits.MaxPresenceStatusSize)
            {
                return MultiplayerResult.Failure(MultiplayerReason.MessageTooLarge);
            }

            string previous = _presenceStatus;
            _presenceStatus = status;
            if (!ShowPresence())
            {
                // The next update asks the backend to show the previous status again.
                _presenceStatus = previous;
                return MultiplayerResult.Failure(MultiplayerReason.BackendError);
            }

            return MultiplayerResult.Success;
        }

        /// <summary>
        /// Brings what the backend shows up to date: the status, and from the lobby on the session
        /// as a group of its players. Every update runs it, so a roster change on any path, a late
        /// join or a return included, reaches the friends list without a call of its own; it
        /// compares a few fields and allocates nothing while nothing changed.
        /// </summary>
        private bool ShowPresence()
        {
            bool isGrouped = _lobbyId.IsValid
                && (_state == SessionState.Lobby || _state == SessionState.InGame || _state == SessionState.Reconnecting);
            LobbyId group = isGrouped ? _lobbyId : LobbyId.None;
            int groupSize = isGrouped ? _participants.Count : 0;
            bool isRetry = false;
            if (string.Equals(_presenceStatus, _shownStatus, StringComparison.Ordinal)
                && group == _shownGroup
                && groupSize == _shownGroupSize)
            {
                // A removal the backend refused is asked again, since a manager that is offline
                // changes nothing that would ask: on the next update, then once a second.
                if (double.IsNaN(_presenceRemovalRetryAt) || Now < _presenceRemovalRetryAt)
                {
                    return true;
                }

                isRetry = true;
            }

            // Taken as shown even when refused, so a backend that keeps refusing is not asked every frame.
            _shownStatus = _presenceStatus;
            _shownGroup = group;
            _shownGroupSize = groupSize;
            _presenceRemovalRetryAt = double.NaN;
            if (_backend.SetPresence(_presenceStatus, group, groupSize))
            {
                return true;
            }

            // A backend that is gone can remove nothing, so only an available one is asked again;
            // the Steam backend deletes its keys itself as it is disposed.
            if (_presenceStatus == null && !group.IsValid && _backend.IsAvailable)
            {
                _presenceRemovalRetryAt = isRetry ? Now + PresenceRemovalRetrySeconds : Now;
            }

            // Once per refusal, not once per retry.
            if (!isRetry)
            {
                Debug.LogWarning($"[Multiplayer] The {_backend.Name} backend refused to show the presence (group {group}, {groupSize} players).");
            }

            return false;
        }

        /// <summary>Removes the status and the group as the session ends.</summary>
        private void ResetPresenceState()
        {
            _presenceStatus = null;
            ShowPresence();
        }
    }
}
