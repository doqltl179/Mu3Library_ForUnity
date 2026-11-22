namespace Mu3Library.Steam.Stats
{
    /// <summary>How a call of <see cref="ISteamStatsManager"/> ended.</summary>
    public readonly struct SteamStatsResult
    {
        public SteamStatsFailureReason Reason { get; }
        public string Message { get; }
        public bool IsSuccess => Reason == SteamStatsFailureReason.None;

        public static SteamStatsResult Success => default;



        public SteamStatsResult(SteamStatsFailureReason reason, string message = null)
        {
            Reason = reason;
            Message = message;
        }

        public static SteamStatsResult Failure(SteamStatsFailureReason reason, string message = null) =>
            new SteamStatsResult(reason, message);

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
