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
    /// and a <see cref="float"/> or <see cref="double"/> travels as its bits, so negative zero, NaN
    /// payloads, and subnormal values arrive as they left. A string is its UTF-8 byte count
    /// followed by the bytes, with -1 for null, and a byte block is its length followed by the bytes.
    /// </para>
    /// <para>
    /// A signaling <see cref="float"/> NaN is the exception. The Mono runtime, which the editor
    /// uses, may turn it into a quiet NaN wherever it holds the value as a <see cref="float"/>:
    /// before the value reaches <see cref="WriteFloat"/>, and again in
    /// <see cref="MessageReader.ReadFloat"/>, so players on different runtimes can read different
    /// bits for it. To carry such bits exactly, write them with <see cref="WriteInt"/> or
    /// <see cref="WriteUInt"/> (from <see cref="BitConverter.SingleToInt32Bits"/>) and read them
    /// with <see cref="MessageReader.ReadInt"/> or <see cref="MessageReader.ReadUInt"/>.
    /// </para>
    /// <para>
    /// Keep one writer and <see cref="Clear"/> it before each message: once the buffer has grown
    /// to the largest message, writing numbers, <see cref="bool"/>, strings, byte blocks, and the
    /// Unity types allocates nothing. One writer serves one thread at a time.
    /// </para>
    /// </remarks>
    public sealed class MessageWriter
    {
        // The largest byte array the runtime creates (Array.MaxLength on newer .NET).
        private const int MaxBufferLength = 0x7FFFFFC7;

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

        /// <summary>
        /// Grows the buffer to hold <paramref name="additional"/> more bytes, doubling it until it
        /// would pass the largest byte array the runtime creates.
        /// </summary>
        private void Ensure(int additional)
        {
            long required = (long)_length + additional;
            if (required <= _buffer.Length)
            {
                return;
            }

            if (required > MaxBufferLength)
            {
                throw new InvalidOperationException($"A message cannot grow past {MaxBufferLength} bytes; it needs {required}.");
            }

            long capacity = Math.Min(Math.Max((long)_buffer.Length * 2, required), MaxBufferLength);
            Array.Resize(ref _buffer, (int)capacity);
        }
    }
}
