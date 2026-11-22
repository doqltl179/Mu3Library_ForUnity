namespace Mu3Library.Save
{
    public static class SaveLimits
    {
        /// <summary>
        /// Longest slot ID or section key, in characters.
        /// </summary>
        public const int MaxKeyLength = 64;

        /// <summary>
        /// Default for <see cref="SaveManager.MaxSlotSize"/>: 8 MiB, the whole slot with every section.
        /// </summary>
        public const int DefaultMaxSlotSize = 8 * 1024 * 1024;

        /// <summary>
        /// Whether <paramref name="slotId"/> can name a slot: 1 to <see cref="MaxKeyLength"/> letters,
        /// digits, <c>_</c>, and <c>-</c>, so it is a valid file name on every platform.
        /// </summary>
        public static bool IsValidSlotId(string slotId) => IsValid(slotId, allowDot: false);

        /// <summary>
        /// Whether <paramref name="key"/> can name a section: 1 to <see cref="MaxKeyLength"/> letters,
        /// digits, <c>_</c>, <c>-</c>, and <c>.</c>.
        /// </summary>
        public static bool IsValidSectionKey(string key) => IsValid(key, allowDot: true);



        private static bool IsValid(string value, bool allowDot)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaxKeyLength)
            {
                return false;
            }

            foreach (char c in value)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
                    c == '_' || c == '-' || (allowDot && c == '.');
                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
