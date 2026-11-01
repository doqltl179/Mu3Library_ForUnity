using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Steamworks;
using UnityEngine;

namespace Mu3Library.Multiplayer.Steam
{
    public sealed partial class SteamBackend
    {
        private sealed class PendingAvatar
        {
            public PlayerId Player;
            public AvatarSize Size;
            public Action<MultiplayerReason, BackendAvatar> OnCompleted;
            public double StartedAt;

            /// <summary>True once the backend asked Steam for what it knows of the player, which it does once a request.</summary>
            public bool HasRequestedInformation;

            /// <summary>True once Steam told what it knows of the player (<c>PersonaStateChange_t</c>) while the request waited.</summary>
            public bool HasHeardOfPlayer;

            /// <summary>True once the backend asked Steam again after it told what it knows, which it does once a request.</summary>
            public bool HasAskedAgain;
        }

        // Steam has an avatar it downloads within a second or two; past this the request answers
        // itself, so every request answers once.
        private const double AvatarTimeoutSeconds = 10d;

        // Steam's avatars are at most 184 pixels wide; anything far larger is not an avatar.
        private const uint MaxAvatarSide = 4096;

        // Requests that wait for Steam to download what it needs, checked again on every update.
        private readonly List<PendingAvatar> _pendingAvatars = new List<PendingAvatar>();

        private Callback<PersonaStateChange_t> _personaStateChanged;
        private Callback<AvatarImageLoaded_t> _avatarImageLoaded;

        /// <summary>How many requests wait for Steam to download an avatar, for the integration tests.</summary>
        internal int PendingAvatarCount => _pendingAvatars.Count;

        /// <summary>How many times the backend asked Steam for what it knows of a player, for the integration tests.</summary>
        internal int UserInformationRequestCount { get; private set; }



        /// <summary>
        /// Reads the avatar Steam holds for the player. Steam knows the avatars of friends and of
        /// the players it shares a lobby with; for anyone else it downloads them first: the
        /// backend asks Steam once (<c>RequestUserInformation</c>) to download what it knows of
        /// the player, then only checks on every update whether Steam has the image, and asks
        /// once more after Steam told what it knows of the player (<c>PersonaStateChange_t</c>)
        /// while it still hands out no image, which tells whether the player has none. The answer
        /// arrives from <see cref="Update"/> in any case:
        /// <see cref="MultiplayerReason.BackendError"/> when Steam knows the player but has no
        /// image, and also once the game shut Steam down or a Steam call failed in its native
        /// library, for a request made then and for one still waiting, and
        /// <see cref="MultiplayerReason.Timeout"/> when it has none within 10 seconds.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For an account it did not know, Steam hands out its default image at once and the
        /// account's own picture a moment later, once it downloaded it: under a second later in
        /// the checks against Steam so far. When Steam reports that a player's avatar changed
        /// (<c>PersonaStateChange_t</c> with <c>k_EPersonaChangeAvatar</c>) or that it loaded an
        /// avatar image (<c>AvatarImageLoaded_t</c>), this player's own included, the backend
        /// tells the listener through <see cref="IMultiplayerBackendListener.OnAvatarChanged"/>
        /// from inside <see cref="Update"/>: the same one when the backend runs Steam's callbacks
        /// itself (<see cref="SteamBackendOptions.RunSteamCallbacks"/>), otherwise the first one
        /// after the game ran them. The manager then reads again the avatars it holds of
        /// that player, so the default image is replaced in the texture the game shows, as a new
        /// picture a player picks later is. Steam sends both notices for one download; the manager
        /// raises its event once for it, as a read that finds the image the texture shows raises
        /// nothing and an earlier read that answers after a later one changes nothing. When such
        /// a read fails, the manager tries it again, so a failure does not leave the default image
        /// in place (see <see cref="IMultiplayerManager.LoadAvatar"/>).
        /// </para>
        /// <para>
        /// An avatar answers <see cref="MultiplayerReason.BackendError"/> once Steam is gone, not
        /// the <see cref="MultiplayerReason.SteamNotRunning"/> a lobby search answers then, so the
        /// game sees one reason for it: the manager answers <see cref="MultiplayerReason.BackendError"/>
        /// itself while a backend is not available, since a disposed backend answers nothing, and
        /// it cannot tell a Steam that shut down from a backend that was disposed.
        /// </para>
        /// </remarks>
        public void LoadAvatar(PlayerId player, AvatarSize size, Action<MultiplayerReason, BackendAvatar> onCompleted)
        {
            if (!IsAvailable || !player.IsValid || size < AvatarSize.Small || size > AvatarSize.Large)
            {
                MultiplayerReason failure = IsAvailable ? MultiplayerReason.InvalidArgument : MultiplayerReason.BackendError;
                EnqueueCallback(() => onCompleted?.Invoke(failure, default));
                return;
            }

            PendingAvatar pending = new PendingAvatar
            {
                Player = player,
                Size = size,
                OnCompleted = onCompleted,
                StartedAt = Time.realtimeSinceStartupAsDouble,
            };

            if (!TryAnswerAvatar(pending))
            {
                _pendingAvatars.Add(pending);
            }
        }

        private void InitializeAvatars()
        {
            _personaStateChanged = Callback<PersonaStateChange_t>.Create(HandlePersonaStateChange);
            _avatarImageLoaded = Callback<AvatarImageLoaded_t>.Create(loaded => ReportAvatarChanged(loaded.m_steamID.m_SteamID));
        }

