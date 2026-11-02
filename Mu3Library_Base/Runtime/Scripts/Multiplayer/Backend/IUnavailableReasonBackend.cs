using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Backend
{
    /// <summary>
    /// A backend that names why it is not available, as the Steam backend tells a Steam that can
    /// no longer be used in this process from a backend that was disposed. The manager gives that
    /// reason for a lobby it cannot create because the backend is not available.
    /// </summary>
    internal interface IUnavailableReasonBackend
    {
        /// <summary>
        /// Why the backend is not available, read only while
        /// <see cref="IMultiplayerBackend.IsAvailable"/> is false;
        /// <see cref="MultiplayerReason.None"/> when the backend names no reason.
        /// </summary>
        MultiplayerReason UnavailableReason { get; }
    }
}
