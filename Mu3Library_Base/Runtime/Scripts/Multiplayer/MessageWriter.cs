using System;
using System.Buffers.Binary;
using System.Text;
using UnityEngine;

namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// Writes a message payload, for <see cref="IMultiplayerMessenger.Send"/> or
    /// <see cref="IMultiplayerManager.StartGame"/>, into a buffer it reuses.
    /// <see cref="MessageReader"/> reads the values back in the order they were written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every player writes and reads the same bytes on every platform: numbers are little-endian,
    /// and a <see cref="float"/> or <see cref="double"/> keeps every bit it is handed, so negative
    /// zero, NaN, and subnormal values arrive as they left. Only a signaling NaN can turn quiet
    /// before it is written: the editor's Mono runtime holds a signaling <see cref="float"/> NaN as
    /// a quiet one. A string is its UTF-8 byte count followed by the bytes, with -1 for null, and a
    /// byte block is its length followed by the bytes.
    /// </para>
    /// <para>
    /// Keep one writer and <see cref="Clear"/> it before each message: once the buffer has grown
    /// to the largest message, writing numbers, <see cref="bool"/>, strings, byte blocks, and the
    /// Unity types allocates nothing. One writer serves one thread at a time.
    /// </para>
    /// </remarks>
    public sealed class MessageWriter
    {
        private byte[] _buffer;
        private int _length;

        /// <summary>The bytes written since the last <see cref="Clear"/>. Valid until the next write or clear.</summary>
        public ReadOnlySpan<byte> Written => new ReadOnlySpan<byte>(_buffer, 0, _length);

        /// <summary>The number of bytes written since the last <see cref="Clear"/>.</summary>
        public int Length => _length;



        public MessageWriter(int initialCapacity = 256)
        {
            if (initialCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), initialCapacity, "The capacity must be at least 1.");
            }

            _buffer = new byte[initialCapacity];
        }

        /// <summary>Starts a new message in the same buffer.</summary>
        public MessageWriter Clear()
        {
            _length = 0;
            return this;
        }

        #region Numbers
        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteByte(byte value)
        {
            Ensure(sizeof(byte));
            _buffer[_length++] = value;
        }

        public void WriteSByte(sbyte value) => WriteByte(unchecked((byte)value));

        public void WriteShort(short value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(Reserve(sizeof(short)), value);
        }

        public void WriteUShort(ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(Reserve(sizeof(ushort)), value);
        }

        public void WriteInt(int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(Reserve(sizeof(int)), value);
        }

        public void WriteUInt(uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(Reserve(sizeof(uint)), value);
        }

        public void WriteLong(long value)
        {
            BinaryPrimitives.WriteInt64LittleEndian(Reserve(sizeof(long)), value);
        }

        public void WriteULong(ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(Reserve(sizeof(ulong)), value);
        }

        /// <summary>Writes the value's bits, so the reader gets exactly this value back.</summary>
        public void WriteFloat(float value) => WriteInt(BitConverter.SingleToInt32Bits(value));

        /// <summary>Writes the value's bits, so the reader gets exactly this value back.</summary>
        public void WriteDouble(double value) => WriteLong(BitConverter.DoubleToInt64Bits(value));
        #endregion

        #region Text And Bytes
        /// <summary>Writes the UTF-8 byte count, or -1 for null, then the UTF-8 bytes.</summary>
        public void WriteString(string value)
        {
            if (value == null)
            {
                WriteInt(-1);
                return;
            }

            int byteCount = Encoding.UTF8.GetByteCount(value);
            WriteInt(byteCount);
            Ensure(byteCount);
            Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, _length);
            _length += byteCount;
        }

        /// <summary>Writes the length, then the bytes. A null array writes as an empty block.</summary>
        public void WriteBytes(ReadOnlySpan<byte> value)
        {
            WriteInt(value.Length);
            value.CopyTo(Reserve(value.Length));
        }
        #endregion

        #region Unity Types
        public void WriteVector2(Vector2 value)
        {
            WriteFloat(value.x);
            WriteFloat(value.y);
        }

        public void WriteVector3(Vector3 value)
        {
            WriteFloat(value.x);
            WriteFloat(value.y);
            WriteFloat(value.z);
        }

        public void WriteQuaternion(Quaternion value)
        {
            WriteFloat(value.x);
            WriteFloat(value.y);
            WriteFloat(value.z);
            WriteFloat(value.w);
        }

        public void WriteColor(Color value)
        {
            WriteFloat(value.r);
            WriteFloat(value.g);
            WriteFloat(value.b);
            WriteFloat(value.a);
        }
        #endregion

        /// <summary>Grows the buffer if needed and hands out the next <paramref name="count"/> bytes.</summary>
        private Span<byte> Reserve(int count)
        {
            Ensure(count);
            Span<byte> span = new Span<byte>(_buffer, _length, count);
            _length += count;
            return span;
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
