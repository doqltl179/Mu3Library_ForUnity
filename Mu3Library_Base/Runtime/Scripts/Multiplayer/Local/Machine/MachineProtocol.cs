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
        /// A player's name as the frames carry it: cut to <see cref="MultiplayerLimits.MaxPlayerNameLength"/>
        /// characters, as the roster cuts it.
        /// </summary>
        public static string ToWireName(string name)
        {
            return name.Length <= MultiplayerLimits.MaxPlayerNameLength
                ? name
                : name.Substring(0, MultiplayerLimits.MaxPlayerNameLength);
        }

        public static void WriteName(PacketWriter writer, string name)
        {
            writer.WriteString(ToWireName(name ?? string.Empty));
        }

        public static string ReadName(ref PacketReader reader) => reader.ReadString(MaxNameBytes);

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
