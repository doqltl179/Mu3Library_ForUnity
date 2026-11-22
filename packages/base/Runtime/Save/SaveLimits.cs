using System;

namespace Mu3Library.Save
{
    public static class SaveLimits
    {
        /// <summary>
        /// Longest slot ID, section key, or metadata key, in characters.
        /// </summary>
        public const int MaxKeyLength = 64;

        /// <summary>
        /// Most metadata entries one slot holds.
        /// </summary>
        public const int MaxMetadataEntries = 32;

        /// <summary>
        /// Longest metadata value, in characters.
        /// </summary>
        public const int MaxMetadataValueLength = 256;

        /// <summary>
        /// Default for <see cref="SaveManager.MaxSlotSize"/>: 8 MiB, the whole slot with every section.
        /// </summary>
        public const int DefaultMaxSlotSize = 8 * 1024 * 1024;

        private static readonly string[] ReservedFileNames =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };



        /// <summary>
        /// Whether <paramref name="slotId"/> can name a slot: 1 to <see cref="MaxKeyLength"/> letters,
        /// digits, <c>_</c>, and <c>-</c>, and none of the names Windows keeps for devices, such as
        /// <c>CON</c>, so it is a file name on every platform. A file system that ignores case, such as
        /// Windows', keeps two IDs that differ only in case as one slot, so pick IDs that differ in more.
        /// </summary>
        public static bool IsValidSlotId(string slotId)
        {
            if (!IsValid(slotId, allowDot: false))
            {
                return false;
            }

            foreach (string reserved in ReservedFileNames)
            {
                if (string.Equals(slotId, reserved, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether <paramref name="key"/> can name a section or a metadata entry: 1 to
        /// <see cref="MaxKeyLength"/> letters, digits, <c>_</c>, <c>-</c>, and <c>.</c>.
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
