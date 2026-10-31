using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mu3Library.Multiplayer
{
    public partial class MultiplayerManager
    {
        private struct OwnedAvatar
        {
            public Texture2D Texture;

            /// <summary>True for another player's avatar that arrived while they were in the session.</summary>
            public bool EndsWithSession;
        }

        // The textures this manager made, one per player and size. It owns them and destroys them.
        private readonly Dictionary<(PlayerId Player, AvatarSize Size), OwnedAvatar> _avatars =
            new Dictionary<(PlayerId Player, AvatarSize Size), OwnedAvatar>();

        // The callbacks that wait for an avatar the backend still reads, so one read serves them all.
        private readonly Dictionary<(PlayerId Player, AvatarSize Size), List<Action<AvatarResult>>> _avatarWaiters =
            new Dictionary<(PlayerId Player, AvatarSize Size), List<Action<AvatarResult>>>();

        private readonly List<(PlayerId Player, AvatarSize Size)> _endingAvatars = new List<(PlayerId Player, AvatarSize Size)>();



        public void LoadAvatar(PlayerId player, AvatarSize size, Action<AvatarResult> callback)
        {
            if (callback == null)
            {
                Debug.LogWarning("[Multiplayer] LoadAvatar needs a callback.");
                return;
            }

            if (!_isInitialized || _isDisposed)
            {
                Invoke(callback, new AvatarResult(MultiplayerReason.NotInitialized, player, size, null));
                return;
            }

            if (!player.IsValid || size < AvatarSize.Small || size > AvatarSize.Large)
            {
                Invoke(callback, new AvatarResult(MultiplayerReason.InvalidArgument, player, size, null));
                return;
            }

            // A texture someone destroyed despite the rule is made again.
            (PlayerId, AvatarSize) key = (player, size);
            if (_avatars.TryGetValue(key, out OwnedAvatar owned) && owned.Texture != null)
            {
                Invoke(callback, new AvatarResult(MultiplayerReason.None, player, size, owned.Texture));
                return;
            }

            // A backend that is gone never answers, so nothing may wait for it.
            if (!_backend.IsAvailable)
            {
                Invoke(callback, new AvatarResult(MultiplayerReason.BackendError, player, size, null));
                return;
            }

            if (_avatarWaiters.TryGetValue(key, out List<Action<AvatarResult>> waiters))
            {
                waiters.Add(callback);
                return;
            }

            _avatarWaiters.Add(key, new List<Action<AvatarResult>> { callback });
            _backend.LoadAvatar(player, size, (reason, avatar) => HandleAvatarLoaded(player, size, reason, avatar));
        }

        private void HandleAvatarLoaded(PlayerId player, AvatarSize size, MultiplayerReason reason, BackendAvatar avatar)
        {
            (PlayerId, AvatarSize) key = (player, size);
            if (_isDisposed || !_avatarWaiters.Remove(key, out List<Action<AvatarResult>> waiters))
            {
                return;
            }

            Texture2D texture = null;
            if (reason == MultiplayerReason.None)
            {
                texture = CreateAvatarTexture(player, size, avatar);
                if (texture == null)
                {
                    reason = MultiplayerReason.BackendError;
                }
                else
                {
                    if (_avatars.TryGetValue(key, out OwnedAvatar previous))
                    {
                        DestroyTexture(previous.Texture);
                    }

                    _avatars[key] = new OwnedAvatar
                    {
                        Texture = texture,
                        EndsWithSession = player != LocalPlayerId && IndexOfParticipant(player) >= 0,
                    };
                }
            }
            else if (reason != MultiplayerReason.NotSupported)
            {
                Debug.LogWarning($"[Multiplayer] The {size} avatar of {player} could not be loaded ({reason}).");
            }

            AvatarResult result = new AvatarResult(reason, player, size, texture);
            int generation = _sessionGeneration;
            for (int i = 0; i < waiters.Count && !_isDisposed; i++)
            {
                // A handler that ended the session destroyed the texture with the session's other
                // avatars, so the callbacks after it ask again, as if they came now. A destroyed
                // texture compares equal to null, so the reason tells that one was made.
                if (reason == MultiplayerReason.None && generation != _sessionGeneration)
                {
                    for (int rest = i; rest < waiters.Count && !_isDisposed; rest++)
                    {
                        LoadAvatar(player, size, waiters[rest]);
                    }

                    return;
                }

                Invoke(waiters[i], result);
            }
        }

        private static Texture2D CreateAvatarTexture(PlayerId player, AvatarSize size, BackendAvatar avatar)
        {
            byte[] pixels = avatar.Pixels;
            if (avatar.Width <= 0 || avatar.Height <= 0 || pixels == null || pixels.Length != (long)avatar.Width * avatar.Height * 4)
            {
                Debug.LogWarning($"[Multiplayer] The backend gave the {size} avatar of {player} as {avatar.Width}x{avatar.Height} pixels in {pixels?.Length ?? 0} bytes.");
                return null;
            }

            // The backend hands the rows over from the top down, and a texture starts at the bottom.
            FlipRows(pixels, avatar.Width * 4, avatar.Height);

            Texture2D texture = new Texture2D(avatar.Width, avatar.Height, TextureFormat.RGBA32, false)
            {
                name = $"Avatar {player} {size}",
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixelData(pixels, 0);
            texture.Apply(false);
            return texture;
        }

        /// <summary>Turns an image upside down in place, one row of <paramref name="rowBytes"/> at a time.</summary>
        internal static void FlipRows(byte[] pixels, int rowBytes, int rows)
        {
            for (int top = 0, bottom = rows - 1; top < bottom; top++, bottom--)
            {
                int topStart = top * rowBytes;
                int bottomStart = bottom * rowBytes;
                for (int i = 0; i < rowBytes; i++)
                {
                    byte swapped = pixels[topStart + i];
                    pixels[topStart + i] = pixels[bottomStart + i];
                    pixels[bottomStart + i] = swapped;
                }
            }
        }

        /// <summary>
        /// Destroys the avatars of other players that arrived while they were in the session that
        /// now ends, those who left before it ended included.
        /// </summary>
        private void ReleaseSessionAvatars()
        {
            foreach (KeyValuePair<(PlayerId Player, AvatarSize Size), OwnedAvatar> pair in _avatars)
            {
                if (pair.Value.EndsWithSession)
                {
                    _endingAvatars.Add(pair.Key);
                }
            }

            for (int i = 0; i < _endingAvatars.Count; i++)
            {
                (PlayerId, AvatarSize) key = _endingAvatars[i];
                DestroyTexture(_avatars[key].Texture);
                _avatars.Remove(key);
            }

            _endingAvatars.Clear();
        }

        /// <summary>Destroys every avatar and drops the requests still waiting, as the manager is disposed.</summary>
        private void ReleaseAvatars()
        {
            foreach (OwnedAvatar owned in _avatars.Values)
            {
                DestroyTexture(owned.Texture);
            }

            _avatars.Clear();
            _avatarWaiters.Clear();
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            // Edit mode, which the tests run in, forbids the deferred destroy.
            if (Application.isPlaying)
            {
                Object.Destroy(texture);
            }
            else
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
