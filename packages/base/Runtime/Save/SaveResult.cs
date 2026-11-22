namespace Mu3Library.Save
{
    /// <summary>
    /// How a save, load, or storage call ended. A failure names the section it concerns, when one,
    /// and carries the exception message that caused it, when one did.
    /// </summary>
    public readonly struct SaveResult
    {
        public SaveFailureReason Reason { get; }
        public string SectionKey { get; }
        public string Message { get; }
        public bool IsSuccess => Reason == SaveFailureReason.None;

        public static SaveResult Success => default;



        public SaveResult(SaveFailureReason reason, string sectionKey = null, string message = null)
        {
            Reason = reason;
            SectionKey = sectionKey;
            Message = message;
        }

        public static SaveResult Failure(SaveFailureReason reason, string sectionKey = null, string message = null) =>
            new SaveResult(reason, sectionKey, message);

        public override string ToString()
        {
            if (IsSuccess)
            {
                return "Success";
            }

            string text = SectionKey != null ? $"{Reason} ({SectionKey})" : Reason.ToString();
            return string.IsNullOrEmpty(Message) ? text : $"{text}: {Message}";
        }
    }
}
