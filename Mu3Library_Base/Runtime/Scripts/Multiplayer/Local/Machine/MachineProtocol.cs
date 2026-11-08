using System;
using System.Collections.Generic;
using System.Text;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;

namespace Mu3Library.Multiplayer.Local.Machine
{
    /// <summary>
    /// The frames that processes of a machine-wide local network exchange over TCP: a 4-byte
    /// little-endian length, then a <see cref="MachineFrameKind"/> byte and its payload.
    /// </summary>
    internal static class MachineProtocol
    {
        /// <summary>"MU3L", at the start of the first frame of every connection.</summary>
        public const int Magic = 0x4C33554D;

        /// <summary>Raise it whenever two versions of this file can no longer talk to each other.</summary>
        public const int Version = 1;

        public const int HeaderBytes = sizeof(int);

        /// <summary>Largest kind byte and payload of one frame: a full packet, or a lobby with its data.</summary>
        public const int MaxFrameBytes = 1 + 1024 * 1024;

        /// <summary>
        /// Most bytes of a lobby, as <see cref="WriteSnapshot"/> writes it, that the frames carry:
        /// the answer to a join, the largest frame that brings a lobby, holds a 4-byte code beside
        /// the kind byte and the lobby.
        /// </summary>
        public const int MaxSnapshotBytes = MaxFrameBytes - 1 - sizeof(int);

        // What WriteSnapshot writes before the members and the data: the lobby ID, the owner's ID
        // and port, the visibility, the capacity, whether it is joinable, and the two list lengths.
        private const int SnapshotFixedBytes = sizeof(ulong) + sizeof(ulong) + sizeof(int) + 1 + sizeof(int) + 1 + sizeof(int) + sizeof(int);

        // What WriteSnapshot writes for a member besides its name: the player ID and the port.
        private const int MemberFixedBytes = sizeof(ulong) + sizeof(int);

        /// <summary>
        /// The most bytes one member takes in what <see cref="WriteSnapshot"/> writes
        /// (<see cref="MemberBytes"/>): its ID, its port, and the longest name with its length,
        /// 208 in all. Search counts a lobby's free slots by it (<see cref="MachineDirectory.ListedCapacity"/>).
        /// </summary>
        public const int MaxMemberBytes = MemberFixedBytes + sizeof(int) + MaxNameBytes;

        /// <summary>
        /// A <see cref="MachineFrameKind.LobbyReply"/> that asks a returning member to try again in
        /// a moment: the player it reached has not noticed yet that the lobby passed to it.
        /// </summary>
        public const int NotYet = -1;

        /// <summary>
        /// The first byte of a <see cref="MachineFrameKind.Ping"/> on a session connection that
        /// measures it. After it come the round trip so far in microseconds, then how many packets
        /// the sender sent on the connection and how many of them the simulated network lost, each
        /// an unsigned 64-bit number. The other side answers a request with a reply that adds its
        /// own delay to the round trip.
        /// </summary>
        public const byte ProbeRequest = 1;
        public const byte ProbeReply = 2;

        /// <summary>
        /// Most members or data entries of one lobby that a frame carries: a lobby holds at most a
        /// few dozen members and keys, so more means a corrupt frame. The lobby files keep to it too.
        /// </summary>
        public const int MaxListLength = 4096;

        /// <summary>
        /// The UTF-8 bytes of the longest player name, lobby data key, and lobby data value that the
        /// frames and the lobby files carry: three for each character of a name or a key, which
        /// also covers a surrogate pair and a lone surrogate, and for a value all of
        /// <see cref="MultiplayerLimits.MaxLobbyDataSize"/>, which also holds the reserved values
        /// the manager adds. A string past its limit is a format error, found before it is decoded.
        /// </summary>
        public const int MaxNameBytes = MultiplayerLimits.MaxPlayerNameLength * 3;
        public const int MaxDataKeyBytes = MultiplayerLimits.MaxDataKeyLength * 3;
        public const int MaxDataValueBytes = MultiplayerLimits.MaxLobbyDataSize;

        /// <summary>
        /// Whether the other processes can read a lobby data entry: an owner takes only such
        /// entries, as Steam refuses a key or a value that is too long.
        /// </summary>
        public static bool IsWithinLimits(string key, string value)
        {
            return Encoding.UTF8.GetByteCount(key) <= MaxDataKeyBytes
                && Encoding.UTF8.GetByteCount(value ?? string.Empty) <= MaxDataValueBytes;
        }

        /// <summary>
        /// A player's name as the frames carry it: cut as the roster cuts it
        /// (<see cref="MultiplayerLimits.CutPlayerName"/>), to <see cref="MultiplayerLimits.MaxPlayerNameLength"/>
        /// characters or one fewer, never splitting a surrogate pair. A cut name takes at most
        /// <see cref="MaxNameBytes"/>.
        /// </summary>
        public static string ToWireName(string name) => MultiplayerLimits.CutPlayerName(name);

        public static void WriteName(PacketWriter writer, string name)
        {
            writer.WriteString(ToWireName(name ?? string.Empty));
        }

