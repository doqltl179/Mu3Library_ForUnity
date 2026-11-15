using Mu3Library.Audio;
using Mu3Library.Game.WatermelonGame.Board.Config;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    public partial class BoardController
    {
        private const float DefaultSfxVolume = 1.0f;
        private const float DefaultBgmVolume = 0.8f;

        /// <summary>
        /// The audio manager the board built for itself. It is created with the first sound the
        /// <br/> board actually plays, so a configuration without any clip never builds one, and the
        /// <br/> board drives and disposes it because nothing else knows about it.
        /// </summary>
        private AudioManager m_ownedAudioManager;

        private IAudioManager _assignedAudioManager;
        /// <summary>
        /// The audio manager board sounds are played through.
        /// <br/> Assign the one a project already runs to share its volumes and its instance limit;
        /// <br/> the board then leaves its lifetime alone. Null gives the board one of its own again.
        /// </summary>
        public IAudioManager AudioManager
        {
            get => _assignedAudioManager ?? m_ownedAudioManager;
            set
            {
                if (ReferenceEquals(_assignedAudioManager, value))
                {
                    return;
                }

                // The board BGM belongs to the manager that was playing it.
                StopBoardBgm();

                _assignedAudioManager = value;

                if (value != null)
                {
                    DisposeOwnedAudioManager();
                }
            }
        }

        /// <summary>
        /// The sounds the active board configuration carries, null while no configuration is applied.
        /// </summary>
        public BoardSoundConfig SoundConfig => _boardConfig != null ? _boardConfig.SoundConfig : null;

        private float _sfxVolume = DefaultSfxVolume;
        /// <summary>
        /// The volume every board sound effect is played at, 0 to 1. A value outside that range
        /// <br/> is clamped into it.
        /// <br/> It is passed to the audio manager per sound, which scales it with its own SFX and
        /// <br/> master volume, so a project that already keeps a volume setting can hand it here
        /// <br/> without the board touching the volumes of a manager it shares.
        /// </summary>
        public float SfxVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Mathf.Clamp01(value);
        }

        private float _bgmVolume = DefaultBgmVolume;
        /// <summary>
        /// The BGM volume the board asks for, 0 to 1. A value outside that range is clamped into it.
        /// <br/> It is only applied to the audio manager the board creates for itself, and takes
        /// <br/> effect right away while that one is playing; an audio manager the project assigns
        /// <br/> keeps the BGM volume it already has.
        /// </summary>
        public float BgmVolume
        {
            get => _bgmVolume;
            set
            {
                _bgmVolume = Mathf.Clamp01(value);

                if (_assignedAudioManager == null && m_ownedAudioManager != null)
                {
                    m_ownedAudioManager.BgmVolume = _bgmVolume;
                }
            }
        }

        /// <summary>
        /// True while the board is the one playing the BGM playlist, so it only ever stops a
        /// <br/> playlist it started itself on a manager it shares with the rest of the project.
        /// </summary>
        protected bool _isBoardBgmPlaying;

        /// <summary>
        /// The merge clip the next merge in the running combo plays, -1 while no combo is running.
        /// </summary>
        protected int _mergeComboIndex = -1;
        /// <summary>
        /// The combo step the last merge played, -1 while no combo is running.
        /// <br/> It counts up on every merge that lands within
        /// <see cref="BoardSoundConfig.MergeComboInterval"/> of the one before it, and stops at
        /// <see cref="BoardSoundConfig.MergeComboIndexMax"/>.
        /// </summary>
        public int MergeComboIndex => _mergeComboIndex;

        /// <summary>
        /// When the last merge was counted into the combo.
        /// </summary>
        protected float _lastMergeSoundTime = float.NegativeInfinity;

        #region Sound Effect
        /// <summary>
        /// Plays the sound the board configuration carries for a board moment.
        /// <br/> A moment without a clip stays silent.
        /// </summary>
        protected virtual void PlayBoardSound(BoardSoundType soundType)
        {
            BoardSoundConfig soundConfig = SoundConfig;
            if (soundConfig == null)
            {
                return;
            }

            PlayBoardSoundEffect(soundConfig.GetClip(soundType));
        }

        /// <summary>
        /// Plays the merge sound of the combo step this merge lands on.
        /// <br/> The first merge plays the first clip, and every merge that follows it within
        /// <see cref="BoardSoundConfig.MergeComboInterval"/> steps one clip further, so a chain
        /// <br/> reaction climbs the list instead of repeating the same sound.
        /// </summary>
        protected virtual void PlayItemMergeSound()
        {
            BoardSoundConfig soundConfig = SoundConfig;
            if (soundConfig == null)
            {
                return;
            }

            bool continuesCombo = _mergeComboIndex >= 0 &&
                Time.time - _lastMergeSoundTime <= soundConfig.MergeComboInterval;

            int previousComboIndex = _mergeComboIndex;

            _mergeComboIndex = continuesCombo
                ? Mathf.Min(_mergeComboIndex + 1, soundConfig.MergeComboIndexMax)
                : 0;
            _lastMergeSoundTime = Time.time;

            // A combo that already reached its last step keeps running on the same clip,
            // so there is nothing new to report.
            if (_mergeComboIndex != previousComboIndex)
            {
                OnMergeComboChanged?.Invoke(_mergeComboIndex);
            }

            PlayBoardSoundEffect(soundConfig.GetMergeClip(_mergeComboIndex));
        }

        /// <summary>
        /// Drops the running merge combo, so the next merge starts over at the first clip.
        /// </summary>
        protected void ResetMergeCombo()
        {
            bool hadCombo = _mergeComboIndex >= 0;

            _mergeComboIndex = -1;
            _lastMergeSoundTime = float.NegativeInfinity;

            if (hadCombo)
            {
                OnMergeComboChanged?.Invoke(_mergeComboIndex);
            }
        }

        private void PlayBoardSoundEffect(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            IAudioManager audioManager = ResolveAudioManager();
            if (audioManager == null)
            {
                return;
            }

            // The board asks for its own volume instead of changing the manager's SFX volume,
            // which the rest of the project shares when the manager was assigned from outside.
            AudioSourceSettings settings = AudioSourceSettings.SfxStandard;
            settings.Volume = _sfxVolume;

            audioManager.PlaySfx(clip, settings);
        }
        #endregion

        #region BGM
        /// <summary>
        /// Starts the board BGM playlist, unless the configuration carries no track.
        /// </summary>
        protected virtual void StartBoardBgm()
        {
            BoardSoundConfig soundConfig = SoundConfig;
            if (soundConfig == null)
            {
                return;
            }

            AudioClip[] clips = soundConfig.GetBgmClipArray();
            if (clips == null)
            {
                return;
            }

            IAudioManager audioManager = ResolveAudioManager();
            if (audioManager == null)
            {
                return;
            }

            // Only the board's own manager is tuned, an assigned one keeps the volumes
            // the project set on it.
            if (m_ownedAudioManager != null && ReferenceEquals(audioManager, m_ownedAudioManager))
            {
                m_ownedAudioManager.BgmVolume = _bgmVolume;
            }

            audioManager.PlayBgmPlaylist(
                clips,
                soundConfig.BgmLoopCount,
                soundConfig.BgmShuffle,
                soundConfig.BgmTrackInterval);

            _isBoardBgmPlaying = true;
        }

        /// <summary>
        /// Stops the board BGM playlist, and only that one. A playlist the board never started
        /// <br/> belongs to the project, so a shared manager is left playing.
        /// </summary>
        protected virtual void StopBoardBgm()
        {
            if (!_isBoardBgmPlaying)
            {
                return;
            }

            _isBoardBgmPlaying = false;

            IAudioManager audioManager = AudioManager;
            audioManager?.StopBgmPlaylist();
        }
        #endregion

        /// <summary>
        /// The assigned manager, or the board's own one, which is built with the first sound
        /// <br/> the board plays.
        /// </summary>
        private IAudioManager ResolveAudioManager()
            => _assignedAudioManager ?? (m_ownedAudioManager ??= new AudioManager());

        /// <summary>
        /// Drives the board's own manager. It is a plain class and not a MonoBehaviour, so nothing
        /// <br/> else advances its SFX pooling and its BGM playlist. An assigned manager is driven
        /// <br/> by whoever owns it.
        /// </summary>
        private void UpdateOwnedAudioManager()
        {
            m_ownedAudioManager?.Update();
        }

        private void DisposeOwnedAudioManager()
        {
            m_ownedAudioManager?.Dispose();
            m_ownedAudioManager = null;
        }
    }
}
