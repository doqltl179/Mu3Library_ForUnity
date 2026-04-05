using UnityEngine;

namespace Mu3Library.Audio
{
    public partial class AudioManager
    {
        private AudioClip[] _playlist = null;

        /// <summary>
        /// Indices into <see cref="_playlist"/> in the order they will be played.
        /// Matches the original order when shuffle is off; randomised when on.
        /// </summary>
        private int[] _playlistOrder = null;

        private int _playlistCursor = 0;

        /// <summary>
        /// Remaining full cycles to play. 0 or less means infinite.
        /// </summary>
        private int _playlistRemainingCycles = -1;

        private bool _playlistShuffle = false;
        private float _playlistInterval = 1.0f;
        private bool _isPlaylistActive = false;

        // Interval countdown between tracks
        private bool _isPlaylistWaitingInterval = false;
        private float _playlistIntervalElapsed = 0.0f;



        public void PlayBgmPlaylist(AudioClip[] clips) =>
            PlayBgmPlaylist(clips, loopCount: -1, shuffle: false, interval: 1.0f);

        public void PlayBgmPlaylist(AudioClip[] clips, int loopCount) =>
            PlayBgmPlaylist(clips, loopCount, shuffle: false, interval: 1.0f);

        public void PlayBgmPlaylist(AudioClip[] clips, bool shuffle) =>
            PlayBgmPlaylist(clips, loopCount: -1, shuffle, interval: 1.0f);

        public void PlayBgmPlaylist(AudioClip[] clips, float interval) =>
            PlayBgmPlaylist(clips, loopCount: -1, shuffle: false, interval);

        public void PlayBgmPlaylist(AudioClip[] clips, int loopCount, bool shuffle) =>
            PlayBgmPlaylist(clips, loopCount, shuffle, interval: 1.0f);

        public void PlayBgmPlaylist(AudioClip[] clips, int loopCount, float interval) =>
            PlayBgmPlaylist(clips, loopCount, shuffle: false, interval);

        public void PlayBgmPlaylist(AudioClip[] clips, bool shuffle, float interval) =>
            PlayBgmPlaylist(clips, loopCount: -1, shuffle, interval);

        public void PlayBgmPlaylist(AudioClip[] clips, int loopCount, bool shuffle, float interval)
        {
            if (clips == null || clips.Length == 0)
            {
                Debug.LogError("BGM playlist clips are NULL or empty.");
                return;
            }

            // Stop current BGM (also cancels any previously active playlist).
            StopBgm();

            _playlist = clips;
            _playlistRemainingCycles = loopCount <= 0 ? -1 : loopCount;
            _playlistShuffle = shuffle;
            _playlistInterval = Mathf.Max(0f, interval);
            _playlistCursor = 0;
            _isPlaylistActive = true;
            _isPlaylistWaitingInterval = false;

            BuildPlaylistOrder();
            PlayCurrentPlaylistTrack();
        }

        public void StopBgmPlaylist()
        {
            CancelPlaylist();
            StopBgm();
        }

        /// <summary>
        /// Driven by <see cref="AudioManager.Update"/>. Counts down the inter-track
        /// interval and advances the playlist when the wait is over.
        /// </summary>
        private void UpdatePlaylist()
        {
            if (!_isPlaylistActive || !_isPlaylistWaitingInterval)
            {
                return;
            }

            // Honour BGM pause — don't count down while audio is paused.
            if (_bgmMainController != null && _bgmMainController.IsPaused)
            {
                return;
            }

            _playlistIntervalElapsed += Time.deltaTime;

            if (_playlistIntervalElapsed >= _playlistInterval)
            {
                _isPlaylistWaitingInterval = false;
                AdvancePlaylist();
            }
        }

        /// <summary>
        /// Resets playlist state without touching the BGM controller.
        /// Called by <see cref="StopBgm"/> so that any external stop also
        /// deactivates the playlist.
        /// </summary>
        private void CancelPlaylist()
        {
            if (_bgmMainController != null)
            {
                _bgmMainController.OnCompleted -= OnPlaylistTrackCompleted;
            }

            _isPlaylistActive = false;
            _isPlaylistWaitingInterval = false;
            _playlist = null;
            _playlistOrder = null;
        }

        private void BuildPlaylistOrder()
        {
            _playlistOrder = new int[_playlist.Length];
            for (int i = 0; i < _playlistOrder.Length; i++)
            {
                _playlistOrder[i] = i;
            }

            if (_playlistShuffle)
            {
                ShufflePlaylistOrder();
            }
        }

        private void ShufflePlaylistOrder()
        {
            // Fisher-Yates shuffle — uses System.Random to avoid Unity frame dependency.
            System.Random rng = new System.Random();
            for (int i = _playlistOrder.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_playlistOrder[i], _playlistOrder[j]) = (_playlistOrder[j], _playlistOrder[i]);
            }
        }

        private void PlayCurrentPlaylistTrack()
        {
            if (!_isPlaylistActive || _playlistOrder == null || _playlistCursor >= _playlistOrder.Length)
            {
                return;
            }

            AudioClip clip = _playlist[_playlistOrder[_playlistCursor]];

            if (clip == null)
            {
                Debug.LogWarning($"BGM playlist clip at index {_playlistOrder[_playlistCursor]} is NULL. Skipping.");
                AdvancePlaylist();
                return;
            }

            // Use BGM-standard audio settings but force a single play (loopCount = 1).
            AudioSourceSettings settings = AudioSourceSettings.BgmStandard;
            settings.LoopCount = 1;

            PlayBgm(clip, settings);

            // Subscribe after Play so the handler is registered before completion can fire.
            if (_bgmMainController != null)
            {
                _bgmMainController.OnCompleted -= OnPlaylistTrackCompleted;
                _bgmMainController.OnCompleted += OnPlaylistTrackCompleted;
            }
        }

        private void OnPlaylistTrackCompleted()
        {
            if (!_isPlaylistActive)
            {
                return;
            }

            if (_playlistInterval > 0f)
            {
                _isPlaylistWaitingInterval = true;
                _playlistIntervalElapsed = 0f;
            }
            else
            {
                AdvancePlaylist();
            }
        }

        private void AdvancePlaylist()
        {
            _playlistCursor++;

            if (_playlistCursor >= _playlistOrder.Length)
            {
                // One full cycle completed.
                if (_playlistRemainingCycles > 0)
                {
                    _playlistRemainingCycles--;
                }

                if (_playlistRemainingCycles == 0)
                {
                    // All requested cycles done — clean up.
                    _isPlaylistActive = false;
                    _playlist = null;
                    _playlistOrder = null;
                    return;
                }

                // Infinite (< 0) or still has remaining cycles: wrap and optionally re-shuffle.
                _playlistCursor = 0;

                if (_playlistShuffle)
                {
                    ShufflePlaylistOrder();
                }
            }

            PlayCurrentPlaylistTrack();
        }
    }
}

