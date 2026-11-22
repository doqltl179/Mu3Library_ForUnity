using System;

namespace Mu3Library.SessionSave
{
    /// <summary>
    /// How shares of a session save go. Every share that <see cref="ISessionSaveManager.Share"/>
    /// started on the host, or that <see cref="OnShareOffered"/> reported on the receiving player,
    /// ends with <see cref="OnShareCompleted"/> or <see cref="OnShareFailed"/> on that player. An
    /// offer the receiving player turns down by itself, because it is already receiving one or the
    /// save is too large for it, raises nothing there; the host hears why.
    /// </summary>
    public interface ISessionSaveManagerEventBus
    {
        /// <summary>
        /// The host offers this player a save. Answer with <see cref="ISessionSaveManager.AcceptShare"/>
        /// or <see cref="ISessionSaveManager.DeclineShare"/>, here or later.
        /// </summary>
        public event Action<SaveShareOffer> OnShareOffered;

        /// <summary>A part of a save left the host, or reached the receiving player.</summary>
        public event Action<SaveShareTransfer> OnShareProgress;

        /// <summary>
        /// The receiving player stored the save: on that player once it is stored, on the host once
        /// it hears so.
        /// </summary>
        public event Action<SaveShareTransfer> OnShareCompleted;

        /// <summary>The share ended without the save being stored, for the reason given.</summary>
        public event Action<SaveShareTransfer, SaveShareFailureReason> OnShareFailed;
    }
}
