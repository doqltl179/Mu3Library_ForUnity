namespace Mu3Library.Multiplayer.Data
{
    public readonly struct MultiplayerResult
    {
        public MultiplayerReason Reason { get; }
        public bool IsSuccess => Reason == MultiplayerReason.None;

        public static MultiplayerResult Success => default;

        public MultiplayerResult(MultiplayerReason reason)
        {
            Reason = reason;
        }

        public static MultiplayerResult Failure(MultiplayerReason reason) => new MultiplayerResult(reason);

        public override string ToString() => IsSuccess ? "Success" : Reason.ToString();
    }
}
