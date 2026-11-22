using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mu3Library.Save
{
    /// <summary>
    /// The bytes of one slot: a header with the format version, the slot's metadata, every section's
    /// key, version, time, and serialized data, and a CRC-32 of everything before it, so a damaged or
    /// foreign file is told apart from a save. Reading checks every length against the limits before
    /// it allocates, since an imported slot can come from another player.
    /// </summary>
    internal sealed class SaveSlotFile
    {
        internal const int FormatVersion = 1;

        private static readonly byte[] Magic = { (byte)'M', (byte)'U', (byte)'3', (byte)'S' };
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        // Magic, format version, created and updated ticks, metadata and section counts, and the CRC.
        private const int MinimumSize = 4 + 4 + 8 + 8 + 4 + 4 + 4;
        private const int FormatVersionOffset = 4;

        public long CreatedTicks;
        public long UpdatedTicks;
        public readonly SortedDictionary<string, string> Metadata = new(StringComparer.Ordinal);
        public readonly List<Entry> Sections = new();



        internal sealed class Entry
        {
            public string Key;
            public int Version;
            public long UpdatedTicks;
            public byte[] Data;
        }



        public Entry Find(string key)
        {
            foreach (Entry entry in Sections)
            {
                if (entry.Key == key)
                {
                    return entry;
                }
            }

            return null;
        }

        public void Set(Entry entry)
        {
            for (int i = 0; i < Sections.Count; i++)
            {
                if (Sections[i].Key == entry.Key)
                {
                    Sections[i] = entry;
                    return;
                }
            }

            Sections.Add(entry);
        }

        public bool Remove(string key) => Sections.RemoveAll(entry => entry.Key == key) > 0;

        public byte[] ToBytes()
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Utf8, leaveOpen: true))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(CreatedTicks);
                writer.Write(UpdatedTicks);
                writer.Write(Metadata.Count);
                foreach (KeyValuePair<string, string> pair in Metadata)
                {
                    WriteKey(writer, pair.Key);
                    byte[] value = Utf8.GetBytes(pair.Value);
                    writer.Write((ushort)value.Length);
                    writer.Write(value);
                }

                writer.Write(Sections.Count);
                foreach (Entry entry in Sections)
                {
                    WriteKey(writer, entry.Key);
                    writer.Write(entry.Version);
                    writer.Write(entry.UpdatedTicks);
                    writer.Write(entry.Data.Length);
                    writer.Write(entry.Data);
                }
            }

            uint crc = Crc32.Compute(stream.GetBuffer(), 0, (int)stream.Length);
            using (var writer = new BinaryWriter(stream, Utf8, leaveOpen: true))
            {
                writer.Write(crc);
            }

            return stream.ToArray();
        }

        public SaveSlotInfo ToInfo(string slotId, int size)
        {
            var sections = new SaveSectionInfo[Sections.Count];
            for (int i = 0; i < Sections.Count; i++)
            {
                Entry entry = Sections[i];
                sections[i] = new SaveSectionInfo(entry.Key, entry.Version, ToUtc(entry.UpdatedTicks), entry.Data.Length);
            }

            var metadata = new Dictionary<string, string>(Metadata, StringComparer.Ordinal);
            return new SaveSlotInfo(slotId, ToUtc(CreatedTicks), ToUtc(UpdatedTicks), size, sections, metadata);
        }

        public static SaveResult TryParse(byte[] bytes, out SaveSlotFile file)
        {
            file = null;
            if (bytes == null || bytes.Length < MinimumSize)
            {
                return SaveResult.Failure(SaveFailureReason.Corrupted, message: "The data is too short to be a save.");
            }

            for (int i = 0; i < Magic.Length; i++)
            {
                if (bytes[i] != Magic[i])
                {
                    return SaveResult.Failure(SaveFailureReason.Corrupted, message: "The data is not a save.");
                }
            }

            // The format version comes before the check, so a newer format, whose layout may differ,
            // is refused as too new rather than as damaged, and a save over it does not replace it.
            int formatVersion = ReadInt32(bytes, FormatVersionOffset);
            if (formatVersion > FormatVersion)
            {
                return SaveResult.Failure(SaveFailureReason.VersionTooNew, message: $"The save was written in format {formatVersion}.");
            }

            if (formatVersion < 1)
            {
                return SaveResult.Failure(SaveFailureReason.Corrupted, message: $"Unknown save format {formatVersion}.");
            }

            int bodyLength = bytes.Length - 4;
            if ((uint)ReadInt32(bytes, bodyLength) != Crc32.Compute(bytes, 0, bodyLength))
            {
                return SaveResult.Failure(SaveFailureReason.Corrupted, message: "The save's check does not match its data.");
            }

            try
            {
                using var stream = new MemoryStream(bytes, 0, bodyLength, writable: false);
                using var reader = new BinaryReader(stream, Utf8);
                stream.Position = FormatVersionOffset + 4;
                var parsed = new SaveSlotFile
                {
                    CreatedTicks = reader.ReadInt64(),
                    UpdatedTicks = reader.ReadInt64(),
                };

                int metadataCount = reader.ReadInt32();
                if (metadataCount < 0 || metadataCount > SaveLimits.MaxMetadataEntries)
                {
                    return SaveResult.Failure(SaveFailureReason.Corrupted, message: "The save lists an invalid metadata count.");
                }

                for (int i = 0; i < metadataCount; i++)
                {
                    if (!TryReadKey(reader, out string key) || parsed.Metadata.ContainsKey(key) ||
                        !TryReadValue(reader, out string value))
                    {
                        return SaveResult.Failure(SaveFailureReason.Corrupted, message: "A metadata entry is invalid.");
                    }

                    parsed.Metadata.Add(key, value);
                }

                int count = reader.ReadInt32();
                if (count < 0)
                {
                    return SaveResult.Failure(SaveFailureReason.Corrupted, message: "The save lists a negative section count.");
                }

                var keys = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < count; i++)
                {
                    if (!TryReadKey(reader, out string key) || !keys.Add(key))
                    {
                        return SaveResult.Failure(SaveFailureReason.Corrupted, message: "A section's key is invalid.");
                    }

                    var entry = new Entry
                    {
                        Key = key,
                        Version = reader.ReadInt32(),
                        UpdatedTicks = reader.ReadInt64(),
                    };

                    int length = reader.ReadInt32();
                    if (entry.Version < 1 || length < 0 || length > stream.Length - stream.Position)
                    {
                        return SaveResult.Failure(SaveFailureReason.Corrupted, key, "A section's version or length is invalid.");
                    }

                    entry.Data = reader.ReadBytes(length);
                    parsed.Sections.Add(entry);
                }

                if (stream.Position != stream.Length)
                {
                    return SaveResult.Failure(SaveFailureReason.Corrupted, message: "The save has data past its last section.");
                }

                file = parsed;
                return SaveResult.Success;
            }
            catch (Exception exception) when (exception is EndOfStreamException || exception is IOException ||
                exception is ArgumentException || exception is DecoderFallbackException || exception is FormatException)
            {
                return SaveResult.Failure(SaveFailureReason.Corrupted, message: exception.Message);
            }
        }



        // A key is one length byte and that many ASCII bytes; the key rules allow nothing else.
        private static void WriteKey(BinaryWriter writer, string key)
        {
            writer.Write((byte)key.Length);
            for (int i = 0; i < key.Length; i++)
            {
                writer.Write((byte)key[i]);
            }
        }

        private static bool TryReadKey(BinaryReader reader, out string key)
        {
            key = null;
            int length = reader.ReadByte();
            if (length < 1 || length > SaveLimits.MaxKeyLength)
            {
                return false;
            }

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException();
            }

            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = (char)bytes[i];
            }

            key = new string(chars);
            return SaveLimits.IsValidSectionKey(key);
        }

        private static bool TryReadValue(BinaryReader reader, out string value)
        {
            value = null;
            int length = reader.ReadUInt16();
            if (length > SaveLimits.MaxMetadataValueLength * 4)
            {
                return false;
            }

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException();
            }

            value = Utf8.GetString(bytes);
            return value.Length <= SaveLimits.MaxMetadataValueLength;
        }

        // BinaryWriter writes little-endian on every platform.
        private static int ReadInt32(byte[] bytes, int offset) =>
            bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24;

        private static DateTime ToUtc(long ticks)
        {
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            {
                return DateTime.MinValue;
            }

            return new DateTime(ticks, DateTimeKind.Utc);
        }



        private static class Crc32
        {
            private static readonly uint[] Table = BuildTable();

            public static uint Compute(byte[] data, int offset, int count)
            {
                uint crc = 0xFFFFFFFFu;
                for (int i = offset; i < offset + count; i++)
                {
                    crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
                }

                return ~crc;
            }

            private static uint[] BuildTable()
            {
                var table = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint value = i;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                    }

                    table[i] = value;
                }

                return table;
            }
        }
    }
}