        /// <summary>
        /// Reads a name of at most <see cref="MaxNameBytes"/> and cuts it as <see cref="ToWireName"/>
        /// does, once, as it arrives: a longer name, which only a process of another version sends,
        /// is kept as a process of this version would have sent it (#374). So every name a lobby
        /// or a connection keeps is already cut, and <see cref="WriteSnapshot"/>,
        /// <see cref="MemberBytes"/>, and the lobby file's capacity write and count it again and
        /// again without allocating.
        /// </summary>
        public static string ReadName(ref PacketReader reader) => ToWireName(reader.ReadString(MaxNameBytes));

        public static void WriteSnapshot(PacketWriter writer, MachineLobby lobby)
        {
            writer.WriteULong(lobby.Id.Value);
            writer.WriteULong(lobby.OwnerId.Value);
            writer.WriteInt(lobby.OwnerPort);
            writer.WriteByte((byte)lobby.Visibility);
            writer.WriteInt(lobby.Capacity);
            writer.WriteBool(lobby.IsJoinable);

            writer.WriteInt(lobby.Members.Count);
            for (int i = 0; i < lobby.Members.Count; i++)
            {
                MachineMember member = lobby.Members[i];
                writer.WriteULong(member.Id.Value);
                writer.WriteInt(member.Port);
                WriteName(writer, member.Name);
            }

            writer.WriteInt(lobby.Data.Count);
            foreach (KeyValuePair<string, string> pair in lobby.Data)
            {
                writer.WriteString(pair.Key);
                writer.WriteString(pair.Value);
            }
        }

        /// <summary>
        /// The bytes <see cref="WriteSnapshot"/> writes for the lobby, counted field by field as it
        /// writes them, without writing, to hold against <see cref="MaxSnapshotBytes"/>. Change it
        /// with <see cref="WriteSnapshot"/>: <c>MultiplayerLocalMachineTests</c> compare the two.
        /// </summary>
        public static int SnapshotBytes(MachineLobby lobby)
        {
            int bytes = SnapshotFixedBytes;
            for (int i = 0; i < lobby.Members.Count; i++)
            {
                bytes += MemberBytes(lobby.Members[i].Name);
            }

            foreach (KeyValuePair<string, string> pair in lobby.Data)
            {
                bytes += DataEntryBytes(pair.Key, pair.Value);
            }

            return bytes;
        }

        /// <summary>
        /// The bytes one member takes in what <see cref="WriteSnapshot"/> writes: its ID, its port,
        /// and its name as the frames carry it, at most <see cref="MaxNameBytes"/> long.
        /// </summary>
        public static int MemberBytes(string name) => MemberFixedBytes + StringBytes(ToWireName(name ?? string.Empty));

        /// <summary>The bytes one lobby data entry takes in what <see cref="WriteSnapshot"/> writes.</summary>
        public static int DataEntryBytes(string key, string value) => StringBytes(key) + StringBytes(value);

        /// <summary>What <see cref="PacketWriter.WriteString"/> writes: a 4-byte length, then the UTF-8 bytes.</summary>
        private static int StringBytes(string value) => sizeof(int) + Encoding.UTF8.GetByteCount(value ?? string.Empty);

        /// <summary>Reads a lobby that <see cref="WriteSnapshot"/> wrote, or throws <see cref="FormatException"/>.</summary>
        public static MachineLobby ReadSnapshot(ref PacketReader reader)
        {
            MachineLobby lobby = new MachineLobby
            {
                Id = new LobbyId(reader.ReadULong()),
                OwnerId = new PlayerId(reader.ReadULong()),
                OwnerPort = reader.ReadInt(),
                Visibility = ReadVisibility(ref reader),
                Capacity = reader.ReadInt(),
                IsJoinable = reader.ReadBool(),
            };

            int memberCount = ReadListLength(ref reader);
            for (int i = 0; i < memberCount; i++)
            {
                lobby.Members.Add(new MachineMember
                {
                    Id = new PlayerId(reader.ReadULong()),
                    Port = reader.ReadInt(),
                    Name = ReadName(ref reader),
                });
            }

            int dataCount = ReadListLength(ref reader);
            for (int i = 0; i < dataCount; i++)
            {
                string key = reader.ReadString(MaxDataKeyBytes);
                lobby.Data[key] = reader.ReadString(MaxDataValueBytes);
            }

            if (!lobby.Id.IsValid || lobby.Capacity < 1)
            {
                throw new FormatException("Invalid lobby.");
            }

            return lobby;
        }

        public static LobbyVisibility ReadVisibility(ref PacketReader reader)
        {
            byte value = reader.ReadByte();
            if (value > (byte)LobbyVisibility.Public)
            {
                throw new FormatException($"Invalid lobby visibility {value}.");
            }

            return (LobbyVisibility)value;
        }

        private static int ReadListLength(ref PacketReader reader)
        {
            int length = reader.ReadInt();
            if (length < 0 || length > MaxListLength)
            {
                throw new FormatException($"Invalid list length {length}.");
            }

            return length;
        }
    }
}
