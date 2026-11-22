using System;

namespace Mu3Library.Multiplayer.Backend
{
    /// <summary>
    /// An optional capability of an <see cref="IMultiplayerBackend"/> that records this player's
    /// voice and decodes the voice of other players, as Steam voice does on the Steam backend and
    /// the microphone does on <see cref="Local.LocalBackend"/>. Reach it by casting the backend
    /// the game created, as in <c>backend as IVoiceBackend</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The backend only records and decodes; the game carries the voice. It sends what
    /// <see cref="TryReadVoice"/> hands out as a game message, usually
    /// <see cref="Data.DeliveryMode.Unreliable"/>, to the players it picks with a
    /// <see cref="Data.MessageTarget"/>, or lets the host pick, for each player, who hears it with
    /// <see cref="IMultiplayerMessenger.SetRelayFilter"/>. Each player decodes what arrives with
    /// <see cref="TryDecodeVoice"/> and plays it through a <see cref="VoicePlayer"/> on an
    /// <c>AudioSource</c> of its own for each speaker, whose settings decide how the voice sounds
    /// in the scene.
    /// </para>
    /// <para>
    /// Every player of a session runs the same kind of backend, so voice that one backend encoded
    /// is decoded by the same kind of backend. Every call runs on the main thread.
    /// </para>
    /// </remarks>
    public interface IVoiceBackend
    {
        /// <summary>
        /// The sample rate, in hertz, of the mono samples <see cref="TryDecodeVoice"/> hands out,
        /// which a <see cref="VoicePlayer"/> for the voice is created with.
        /// </summary>
        public int VoiceSampleRate { get; }

        /// <summary>True from <see cref="StartVoiceRecording"/> until <see cref="StopVoiceRecording"/>, or until the backend is no longer available.</summary>
        public bool IsRecordingVoice { get; }

        /// <summary>
        /// Starts recording this player's voice, which <see cref="TryReadVoice"/> then hands out.
        /// True also while it already records. False when it cannot, for example when there is no
        /// microphone or the backend is no longer available.
        /// </summary>
        public bool StartVoiceRecording();

        /// <summary>
        /// Stops recording. Steam goes on a moment after the call, so
        /// <see cref="TryReadVoice"/> may still hand out the end of what this player said: keep
        /// calling it once a frame, as while recording, and send what it hands out. It hands out
        /// nothing more once the backend finished recording.
        /// </summary>
        public void StopVoiceRecording();

        /// <summary>
        /// Hands out the encoded voice recorded since the last call, or false when there is none.
        /// The data stays valid until the next call; send it before then. Call it once a frame
        /// while recording, so the voice leaves in small parts.
        /// </summary>
        public bool TryReadVoice(out ReadOnlySpan<byte> voice);

        /// <summary>
        /// Decodes voice that another player's backend handed out with <see cref="TryReadVoice"/>
        /// into mono samples from -1 to 1 at <see cref="VoiceSampleRate"/>, or answers false for
        /// data it cannot decode, empty data included. The samples stay valid until the next call;
        /// write them to the speaker's <see cref="VoicePlayer"/> before then.
        /// </summary>
        public bool TryDecodeVoice(ReadOnlySpan<byte> voice, out ReadOnlySpan<float> samples);
    }
}
