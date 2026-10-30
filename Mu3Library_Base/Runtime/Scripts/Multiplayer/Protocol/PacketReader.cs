using System;
using System.Buffers.Binary;
using System.Text;

namespace Mu3Library.Multiplayer.Protocol
{
    /// <summary>
    /// Reads a packet that <see cref="PacketWriter"/> wrote. Data from another player is untrusted,
    /// so every read checks its bounds and throws <see cref="FormatException"/> when the packet is
    /// short or malformed; the manager drops such a packet.
    /// </summary>
    internal ref struct PacketReader
    {
        // A packet never carries a string or a byte block larger than what one send may hold.
        private const int MaxBlockLength = 512 * 1024;

        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        public int Remaining => _data.Length - _position;

        public PacketReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        public PacketKind ReadKind() => (PacketKind)ReadByte();

        public byte ReadByte()
        {
            Require(1);
            return _data[_position++];
        }

        public bool ReadBool()
        {
            byte value = ReadByte();
            if (value > 1)
            {
                throw new FormatException($"Invalid bool value {value}.");
            }

            return value == 1;
        }

        public int ReadInt()
        {
            Require(sizeof(int));
            int value = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_position, sizeof(int)));
            _position += sizeof(int);
            return value;
        }

        public ulong ReadULong()
        {
            Require(sizeof(ulong));
            ulong value = BinaryPrimitives.ReadUInt64LittleEndian(_data.Slice(_position, sizeof(ulong)));
            _position += sizeof(ulong);
            return value;
        }

        public string ReadString()
        {
            int length = ReadBlockLength();
            string value = Encoding.UTF8.GetString(_data.Slice(_position, length));
            _position += length;
            return value;
        }

        public byte[] ReadBytes()
        {
            int length = ReadBlockLength();
            byte[] value = _data.Slice(_position, length).ToArray();
            _position += length;
            return value;
        }

        private int ReadBlockLength()
        {
            int length = ReadInt();
            if (length < 0 || length > MaxBlockLength)
            {
                throw new FormatException($"Invalid block length {length}.");
            }

            Require(length);
            return length;
        }

        private void Require(int count)
        {
            if (count > Remaining)
            {
                throw new FormatException($"Packet ended early: {count} bytes needed, {Remaining} left.");
            }
        }
    }
}
