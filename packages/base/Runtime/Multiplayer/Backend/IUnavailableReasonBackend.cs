using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Backend
{
    /// <summary>
    /// An optional capability of an <see cref="IMultiplayerBackend"/> that names why
    /// <see cref="IMultiplayerBackend.IsAvailable"/> turned false, as the Steam backend tells a
    /// Steam that can no longer be used in this process from a backend that was disposed. The
    /// <see cref="MultiplayerManager"/> gives that reason for a session it ends, a request it
    /// answers itself, such as a lobby search or an avatar request, and a lobby it cannot create
    /// because the backend is not available.
    /// </summary>
    public interface IUnavailableReasonBackend
    {
        /// <summary>
        /// Why the backend is not available. The manager reads it only while
        /// <see cref="IMultiplayerBackend.IsAvailable"/> is false.
        /// <see cref="MultiplayerReason.None"/> names no reason and keeps the manager's own:
        /// <see cref="MultiplayerReason.BackendError"/> for a session it ends or a request it
        /// answers itself, and <see cref="MultiplayerReason.ConnectionFailed"/> for a lobby it
        /// cannot create.
        /// </summary>
        public MultiplayerReason UnavailableReason { get; }
    }
}
