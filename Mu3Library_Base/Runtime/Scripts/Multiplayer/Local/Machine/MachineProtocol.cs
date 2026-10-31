using System;
using System.Collections.Generic;
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

        // A lobby holds at most a few dozen members and keys; more means a corrupt frame.
        private const int MaxListLength = 4096;

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
                writer.WriteString(member.Name);
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
                    Name = reader.ReadString(),
                });
            }

            int dataCount = ReadListLength(ref reader);
            for (int i = 0; i < dataCount; i++)
            {
                string key = reader.ReadString();
                lobby.Data[key] = reader.ReadString();
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
