using System;
using Mu3Library.Multiplayer.Backend;

namespace Mu3Library.Multiplayer.Local
{
    public sealed partial class LocalBackend : IVoiceBackend
    {
        private ILocalVoiceSource _voiceSource = new MicrophoneVoiceSource();
        private bool _isRecordingVoice = false;
        private readonly float[] _voiceFrame = new float[LocalVoice.FrameSamples];
        private byte[] _voiceRead = null;
        private float[] _voiceDecoded = Array.Empty<float>();

        /// <summary>16,000 Hz.</summary>
        public int VoiceSampleRate => LocalVoice.SampleRate;

        public bool IsRecordingVoice => _isRecordingVoice && IsAvailable;

        /// <summary>
        /// Starts recording the default microphone, which stands in for Steam voice, so the same
        /// game code records, sends, and plays voice in the editor and between the processes of
        /// one computer. False when no microphone is connected, in a WebGL player, which has no
        /// <c>Microphone</c>, and once the backend is no longer available.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The backend records with <c>Microphone.Start</c> at 16 kHz mono and encodes each sample
        /// as one mu-law byte, so a second of voice takes 16,000 bytes; set
        /// <see cref="Data.SessionRules.MaxBytesPerSecond"/> with that in mind while testing. <see cref="TryReadVoice"/> hands out whole 20-millisecond frames, at most half
        /// a second of them at a time. The backends of one process share the microphone, and each
        /// hears all of it.
        /// </para>
        /// <para>
        /// Recording stops at once with <see cref="StopVoiceRecording"/>, and what it recorded and
        /// <see cref="TryReadVoice"/> did not hand out yet is dropped. Disposing the backend stops
        /// it too. <see cref="TryDecodeVoice"/> decodes voice that any <see cref="LocalBackend"/>
        /// read, also after the backend is disposed.
        /// </para>
        /// </remarks>
        public bool StartVoiceRecording()
        {
            if (!IsAvailable)
            {
                return false;
            }

            if (!_isRecordingVoice)
            {
                _isRecordingVoice = _voiceSource.Start();
            }

            return _isRecordingVoice;
        }

        public void StopVoiceRecording()
        {
            if (!_isRecordingVoice)
            {
                return;
            }

            _isRecordingVoice = false;
            _voiceSource.Stop();
        }

        public bool TryReadVoice(out ReadOnlySpan<byte> voice)
        {
            voice = default;
            if (!IsRecordingVoice)
            {
                return false;
            }

            _voiceRead ??= new byte[LocalVoice.FrameSamples * LocalVoice.MaxFramesPerRead];
            int length = 0;
            while (length < _voiceRead.Length && _voiceSource.TryReadFrame(_voiceFrame))
            {
                LocalVoice.Encode(_voiceFrame, _voiceRead.AsSpan(length, LocalVoice.FrameSamples));
                length += LocalVoice.FrameSamples;
            }

            if (length == 0)
            {
                return false;
            }

            voice = new ReadOnlySpan<byte>(_voiceRead, 0, length);
            return true;
        }

        public bool TryDecodeVoice(ReadOnlySpan<byte> voice, out ReadOnlySpan<float> samples)
        {
            samples = default;
            if (voice.IsEmpty)
            {
                return false;
            }

            if (_voiceDecoded.Length < voice.Length)
            {
                _voiceDecoded = new float[Math.Max(voice.Length, LocalVoice.FrameSamples * LocalVoice.MaxFramesPerRead)];
            }

            LocalVoice.Decode(voice, _voiceDecoded);
            samples = new ReadOnlySpan<float>(_voiceDecoded, 0, voice.Length);
            return true;
        }

        /// <summary>
        /// Test hook: records from <paramref name="source"/> instead of the microphone, which the
        /// machines that run the tests may lack.
        /// </summary>
        internal void UseVoiceSourceForTests(ILocalVoiceSource source)
        {
            StopVoiceRecording();
            _voiceSource = source;
        }
    }
}
