using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Config
{
    /// <summary>
    /// The sounds the board plays, through <see cref="Mu3Library.Audio.AudioManager"/>.
    /// <br/> Every clip is optional and a moment without one stays silent, so a board can be
    /// <br/> configured with only the sounds a project already has.
    /// </summary>
    [System.Serializable]
    public class BoardSoundConfig
    {
        private static readonly AudioClip[] EmptyClips = new AudioClip[0];

        [Tooltip("The volume every board sound effect is played at.\nIt is passed to the AudioManager per sound, which scales it with its own SFX and master volume.")]
        [SerializeField, Range(0.0f, 1.0f)] protected float _sfxVolume = 1.0f;
        /// <summary>
        /// The volume every board sound effect is played at, 0 to 1.
        /// </summary>
        public float SfxVolume => Mathf.Clamp01(_sfxVolume);

        [Tooltip("The BGM volume the board asks for.\nIt is only applied to the AudioManager the board creates for itself; an AudioManager the project assigns keeps the BGM volume it already has.")]
        [SerializeField, Range(0.0f, 1.0f)] protected float _bgmVolume = 0.8f;
        /// <summary>
        /// The BGM volume the board asks for, 0 to 1.
        /// </summary>
        public float BgmVolume => Mathf.Clamp01(_bgmVolume);

        [Space(20)]
        [Tooltip("Optional. Played as a playlist while the game is running, started on game start and stopped on game end.\nEmpty entries are skipped.")]
        [SerializeField] protected AudioClip[] _bgmClips = new AudioClip[0];
        /// <summary>
        /// The tracks the board BGM playlist is built from, empty while none are set.
        /// </summary>
        public IReadOnlyList<AudioClip> BgmClips => _bgmClips ?? EmptyClips;

        [Tooltip("Plays the tracks in a random order, reshuffled on every cycle.")]
        [SerializeField] protected bool _bgmShuffle;
        public bool BgmShuffle => _bgmShuffle;

        [Tooltip("Seconds of silence between two tracks.")]
        [SerializeField, Min(0.0f)] protected float _bgmTrackInterval = 1.0f;
        public float BgmTrackInterval => Mathf.Max(0.0f, _bgmTrackInterval);

        [Tooltip("How many times the whole playlist is played.\n0 or less repeats it for as long as the game runs.")]
        [SerializeField] protected int _bgmLoopCount = -1;
        /// <summary>
        /// The playlist cycles to play, 0 or less for as long as the game runs.
        /// </summary>
        public int BgmLoopCount => _bgmLoopCount;

        [Space(20)]
        [Tooltip("Optional. Played once when the board starts running.")]
        [SerializeField] protected AudioClip _gameStartClip;
        public AudioClip GameStartClip => _gameStartClip;

        [Tooltip("Optional. Played once when the board overflows and the game ends.")]
        [SerializeField] protected AudioClip _gameEndClip;
        public AudioClip GameEndClip => _gameEndClip;

        [Tooltip("Optional. Played when the player releases the item they are holding.")]
        [SerializeField] protected AudioClip _itemDropClip;
        public AudioClip ItemDropClip => _itemDropClip;

        [Space(20)]
        [Tooltip("Optional. Played when two items finish merging.\nThe first merge plays the first clip, and every merge that follows it within the combo interval steps one clip further until the last one is reached.")]
        [SerializeField] protected AudioClip[] _itemMergeClips = new AudioClip[0];
        /// <summary>
        /// The merge clips, ordered by combo step, empty while none are set.
        /// </summary>
        public IReadOnlyList<AudioClip> ItemMergeClips => _itemMergeClips ?? EmptyClips;

        [Tooltip("Seconds a merge keeps the combo alive.\nA merge that comes later starts the combo over at the first clip.")]
        [SerializeField, Min(0.0f)] protected float _mergeComboInterval = 5.0f;
        /// <summary>
        /// How long a merge keeps the combo alive, in seconds.
        /// </summary>
        public float MergeComboInterval => Mathf.Max(0.0f, _mergeComboInterval);

        /// <summary>
        /// The last combo step that still picks a different clip, 0 while no merge clip is set.
        /// </summary>
        public int MergeComboIndexMax => Mathf.Max(0, (_itemMergeClips?.Length ?? 0) - 1);



        /// <summary>
        /// True when the playlist has at least one track to play.
        /// </summary>
        public bool HasBgmClip
        {
            get
            {
                if (_bgmClips == null)
                {
                    return false;
                }

                for (int index = 0; index < _bgmClips.Length; index++)
                {
                    if (_bgmClips[index] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// The clip configured for a board moment, null while no clip is set for it.
        /// </summary>
        public AudioClip GetClip(BoardSoundType soundType) => soundType switch
        {
            BoardSoundType.GameStart => _gameStartClip,
            BoardSoundType.GameEnd => _gameEndClip,
            BoardSoundType.ItemDrop => _itemDropClip,
            _ => null,
        };

        /// <summary>
        /// True when a board moment has a clip to play.
        /// </summary>
        public bool HasClip(BoardSoundType soundType) => GetClip(soundType) != null;

        /// <summary>
        /// The merge clip a combo step plays. A step past the last clip keeps playing that one,
        /// <br/> so a long combo stays at its loudest instead of falling silent.
        /// </summary>
        public AudioClip GetMergeClip(int comboIndex)
        {
            if (_itemMergeClips == null || _itemMergeClips.Length == 0)
            {
                return null;
            }

            return _itemMergeClips[Mathf.Clamp(comboIndex, 0, _itemMergeClips.Length - 1)];
        }

        /// <summary>
        /// The playlist tracks as the array the audio manager takes, null while none are set.
        /// <br/> The empty entries are dropped here, the audio manager reports every one it is
        /// <br/> handed, and a list that is still being filled in carries plenty of them.
        /// </summary>
        internal AudioClip[] GetBgmClipArray()
        {
            if (_bgmClips == null)
            {
                return null;
            }

            int clipCount = 0;
            for (int index = 0; index < _bgmClips.Length; index++)
            {
                if (_bgmClips[index] != null)
                {
                    clipCount++;
                }
            }

            if (clipCount == 0)
            {
                return null;
            }

            if (clipCount == _bgmClips.Length)
            {
                return _bgmClips;
            }

            AudioClip[] clips = new AudioClip[clipCount];
            int clipIndex = 0;
            for (int index = 0; index < _bgmClips.Length; index++)
            {
                if (_bgmClips[index] != null)
                {
                    clips[clipIndex++] = _bgmClips[index];
                }
            }

            return clips;
        }
    }
}
