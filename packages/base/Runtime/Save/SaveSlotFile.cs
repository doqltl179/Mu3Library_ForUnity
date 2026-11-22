using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mu3Library.Save
{
    /// <summary>
    /// The bytes of one slot: a header, every section's key, version, time, and serialized data, and a
    /// CRC-32 of everything before it, so a damaged or foreign file is told apart from a save.
    /// </summary>
    internal sealed class SaveSlotFile
    {
        internal const int FormatVersion = 1;

        private static readonly byte[] Magic = { (byte)'M', (byte)'U', (byte)'3', (byte)'S' };
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        // Magic, format version, created and updated ticks, section count, and the CRC.
        private const int MinimumSize = 4 + 4 + 8 + 8 + 4 + 4;

        public long CreatedTicks;
        public long UpdatedTicks;
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
                writer.Write(Sections.Count);
                foreach (Entry entry in Sections)
                {
                    writer.Write(entry.Key);
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

            return new SaveSlotInfo(slotId, ToUtc(CreatedTicks), ToUtc(UpdatedTicks), size, sections);
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

            int bodyLength = bytes.Length - 4;
            uint storedCrc = BitConverter.ToUInt32(bytes, bodyLength);
            if (!BitConverter.IsLittleEndian)
            {
                storedCrc = ReverseBytes(storedCrc);
            }

            if (storedCrc != Crc32.Compute(bytes, 0, bodyLength))
            {
                return SaveResult.Failure(SaveFailureReason.Corrupted, message: "The save's check does not match its data.");
            }

            try
            {
                using var stream = new MemoryStream(bytes, 0, bodyLength, writable: false);
                using var reader = new BinaryReader(stream, Utf8);
                reader.ReadBytes(Magic.Length);
                int formatVersion = reader.ReadInt32();
                if (formatVersion > FormatVersion)
                {
                    return SaveResult.Failure(SaveFailureReason.VersionTooNew, message: $"The save was written in format {formatVersion}.");
                }

                if (formatVersion < 1)
                {
                    return SaveResult.Failure(SaveFailureReason.Corrupted, message: $"Unknown save format {formatVersion}.");
                }

                var parsed = new SaveSlotFile
                {
                    CreatedTicks = reader.ReadInt64(),
                    UpdatedTicks = reader.ReadInt64(),
                };

                int count = reader.ReadInt32();
                if (count < 0)
                {
                    return SaveResult.Failure(SaveFailureReason.Corrupted, message: "The save lists a negative section count.");
                }

                for (int i = 0; i < count; i++)
                {
                    var entry = new Entry
                    {
                        Key = reader.ReadString(),
                        Version = reader.ReadInt32(),
                        UpdatedTicks = reader.ReadInt64(),
                    };

                    int length = reader.ReadInt32();
                    if (length < 0 || length > stream.Length - stream.Position)
                    {
                        return SaveResult.Failure(SaveFailureReason.Corrupted, entry.Key, "A section's length runs past the save.");
                    }

                    entry.Data = reader.ReadBytes(length);
                    if (!SaveLimits.IsValidSectionKey(entry.Key) || entry.Version < 1 || parsed.Find(entry.Key) != null)
                    {
                        return SaveResult.Failure(SaveFailureReason.Corrupted, entry.Key, "A section's key or version is invalid.");
                    }

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
                exception is ArgumentException || exception is DecoderFallbackException)
            {
                return SaveResult.Failure(SaveFailureReason.Corrupted, message: exception.Message);
            }
        }



        private static DateTime ToUtc(long ticks)
        {
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            {
                return DateTime.MinValue;
            }

            return new DateTime(ticks, DateTimeKind.Utc);
        }

        private static uint ReverseBytes(uint value) =>
            (value & 0x000000FFu) << 24 | (value & 0x0000FF00u) << 8 | (value & 0x00FF0000u) >> 8 | (value & 0xFF000000u) >> 24;



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
