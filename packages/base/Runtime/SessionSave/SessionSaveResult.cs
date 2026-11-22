using Mu3Library.Multiplayer.Data;
using Mu3Library.Save;

namespace Mu3Library.SessionSave
{
    /// <summary>
    /// How a session save call ended. A failure of the save system carries its
    /// <see cref="SaveResult"/>, and a message that could not leave its <see cref="MultiplayerReason"/>.
    /// </summary>
    public readonly struct SessionSaveResult
    {
        public SessionSaveFailureReason Reason { get; }

        /// <summary>What the save system answered, for <see cref="SessionSaveFailureReason.SaveFailed"/>.</summary>
        public SaveResult Save { get; }

        /// <summary>Why the message could not leave, for <see cref="SessionSaveFailureReason.SendFailed"/>.</summary>
        public MultiplayerReason Multiplayer { get; }

        public bool IsSuccess => Reason == SessionSaveFailureReason.None;

        public static SessionSaveResult Success => default;



        private SessionSaveResult(SessionSaveFailureReason reason, SaveResult save, MultiplayerReason multiplayer)
        {
            Reason = reason;
            Save = save;
            Multiplayer = multiplayer;
        }

        public static SessionSaveResult Failure(SessionSaveFailureReason reason) =>
            new SessionSaveResult(reason, SaveResult.Success, MultiplayerReason.None);

        public static SessionSaveResult FromSave(SaveResult save) =>
            save.IsSuccess ? Success : new SessionSaveResult(SessionSaveFailureReason.SaveFailed, save, MultiplayerReason.None);

        public static SessionSaveResult FromSend(MultiplayerReason reason) =>
            reason == MultiplayerReason.None ? Success : new SessionSaveResult(SessionSaveFailureReason.SendFailed, SaveResult.Success, reason);

        public override string ToString()
        {
            switch (Reason)
            {
                case SessionSaveFailureReason.None:
                    return "Success";
                case SessionSaveFailureReason.SaveFailed:
                    return $"{Reason}: {Save}";
                case SessionSaveFailureReason.SendFailed:
                    return $"{Reason}: {Multiplayer}";
                default:
                    return Reason.ToString();
            }
        }
    }
}
