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
        }

        // Steam has an avatar it downloads within a second or two; past this the request answers
        // itself, so every request answers once.
        private const double AvatarTimeoutSeconds = 10d;

        // Steam's avatars are at most 184 pixels wide; anything far larger is not an avatar.
        private const uint MaxAvatarSide = 4096;

        // Requests that wait for Steam to download what it needs, asked again on every update.
        private readonly List<PendingAvatar> _pendingAvatars = new List<PendingAvatar>();

        /// <summary>How many requests wait for Steam to download an avatar, for the integration tests.</summary>
        internal int PendingAvatarCount => _pendingAvatars.Count;



        /// <summary>
        /// Reads the avatar Steam holds for the player. Steam knows the avatars of friends and of
        /// the players it shares a lobby with; for anyone else it downloads them first, and while
        /// it downloads, the backend asks again on every update until Steam has the image or tells
        /// that the player has none. The answer arrives from <see cref="Update"/> in any case:
        /// <see cref="MultiplayerReason.BackendError"/> when Steam knows the player but has no
        /// image, and also once the game shut Steam down, for a request made then and for one
        /// still waiting, and <see cref="MultiplayerReason.Timeout"/> when it has none within 10
        /// seconds.
        /// </summary>
        /// <remarks>
        /// An avatar answers <see cref="MultiplayerReason.BackendError"/> once Steam is gone, not
        /// the <see cref="MultiplayerReason.SteamNotRunning"/> a lobby search answers then, so the
        /// game sees one reason for it: the manager answers <see cref="MultiplayerReason.BackendError"/>
        /// itself while a backend is not available, since a disposed backend answers nothing, and
        /// it cannot tell a Steam that shut down from a backend that was disposed.
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

        /// <summary>Asks Steam again for every waiting avatar, and answers the ones that ran out of time.</summary>
        private void UpdateAvatars()
        {
            if (_pendingAvatars.Count == 0)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            int i = 0;
            while (i < _pendingAvatars.Count)
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

        /// <summary>Answers the request when Steam has the image or cannot have one; false while Steam still downloads.</summary>
        private bool TryAnswerAvatar(PendingAvatar pending)
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
                // Steam does not know the player well enough yet and starts to download what it
                // knows of them, or already knows everything, and the player has no image.
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