        private void ShutdownAvatars()
        {
            _pendingAvatars.Clear();
            _personaStateChanged?.Dispose();
            _personaStateChanged = null;
            _avatarImageLoaded?.Dispose();
            _avatarImageLoaded = null;
        }

        private void HandlePersonaStateChange(PersonaStateChange_t change)
        {
            for (int i = 0; i < _pendingAvatars.Count; i++)
            {
                if (_pendingAvatars[i].Player.Value == change.m_ulSteamID)
                {
                    _pendingAvatars[i].HasHeardOfPlayer = true;
                }
            }

            if ((change.m_nChangeFlags & EPersonaChange.k_EPersonaChangeAvatar) != 0)
            {
                ReportAvatarChanged(change.m_ulSteamID);
            }
        }

        private void ReportAvatarChanged(ulong steamId)
        {
            if (!_isDisposed)
            {
                _events.Enqueue(new PendingEvent { Kind = EventKind.AvatarChanged, Player = new PlayerId(steamId) });
            }
        }

        /// <summary>Checks every waiting avatar again, and answers the ones that ran out of time.</summary>
        private void UpdateAvatars()
        {
            if (_pendingAvatars.Count == 0)
            {
                return;
            }

            // A request whose Steam call failed is answered; Update answers the rest then.
            double now = Time.realtimeSinceStartupAsDouble;
            int i = 0;
            while (i < _pendingAvatars.Count && IsAvailable)
            {
                PendingAvatar pending = _pendingAvatars[i];
                bool isAnswered = TryAnswerAvatar(pending);
                if (!isAnswered && now - pending.StartedAt > AvatarTimeoutSeconds)
                {
                    AnswerAvatar(pending, MultiplayerReason.Timeout, default);
                    isAnswered = true;
                }

                if (isAnswered)
                {
                    _pendingAvatars.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        }

        private void FailAvatars(MultiplayerReason reason)
        {
            for (int i = 0; i < _pendingAvatars.Count; i++)
            {
                AnswerAvatar(_pendingAvatars[i], reason, default);
            }

            _pendingAvatars.Clear();
        }

        /// <summary>
        /// Answers the request when Steam has the image or cannot have one, or when a Steam call
        /// failed (<see cref="MultiplayerReason.BackendError"/>); false while Steam still downloads.
        /// </summary>
        private bool TryAnswerAvatar(PendingAvatar pending)
        {
            try
            {
                return TryReadAvatar(pending);
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                // Each Steam call comes before the answer, so this answers the request once.
                Break(cause);
                AnswerAvatar(pending, MultiplayerReason.BackendError, default);
                return true;
            }
        }

        private bool TryReadAvatar(PendingAvatar pending)
        {
            CSteamID player = new CSteamID(pending.Player.Value);
            int image = GetAvatarImage(player, pending.Size);

            // Steam downloads the image and hands out its handle once it has it.
            if (image == -1)
            {
                return false;
            }

            if (image == 0)
            {
                // Steam does not know the player well enough yet. Asked, it starts to download
                // what it knows of them, or answers that it knows everything already and the
                // player has no image. Asking again while it downloads would only start the same
                // download again, so the request waits for the image, and asks once more after
                // Steam told what it knows of the player and still hands out no image, so a player
                // without one answers then instead of at the timeout.
                bool asksAgain = pending.HasRequestedInformation && pending.HasHeardOfPlayer && !pending.HasAskedAgain;
                if (pending.HasRequestedInformation && !asksAgain)
                {
                    return false;
                }

                pending.HasRequestedInformation = true;
                pending.HasAskedAgain |= asksAgain;
                UserInformationRequestCount++;
                if (SteamFriends.RequestUserInformation(player, false))
                {
                    return false;
                }

                AnswerAvatar(pending, MultiplayerReason.BackendError, default);
                return true;
            }

            MultiplayerReason reason = ReadAvatar(image, out BackendAvatar avatar);
            AnswerAvatar(pending, reason, avatar);
            return true;
        }

        private static int GetAvatarImage(CSteamID player, AvatarSize size)
        {
            switch (size)
            {
                case AvatarSize.Small: return SteamFriends.GetSmallFriendAvatar(player);
                case AvatarSize.Medium: return SteamFriends.GetMediumFriendAvatar(player);
                default: return SteamFriends.GetLargeFriendAvatar(player);
            }
        }

        /// <summary>Copies one of Steam's images out, RGBA32 with the top row first, as Steam keeps it.</summary>
        private static MultiplayerReason ReadAvatar(int image, out BackendAvatar avatar)
        {
            avatar = default;
            if (!SteamUtils.GetImageSize(image, out uint width, out uint height)
                || width == 0 || height == 0 || width > MaxAvatarSide || height > MaxAvatarSide)
            {
                return MultiplayerReason.BackendError;
            }

            byte[] pixels = new byte[(int)(width * height * 4)];
            if (!SteamUtils.GetImageRGBA(image, pixels, pixels.Length))
            {
                return MultiplayerReason.BackendError;
            }

            avatar = new BackendAvatar((int)width, (int)height, pixels);
            return MultiplayerReason.None;
        }

        private void AnswerAvatar(PendingAvatar pending, MultiplayerReason reason, BackendAvatar avatar)
        {
            Action<MultiplayerReason, BackendAvatar> onCompleted = pending.OnCompleted;
            EnqueueCallback(() => onCompleted?.Invoke(reason, avatar));
        }
    }
}
