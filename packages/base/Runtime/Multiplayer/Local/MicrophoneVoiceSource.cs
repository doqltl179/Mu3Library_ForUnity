using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// Records the default microphone with <c>Microphone.Start</c>. The backends of one process
    /// share the recording: the first that starts starts the microphone, each reads it from where
    /// it started, and the last that stops ends it. It never starts in a WebGL player, whose
    /// <c>UnityEngine.AudioModule</c> on the 6000.3 floor holds no <c>Microphone</c>.
    /// </summary>
#if UNITY_WEBGL && !UNITY_EDITOR
    internal sealed class MicrophoneVoiceSource : ILocalVoiceSource
    {
        public bool Start() => false;

        public void Stop()
        {
        }

        public bool TryReadFrame(Span<float> frame) => false;
    }
#else
    internal sealed class MicrophoneVoiceSource : ILocalVoiceSource
    {
        // The recording loops over one second, which a game that reads once a frame stays within.
        private const int ClipSeconds = 1;

        private static AudioClip s_clip = null;
        private static int s_users = 0;

        private bool _isStarted = false;

        // Where this source reads next, in sample frames of the clip.
        private int _readPosition = 0;

        // 20 milliseconds of the clip at its own frequency and channels.
        private float[] _chunk = Array.Empty<float>();



        public bool Start()
        {
            if (_isStarted)
            {
                return true;
            }

            // Started again when something else ended the recording under the users it had.
            if (s_clip == null || !Microphone.IsRecording(null))
            {
                if (Microphone.devices.Length == 0)
                {
                    return false;
                }

                AudioClip clip = Microphone.Start(null, true, ClipSeconds, LocalVoice.SampleRate);
                if (clip == null)
                {
                    return false;
                }

                DestroyClip(s_clip);
                s_clip = clip;
            }

            s_users++;
            _isStarted = true;
            _readPosition = Math.Max(0, Microphone.GetPosition(null));
            _chunk = new float[Math.Max(1, s_clip.frequency / 50) * s_clip.channels];
            return true;
        }

        public void Stop()
        {
            if (!_isStarted)
            {
                return;
            }

            _isStarted = false;
            s_users--;
            if (s_users > 0)
            {
                return;
            }

            s_users = 0;
            Microphone.End(null);
            DestroyClip(s_clip);
            s_clip = null;
        }

        public bool TryReadFrame(Span<float> frame)
        {
            if (!_isStarted || s_clip == null)
            {
                return false;
            }

            int position = Microphone.GetPosition(null);
            int length = s_clip.samples;
            int channels = s_clip.channels;
            int chunkFrames = _chunk.Length / channels;
            if (position < 0 || position >= length || chunkFrames > length)
            {
                return false;
            }

            int recorded = position - _readPosition;
            if (recorded < 0)
            {
                recorded += length;
            }

            if (recorded < chunkFrames)
            {
                return false;
            }

            // Read in two parts where the frame wraps past the end of the looping clip.
            int first = Math.Min(chunkFrames, length - _readPosition);
            s_clip.GetData(_chunk.AsSpan(0, first * channels), _readPosition);
            if (first < chunkFrames)
            {
                s_clip.GetData(_chunk.AsSpan(first * channels), 0);
            }

            _readPosition = (_readPosition + chunkFrames) % length;
            Resample(_chunk, chunkFrames, channels, frame);
            return true;
        }

        /// <summary>
        /// Maps <paramref name="frames"/> sample frames of <paramref name="chunk"/> onto
        /// <paramref name="frame"/>, mixing the channels down and interpolating between samples,
        /// so a microphone that records at another frequency still gives 20 milliseconds at
        /// <see cref="LocalVoice.SampleRate"/>.
        /// </summary>
        internal static void Resample(float[] chunk, int frames, int channels, Span<float> frame)
        {
            float step = frame.Length > 1 && frames > 1 ? (frames - 1) / (float)(frame.Length - 1) : 0f;
            for (int i = 0; i < frame.Length; i++)
            {
                float at = i * step;
                int index = Math.Min((int)at, frames - 1);
                int next = Math.Min(index + 1, frames - 1);
                float weight = at - index;
                frame[i] = Mono(chunk, index, channels) * (1f - weight) + Mono(chunk, next, channels) * weight;
            }
        }

        private static float Mono(float[] chunk, int index, int channels)
        {
            float sum = 0f;
            for (int channel = 0; channel < channels; channel++)
            {
                sum += chunk[index * channels + channel];
            }

            return sum / channels;
        }

        private static void DestroyClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            // Edit mode forbids the deferred destroy.
            if (Application.isPlaying)
            {
                Object.Destroy(clip);
            }
            else
            {
                Object.DestroyImmediate(clip);
            }
        }
    }
#endif
}
