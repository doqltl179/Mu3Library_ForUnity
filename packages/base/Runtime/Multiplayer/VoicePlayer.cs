using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// Plays one player's voice through an <see cref="AudioSource"/>: write each part of the
    /// voice that <see cref="Backend.IVoiceBackend.TryDecodeVoice"/> decoded, and call
    /// <see cref="Update"/> once a frame. Create one for each player whose voice this player
    /// hears, on an <see cref="AudioSource"/> of that player's own, such as one on their character,
    /// whose settings (volume, <see cref="AudioSource.spatialBlend"/>, rolloff, mixer group,
    /// <see cref="AudioSource.mute"/>) decide how the voice sounds in the scene.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The player takes the source over: it sets a looping clip of its own as the source's clip,
    /// writes the voice into it, and starts and stops the source itself. It starts once
    /// <see cref="DelaySeconds"/> of voice is buffered, which absorbs the uneven arrival of
    /// unreliable messages, and stops once the source played everything written, when the
    /// speaker stopped talking or their voice stopped arriving; the next voice waits for the
    /// delay again. When voice arrives faster than the source plays it, as after a network stall,
    /// it skips ahead to the newest <see cref="DelaySeconds"/> once more than a quarter second
    /// beyond the delay is buffered, so the voice never falls far behind.
    /// </para>
    /// <para>
    /// Every call runs on the main thread. <see cref="Dispose"/> stops the source, takes the clip
    /// off it, and destroys the clip; dispose the player when its speaker leaves or before its
    /// source is destroyed. A player whose source was destroyed does nothing.
    /// </para>
    /// </remarks>
    public sealed class VoicePlayer : IDisposable
    {
        public const float DefaultDelaySeconds = 0.1f;

        /// <summary>The longest <see cref="DelaySeconds"/>, which the looping clip leaves room for.</summary>
        public const float MaxDelaySeconds = 1f;

        // The clip loops over this much voice, well above the delay, the quarter second the player
        // buffers beyond it, and the silence it writes ahead.
        private const float ClipSeconds = 2f;

        private readonly AudioSource _source;
        private AudioClip _clip;

        // In samples: the clip, the voice the source waits for, and the most it buffers.
        private readonly int _length;
        private readonly int _delay;
        private readonly int _maxBuffered;

        // A tenth of a second written after the voice, so a source that played past it before
        // the next update stops plays silence rather than older voice.
        private readonly float[] _silence;

        // Where the next sample goes, and how many written samples the source has not played.
        private int _write = 0;
        private int _buffered = 0;

        // The source's position at the last look, from which the next look counts what it played.
        private int _lastPlayhead = 0;
        private bool _isPlaying = false;
        private bool _isDisposed = false;

        // Set by the tests, which move the position by hand: edit mode plays no audio.
        private Func<int> _readPlayhead = null;
        private Action<int> _seek = null;

        public AudioSource Source => _source;

        /// <summary>The sample rate of the voice written, as <see cref="Backend.IVoiceBackend.VoiceSampleRate"/> names it.</summary>
        public int SampleRate { get; }

        /// <summary>How much voice the source waits for before it starts.</summary>
        public float DelaySeconds { get; }

        /// <summary>True from when <see cref="DelaySeconds"/> of voice is buffered until the source played all of it.</summary>
        public bool IsPlaying => _isPlaying && !_isDisposed;

        /// <summary>The voice written that the source has not played yet, as of the last write or update.</summary>
        public float BufferedSeconds => _buffered / (float)SampleRate;



        /// <param name="source">The source that plays the voice, which the player takes over.</param>
        /// <param name="sampleRate">The sample rate of the voice, <see cref="Backend.IVoiceBackend.VoiceSampleRate"/>.</param>
        /// <param name="delaySeconds">How much voice to buffer before playing, from 0 to <see cref="MaxDelaySeconds"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="sampleRate"/> is outside 1,000 to 192,000, or <paramref name="delaySeconds"/> outside 0 to <see cref="MaxDelaySeconds"/>.
        /// </exception>
        public VoicePlayer(AudioSource source, int sampleRate, float delaySeconds = DefaultDelaySeconds)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (sampleRate < 1000 || sampleRate > 192000)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "The sample rate must be from 1,000 to 192,000.");
            }

            if (!(delaySeconds >= 0f && delaySeconds <= MaxDelaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), delaySeconds, $"The delay must be from 0 to {MaxDelaySeconds} seconds.");
            }

            _source = source;
            SampleRate = sampleRate;
            DelaySeconds = delaySeconds;

            _length = (int)(sampleRate * ClipSeconds);
            _delay = Math.Max(1, (int)(sampleRate * delaySeconds));
            _maxBuffered = _delay + sampleRate / 4;
            _silence = new float[sampleRate / 10];

            _clip = AudioClip.Create("Voice", _length, 1, sampleRate, false);
            _source.Stop();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.clip = _clip;
        }

        /// <summary>
        /// Buffers decoded voice, mono at <see cref="SampleRate"/>, and starts the source once
        /// <see cref="DelaySeconds"/> of it is buffered. Write the parts in the order they were
        /// recorded; a part longer than the most the player buffers keeps only its end.
        /// </summary>
        public void Write(ReadOnlySpan<float> samples)
        {
            if (_isDisposed || samples.IsEmpty || _source == null || _clip == null)
            {
                return;
            }

            Sync();

            if (samples.Length > _maxBuffered)
            {
                samples = samples.Slice(samples.Length - _maxBuffered);
            }

            WriteAt(_write, samples);
            _write = (_write + samples.Length) % _length;
            WriteAt(_write, _silence);
            _buffered += samples.Length;

            if (!_isPlaying && _buffered >= _delay)
            {
                // The voice was written from the clip's start, where the source starts playing.
                _source.Play();
                _lastPlayhead = 0;
                _isPlaying = true;
            }

            if (_isPlaying && _buffered > _maxBuffered)
            {
                int head = _write - _delay;
                head = head < 0 ? head + _length : head;
                Seek(head);
                _lastPlayhead = head;
                _buffered = _delay;
            }
        }

        /// <summary>
        /// Counts what the source played since the last call, and stops it once it played
        /// everything written. Call it once a frame.
        /// </summary>
        public void Update()
        {
            if (_isDisposed || _source == null || _clip == null)
            {
                return;
            }

            Sync();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _isPlaying = false;
            if (_source != null)
            {
                _source.Stop();
                if (_source.clip == _clip)
                {
                    _source.clip = null;
                }
            }

            if (_clip != null)
            {
                // Edit mode, which the tests run in, forbids the deferred destroy.
                if (Application.isPlaying)
                {
                    Object.Destroy(_clip);
                }
                else
                {
                    Object.DestroyImmediate(_clip);
                }
            }

            _clip = null;
        }

        /// <summary>
        /// Test hook: reads and moves the source's position through these instead of
        /// <see cref="AudioSource.timeSamples"/>, since edit mode plays no audio.
        /// </summary>
        internal void UsePlayheadForTests(Func<int> read, Action<int> seek)
        {
            _readPlayhead = read;
            _seek = seek;
        }

        private void Sync()
        {
            if (!_isPlaying)
            {
                return;
            }

            int head = _readPlayhead != null ? _readPlayhead() : _source.timeSamples;
            int played = head - _lastPlayhead;
            _lastPlayhead = head;
            _buffered -= played < 0 ? played + _length : played;
            if (_buffered > 0)
            {
                return;
            }

            // Played everything: the next voice starts again from the clip's start after the delay.
            _source.Stop();
            _isPlaying = false;
            _buffered = 0;
            _write = 0;
            _lastPlayhead = 0;
        }

        private void Seek(int position)
        {
            if (_seek != null)
            {
                _seek(position);
            }
            else
            {
                _source.timeSamples = position;
            }
        }

        private void WriteAt(int position, ReadOnlySpan<float> samples)
        {
            int first = Math.Min(samples.Length, _length - position);
            _clip.SetData(samples.Slice(0, first), position);
            if (first < samples.Length)
            {
                _clip.SetData(samples.Slice(first), 0);
            }
        }
    }
}
