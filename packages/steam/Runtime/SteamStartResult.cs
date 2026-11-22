namespace Mu3Library.Steam
{
    /// <summary>How <see cref="SteamManager.TryStart"/> ended.</summary>
    public readonly struct SteamStartResult
    {
        public SteamStartFailureReason Reason { get; }

        /// <summary>What Steam or the runtime answered for a failure, or null.</summary>
        public string Message { get; }

        public bool IsSuccess => Reason == SteamStartFailureReason.None;

        public static SteamStartResult Success => default;



        public SteamStartResult(SteamStartFailureReason reason, string message = null)
        {
            Reason = reason;
            Message = message;
        }

        public static SteamStartResult Failure(SteamStartFailureReason reason, string message = null) =>
            new SteamStartResult(reason, message);

        public override string ToString()
        {
            if (IsSuccess)
            {
                return "Success";
            }

            return string.IsNullOrEmpty(Message) ? Reason.ToString() : $"{Reason}: {Message}";
        }
    }
}
