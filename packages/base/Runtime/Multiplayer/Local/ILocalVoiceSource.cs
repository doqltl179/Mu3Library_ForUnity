using System;

namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// Where <see cref="LocalBackend"/> records voice from: the microphone
    /// (<see cref="MicrophoneVoiceSource"/>), or in the tests samples they hand in.
    /// </summary>
    internal interface ILocalVoiceSource
    {
        /// <summary>Starts recording; false when it cannot.</summary>
        public bool Start();

        public void Stop();

        /// <summary>
        /// Fills <paramref name="frame"/>, <see cref="LocalVoice.FrameSamples"/> long, with the
        /// next 20 milliseconds of mono samples at <see cref="LocalVoice.SampleRate"/>, or answers
        /// false while less than that has been recorded since the last frame.
        /// </summary>
        public bool TryReadFrame(Span<float> frame);
    }
}
