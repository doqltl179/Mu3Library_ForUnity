using System;
using Mu3Library.Multiplayer.Backend;
using Steamworks;

namespace Mu3Library.Steam.Multiplayer
{
    public sealed partial class SteamBackend : IVoiceBackend
    {
        /// <summary>
        /// The sample rate a backend made without Steam names, and the one the backend decodes at
        /// should Steam's optimal rate lie outside what <c>DecompressVoice</c> takes.
        /// </summary>
        internal const int DefaultVoiceSampleRate = 24000;

        private const int MinVoiceSampleRate = 11025;
        private const int MaxVoiceSampleRate = 48000;

        // What GetVoice reads into at first; it grows when Steam has more ready.
        private const int VoiceReadBytes = 8 * 1024;

        private readonly int _voiceSampleRate = DefaultVoiceSampleRate;
        private bool _isRecordingVoice = false;

        // After StopVoiceRecording, Steam records a moment longer, until GetAvailableVoice
        // answers that it no longer records.
        private bool _isDrainingVoice = false;

        private byte[] _voiceRead = null;
        private byte[] _voiceCompressed = Array.Empty<byte>();
        private byte[] _voicePcm = Array.Empty<byte>();
        private float[] _voiceSamples = Array.Empty<float>();

        /// <summary>
        /// The rate Steam decodes voice at with the least work (<c>GetVoiceOptimalSampleRate</c>),
        /// read once as the backend is created.
        /// </summary>
        public int VoiceSampleRate => _voiceSampleRate;

        public bool IsRecordingVoice => _isRecordingVoice && IsAvailable;

        /// <summary>
        /// Starts Steam voice (<c>SteamUser.StartVoiceRecording</c>) and tells Steam that this
        /// player speaks in the game (<c>SteamFriends.SetInGameVoiceSpeaking</c>), which Steam
        /// documents as keeping its own voice chat from taking the microphone. False once the
        /// backend is no longer available. Steam records with the microphone and the settings the
        /// player chose in Steam.
        /// </summary>
        public bool StartVoiceRecording()
        {
            if (!IsAvailable)
            {
                return false;
            }

            if (_isRecordingVoice)
            {
                return true;
            }

            try
            {
                SteamUser.StartVoiceRecording();

                // Set before the next call, so a backend that breaks there stops the recording.
                _isRecordingVoice = true;
                _isDrainingVoice = false;
                SteamFriends.SetInGameVoiceSpeaking(new CSteamID(LocalPlayerId.Value), true);
                return true;
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
                return false;
            }
        }

        /// <summary>
        /// Stops Steam voice and tells Steam that this player no longer speaks in the game. Steam
        /// records a moment longer, which <see cref="TryReadVoice"/> still hands out, in the frames
        /// it has some, until Steam answers that it no longer records.
        /// </summary>
        public void StopVoiceRecording()
        {
            if (!_isRecordingVoice)
            {
                return;
            }

            _isRecordingVoice = false;
            _isDrainingVoice = true;
            if (!IsAvailable)
            {
                _isDrainingVoice = false;
                return;
            }

            try
            {
                SteamUser.StopVoiceRecording();
                SteamFriends.SetInGameVoiceSpeaking(new CSteamID(LocalPlayerId.Value), false);
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
            }
        }

        /// <summary>
        /// Hands out the compressed voice Steam recorded since the last call
        /// (<c>GetAvailableVoice</c>, then <c>GetVoice</c>), usually a few hundred bytes a frame
        /// while the player speaks and nothing while they are silent.
        /// </summary>
        public bool TryReadVoice(out ReadOnlySpan<byte> voice)
        {
            voice = default;
            if (!(_isRecordingVoice || _isDrainingVoice) || !IsAvailable)
            {
                return false;
            }

            try
            {
                EVoiceResult result = SteamUser.GetAvailableVoice(out uint available);
                if (result == EVoiceResult.k_EVoiceResultNotRecording)
                {
                    _isDrainingVoice = false;
                    return false;
                }

                if (result != EVoiceResult.k_EVoiceResultOK || available == 0)
                {
                    return false;
                }

                if (_voiceRead == null || _voiceRead.Length < available)
                {
                    _voiceRead = new byte[Math.Max(VoiceReadBytes, (int)available)];
                }

                result = SteamUser.GetVoice(true, _voiceRead, (uint)_voiceRead.Length, out uint written);
                if (result != EVoiceResult.k_EVoiceResultOK || written == 0)
                {
                    return false;
                }

                voice = new ReadOnlySpan<byte>(_voiceRead, 0, (int)written);
                return true;
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
                return false;
            }
        }

        /// <summary>
        /// Decodes compressed Steam voice of any player with <c>DecompressVoice</c> at
        /// <see cref="VoiceSampleRate"/>. False for data Steam cannot decode and once the backend
        /// is no longer available.
        /// </summary>
        public bool TryDecodeVoice(ReadOnlySpan<byte> voice, out ReadOnlySpan<float> samples)
        {
            samples = default;
            if (voice.IsEmpty || !IsAvailable)
            {
                return false;
            }

            if (_voiceCompressed.Length < voice.Length)
            {
                _voiceCompressed = new byte[voice.Length];
            }

            voice.CopyTo(_voiceCompressed);
            if (_voicePcm.Length == 0)
            {
                // A second of 16-bit samples; it grows when Steam names more.
                _voicePcm = new byte[_voiceSampleRate * 2];
            }

            uint written;
            try
            {
                EVoiceResult result = Decompress(voice.Length, out written);
                if (result == EVoiceResult.k_EVoiceResultBufferTooSmall && written > _voicePcm.Length)
                {
                    _voicePcm = new byte[written];
                    result = Decompress(voice.Length, out written);
                }

                if (result != EVoiceResult.k_EVoiceResultOK || written < 2)
                {
                    return false;
                }
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
                return false;
            }

            // Steam decodes to 16-bit signed little-endian samples.
            int count = (int)written / 2;
            if (_voiceSamples.Length < count)
            {
                _voiceSamples = new float[count];
            }

            for (int i = 0; i < count; i++)
            {
                _voiceSamples[i] = (short)(_voicePcm[2 * i] | (_voicePcm[2 * i + 1] << 8)) / 32768f;
            }

            samples = new ReadOnlySpan<float>(_voiceSamples, 0, count);
            return true;
        }

        private EVoiceResult Decompress(int compressedLength, out uint written)
        {
            return SteamUser.DecompressVoice(_voiceCompressed, (uint)compressedLength, _voicePcm, (uint)_voicePcm.Length, out written, (uint)_voiceSampleRate);
        }

        /// <summary>The rate to decode at: Steam's optimal rate, or the default should it lie outside what <c>DecompressVoice</c> takes.</summary>
        internal static int ChooseVoiceSampleRate(uint optimal)
        {
            return optimal >= MinVoiceSampleRate && optimal <= MaxVoiceSampleRate ? (int)optimal : DefaultVoiceSampleRate;
        }

        /// <summary>
        /// Stops a recording the backend started when <paramref name="steamRuns"/>, inside the
        /// guard, as <see cref="Dispose"/> and <see cref="Break"/> do; then forgets it either way.
        /// </summary>
        private void ShutdownVoice(bool steamRuns)
        {
            bool wasRecording = _isRecordingVoice;
            _isRecordingVoice = false;
            _isDrainingVoice = false;
            if (!wasRecording || !steamRuns)
            {
                return;
            }

            try
            {
                SteamUser.StopVoiceRecording();
                SteamFriends.SetInGameVoiceSpeaking(new CSteamID(LocalPlayerId.Value), false);
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
            }
        }
    }
}
