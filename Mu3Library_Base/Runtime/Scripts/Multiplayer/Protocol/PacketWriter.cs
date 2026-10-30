using System;
using System.Buffers.Binary;
using System.Text;

namespace Mu3Library.Multiplayer.Protocol
{
    /// <summary>
    /// Writes a packet into a reused buffer, little-endian. One writer serves one manager, which
    /// sends from the main thread only.
    /// </summary>
    internal sealed class PacketWriter
    {
        private byte[] _buffer;
        private int _length;

        public ReadOnlySpan<byte> Written => new ReadOnlySpan<byte>(_buffer, 0, _length);

        public PacketWriter(int initialCapacity = 256)
        {
            _buffer = new byte[initialCapacity];
        }

        public PacketWriter Begin(PacketKind kind)
        {
            _length = 0;
            WriteByte((byte)kind);
            return this;
        }

        public void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[_length++] = value;
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteInt(int value)
        {
            Ensure(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(_buffer, _length, sizeof(int)), value);
            _length += sizeof(int);
        }

        public void WriteULong(ulong value)
        {
            Ensure(sizeof(ulong));
            BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(_buffer, _length, sizeof(ulong)), value);
            _length += sizeof(ulong);
        }

        public void WriteString(string value)
        {
            value ??= string.Empty;
            int byteCount = Encoding.UTF8.GetByteCount(value);
            WriteInt(byteCount);
            Ensure(byteCount);
            Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, _length);
            _length += byteCount;
        }

        public void WriteBytes(ReadOnlySpan<byte> value)
        {
            WriteInt(value.Length);
            Ensure(value.Length);
            value.CopyTo(new Span<byte>(_buffer, _length, value.Length));
            _length += value.Length;
        }

        private void Ensure(int additional)
        {
            int required = _length + additional;
            if (required <= _buffer.Length)
            {
                return;
            }

            int capacity = _buffer.Length * 2;
            while (capacity < required)
            {
                capacity *= 2;
            }

            Array.Resize(ref _buffer, capacity);
        }
    }
}
