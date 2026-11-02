using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Unity.Collections;
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

        /// <summary>
        /// The reads that redraw one avatar the manager holds after the backend told it changed,
        /// numbered in the order the manager asked for them.
        /// </summary>
        private struct AvatarRefresh
        {
            /// <summary>The first read, so one asked for before the avatar was released and held anew counts for nothing.</summary>
            public int First;

            /// <summary>The latest read: after the latest change, or a failed one tried again. Only its failure is tried again.</summary>
            public int Latest;

            /// <summary>The newest read whose image the texture took, so an earlier one that answers after it changes nothing.</summary>
            public int Drawn;

            /// <summary>How many of the reads are still on their way.</summary>
            public int Reading;

            /// <summary>How many times a failed read after the latest change was tried again.</summary>
            public int Retries;

            /// <summary>When the failed latest read is tried again; NaN while none waits.</summary>
            public double RetryAt;
        }

        // How many times a failed read after a change is tried again, and how long after each failure.
        private const int AvatarRefreshRetries = 3;
        private const double AvatarRefreshRetrySeconds = 1d;

        // The textures this manager made, one per player and size. It owns them and destroys them.
        private readonly Dictionary<(PlayerId Player, AvatarSize Size), OwnedAvatar> _avatars =
            new Dictionary<(PlayerId Player, AvatarSize Size), OwnedAvatar>();

        // The callbacks that wait for an avatar the backend still reads, so one read serves them all.
        private readonly Dictionary<(PlayerId Player, AvatarSize Size), List<Action<AvatarResult>>> _avatarWaiters =
            new Dictionary<(PlayerId Player, AvatarSize Size), List<Action<AvatarResult>>>();

        private readonly List<(PlayerId Player, AvatarSize Size)> _endingAvatars = new List<(PlayerId Player, AvatarSize Size)>();

        // The reads that redraw a held avatar after the backend told it changed, for each player
        // and size while any is on its way or waits to be tried again (see HandleAvatarRefreshed).
        private readonly Dictionary<(PlayerId Player, AvatarSize Size), AvatarRefresh> _avatarRefreshes =
            new Dictionary<(PlayerId Player, AvatarSize Size), AvatarRefresh>();
        private readonly List<(PlayerId Player, AvatarSize Size)> _dueAvatarRefreshes = new List<(PlayerId Player, AvatarSize Size)>();
        private int _lastAvatarRefresh = 0;

        private readonly ManagerEvent<Action<AvatarResult>> _onAvatarChanged = new ManagerEvent<Action<AvatarResult>>(nameof(OnAvatarChanged));

        public event Action<AvatarResult> OnAvatarChanged { add => _onAvatarChanged.Add(value); remove => _onAvatarChanged.Remove(value); }



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

            // A backend that is gone never answers, so nothing may wait for it, and the requests
            // that already do are answered first.
            if (!_backend.IsAvailable)
            {
                FailAvatarWaiters();
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
            for (int i = 0; i < waiters.Count && !_isDisposed; i++)
            {
                // A handler that ended the session or released the avatar destroyed the texture,
                // so the callbacks after it ask again, as if they came now. In play mode Unity
                // destroys it only at the end of the frame, so the manager tells by whether it
                // still holds that texture, not by comparing it to null.
                if (reason == MultiplayerReason.None && !HoldsAvatar(key, texture))
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

        /// <summary>Answers the avatar requests still waiting once the backend is no longer available.</summary>
        private void UpdateAvatarWaiters()
        {
            if (_avatarWaiters.Count > 0 && !_backend.IsAvailable)
            {
                FailAvatarWaiters();
            }
        }

        /// <summary>
        /// Answers every request that waits for the backend with <see cref="MultiplayerReason.BackendError"/>,
        /// as a backend that is no longer available never answers them, so no game callback stays
        /// held until the manager is disposed. A backend that answers after all finds nothing waiting.
        /// </summary>
        private void FailAvatarWaiters()
        {
            if (_avatarWaiters.Count == 0)
            {
                return;
            }

            // Emptied before any callback runs, so a callback that asks again is answered at once
            // instead of joining a request that is being failed.
            List<KeyValuePair<(PlayerId Player, AvatarSize Size), List<Action<AvatarResult>>>> failing =
                new List<KeyValuePair<(PlayerId Player, AvatarSize Size), List<Action<AvatarResult>>>>(_avatarWaiters);
            _avatarWaiters.Clear();

            for (int i = 0; i < failing.Count && !_isDisposed; i++)
            {
                (PlayerId player, AvatarSize size) = failing[i].Key;
                AvatarResult result = new AvatarResult(MultiplayerReason.BackendError, player, size, null);
                List<Action<AvatarResult>> waiters = failing[i].Value;
                for (int w = 0; w < waiters.Count && !_isDisposed; w++)
                {
                    Invoke(waiters[w], result);
                }
            }
        }

        /// <summary>
        /// Reads again every avatar the manager holds of the player, once the backend told that
        /// it changed, and draws each new image into the texture the game already has (see
        /// <see cref="HandleAvatarRefreshed"/>). A request still waiting for the backend needs
        /// nothing: the backend reads the image when it answers.
        /// </summary>
        private void HandleAvatarChanged(PlayerId player)
        {
            if (_isDisposed || !player.IsValid || !_backend.IsAvailable)
            {
                return;
            }

            // Gathered first, so a backend that answers inside the request, against its contract,
            // and a handler that releases avatars then cannot change what is being looked through.
            List<AvatarSize> sizes = null;
            foreach (KeyValuePair<(PlayerId Player, AvatarSize Size), OwnedAvatar> pair in _avatars)
            {
                if (pair.Key.Player == player && pair.Value.Texture != null)
                {
                    sizes ??= new List<AvatarSize>(3);
                    sizes.Add(pair.Key.Size);
                }
            }

            for (int i = 0; sizes != null && i < sizes.Count && !_isDisposed; i++)
            {
                ReadAvatarAgain((player, sizes[i]), isRetry: false);
            }
        }

        /// <summary>
        /// Asks the backend again for an avatar the manager holds: after a change, which starts its
        /// tries anew, or to try a failed read again.
        /// </summary>
        private void ReadAvatarAgain((PlayerId Player, AvatarSize Size) key, bool isRetry)
        {
            int read = ++_lastAvatarRefresh;
            if (!_avatarRefreshes.TryGetValue(key, out AvatarRefresh refresh))
            {
                refresh.First = read;
            }

            refresh.Latest = read;
            refresh.Reading++;
            refresh.RetryAt = double.NaN;
            if (!isRetry)
            {
                refresh.Retries = 0;
            }

            // Stored before the request, so a backend that answers inside it, against its contract, finds it.
            _avatarRefreshes[key] = refresh;
            (PlayerId player, AvatarSize size) = key;
            _backend.LoadAvatar(player, size, (reason, avatar) => HandleAvatarRefreshed(player, size, read, reason, avatar));
        }

        /// <summary>
        /// Draws the image a read after a change brought into the texture the manager holds, so a
        /// texture the game shows changes with it and stays the same object, and raises
        /// <see cref="OnAvatarChanged"/> when the image is a new one. Of reads that overlap, the
        /// texture takes the image of the latest one that brought an image: an earlier read that
        /// answers after it changes nothing, and a later read that fails leaves the image an earlier
        /// one brought. When the latest read fails, the manager tries it again up to
        /// <see cref="AvatarRefreshRetries"/> times, <see cref="AvatarRefreshRetrySeconds"/> after
        /// each failure, while the backend is available, as a failed read would otherwise leave the
        /// default image Steam hands out first for an account it did not know until Steam told of
        /// another change. It warns once it gives up, and the texture keeps the last image it took.
        /// An image that arrives broken counts as no image, and is not read again, since that cannot
        /// mend a backend that hands it over so. An avatar the game released meanwhile, or that ended with its session,
        /// stays released.
        /// </summary>
        private void HandleAvatarRefreshed(PlayerId player, AvatarSize size, int read, MultiplayerReason reason, BackendAvatar avatar)
        {
            (PlayerId, AvatarSize) key = (player, size);
            if (_isDisposed || !_avatarRefreshes.TryGetValue(key, out AvatarRefresh refresh) || read < refresh.First)
            {
                return;
            }

            if (!_avatars.TryGetValue(key, out OwnedAvatar owned) || owned.Texture == null)
            {
                _avatarRefreshes.Remove(key);
                return;
            }

            refresh.Reading--;

            // A broken image brought no image, so it neither takes the place of the image an
            // earlier read brings nor hides it.
            bool draws = reason == MultiplayerReason.None && read > refresh.Drawn && IsUsableAvatar(player, size, avatar);
            if (draws)
            {
                refresh.Drawn = read;
            }
            else if (read == refresh.Latest && reason != MultiplayerReason.None && reason != MultiplayerReason.NotSupported)
            {
                // A later change may have brought another image, so the latest read is tried again
                // even when an earlier one brought an image.
                if (refresh.Retries < AvatarRefreshRetries && _backend.IsAvailable)
                {
                    refresh.Retries++;
                    refresh.RetryAt = Now + AvatarRefreshRetrySeconds;
                }
                else
                {
                    int tries = refresh.Retries + 1;
                    Debug.LogWarning($"[Multiplayer] The {size} avatar of {player} changed but could not be read again ({reason}) in {tries} {(tries == 1 ? "try" : "tries")}; its texture keeps the last image it took.");
                }
            }

            if (refresh.Reading == 0 && double.IsNaN(refresh.RetryAt))
            {
                _avatarRefreshes.Remove(key);
            }
            else
            {
                _avatarRefreshes[key] = refresh;
            }

            // Last, as a handler may release the avatar or end the session.
            if (draws && RedrawAvatarTexture(owned.Texture, avatar))
            {
                Raise(_onAvatarChanged, new AvatarResult(MultiplayerReason.None, player, size, owned.Texture));
            }
        }

        /// <summary>
        /// Tries the failed reads after a change again once their time comes, and forgets every read
        /// once the backend is no longer available, since it answers none of them then. It allocates
        /// nothing while no read is due.
        /// </summary>
        private void UpdateAvatarRefreshes()
        {
            if (_avatarRefreshes.Count == 0)
            {
                return;
            }

            if (!_backend.IsAvailable)
            {
                _avatarRefreshes.Clear();
                return;
            }

            double now = Now;
            foreach (KeyValuePair<(PlayerId Player, AvatarSize Size), AvatarRefresh> pair in _avatarRefreshes)
            {
                // NaN, for no read to try again, is never due.
                if (pair.Value.RetryAt <= now)
                {
                    _dueAvatarRefreshes.Add(pair.Key);
                }
            }

            // Gathered first, so a backend that answers inside a request, against its contract, and
            // a handler that releases avatars then cannot change what is being looked through.
            for (int i = 0; i < _dueAvatarRefreshes.Count && !_isDisposed; i++)
            {
                (PlayerId Player, AvatarSize Size) key = _dueAvatarRefreshes[i];
                if (_avatarRefreshes.TryGetValue(key, out AvatarRefresh refresh) && refresh.RetryAt <= now)
                {
                    ReadAvatarAgain(key, isRetry: true);
                }
            }

            _dueAvatarRefreshes.Clear();
        }

        private static Texture2D CreateAvatarTexture(PlayerId player, AvatarSize size, BackendAvatar avatar)
        {
            if (!IsUsableAvatar(player, size, avatar))
            {
                return null;
            }

            // The backend hands the rows over from the top down, and a texture starts at the bottom.
            byte[] pixels = avatar.Pixels;
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

        /// <summary>
        /// Draws a usable image of the backend (see <see cref="IsUsableAvatar"/>) into a texture
        /// the manager made, resized to it when it has another size. False, with the texture left
        /// as it was, for the image the texture already shows.
        /// </summary>
        private static bool RedrawAvatarTexture(Texture2D texture, BackendAvatar avatar)
        {
            byte[] pixels = avatar.Pixels;
            FlipRows(pixels, avatar.Width * 4, avatar.Height);

            bool isSameSize = texture.width == avatar.Width && texture.height == avatar.Height;
            if (isSameSize && HasPixels(texture, pixels))
            {
                return false;
            }

            if (!isSameSize)
            {
                texture.Reinitialize(avatar.Width, avatar.Height, TextureFormat.RGBA32, false);
            }

            texture.SetPixelData(pixels, 0);
            texture.Apply(false);
            return true;
        }

        private static bool HasPixels(Texture2D texture, byte[] pixels)
        {
            NativeArray<byte> current = texture.GetPixelData<byte>(0);
            return current.AsReadOnlySpan().SequenceEqual(pixels);
        }

        private static bool IsUsableAvatar(PlayerId player, AvatarSize size, BackendAvatar avatar)
        {
            byte[] pixels = avatar.Pixels;
            if (avatar.Width <= 0 || avatar.Height <= 0 || pixels == null || pixels.Length != (long)avatar.Width * avatar.Height * 4)
            {
                Debug.LogWarning($"[Multiplayer] The backend gave the {size} avatar of {player} as {avatar.Width}x{avatar.Height} pixels in {pixels?.Length ?? 0} bytes.");
                return false;
            }

            return true;
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

        public void ReleaseAvatar(PlayerId player)
        {
            if (!player.IsValid)
            {
                return;
            }

            foreach ((PlayerId Player, AvatarSize Size) key in _avatars.Keys)
            {
                if (key.Player == player)
                {
                    _endingAvatars.Add(key);
                }
            }

            DestroyEndingAvatars();
        }

        public void ReleaseAvatars()
        {
            // A player of the session now keeps theirs, also one whose avatar arrived before they
            // joined, such as the host of a lobby that search found, so a game that releases the
            // avatars of its lobby list once it joined one keeps the ones the lobby shows.
            PlayerId self = LocalPlayerId;
            foreach ((PlayerId Player, AvatarSize Size) key in _avatars.Keys)
            {
                if (key.Player != self && IndexOfParticipant(key.Player) < 0)
                {
                    _endingAvatars.Add(key);
                }
            }

            DestroyEndingAvatars();
        }

        private bool HoldsAvatar((PlayerId Player, AvatarSize Size) key, Texture2D texture)
        {
            return _avatars.TryGetValue(key, out OwnedAvatar owned) && ReferenceEquals(owned.Texture, texture);
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

            DestroyEndingAvatars();
        }

        /// <summary>
        /// Destroys and forgets the avatars listed in <see cref="_endingAvatars"/>, with the reads
        /// after a change that would redraw them.
        /// </summary>
        private void DestroyEndingAvatars()
        {
            for (int i = 0; i < _endingAvatars.Count; i++)
            {
                (PlayerId, AvatarSize) key = _endingAvatars[i];
                DestroyTexture(_avatars[key].Texture);
                _avatars.Remove(key);
                _avatarRefreshes.Remove(key);
            }

            _endingAvatars.Clear();
        }

        /// <summary>
        /// Destroys every avatar, drops the requests and reads still waiting, and lets go of the
        /// <see cref="OnAvatarChanged"/> handlers, as the manager is disposed.
        /// </summary>
        private void DisposeAvatars()
        {
            foreach (OwnedAvatar owned in _avatars.Values)
            {
                DestroyTexture(owned.Texture);
            }

            _avatars.Clear();
            _avatarWaiters.Clear();
            _avatarRefreshes.Clear();
            _onAvatarChanged.Clear();
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
