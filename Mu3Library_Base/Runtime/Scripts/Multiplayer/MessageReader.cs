using System;
using System.Buffers.Binary;
using System.Text;
using UnityEngine;

namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// Reads a message payload that <see cref="MessageWriter"/> wrote, in the order it was
    /// written, such as the payload a <see cref="MessageHandler"/> receives or
    /// <see cref="IMultiplayerManager.StartData"/>. Pass it on with <c>ref</c>, since every read
    /// moves it forward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Another player's payload is untrusted, so every read checks its bounds and throws
    /// <see cref="FormatException"/> when the payload ends early or holds an invalid length or
    /// <see cref="bool"/>. The manager catches an exception that a handler throws and goes on with
    /// the next message, so a malformed message costs only itself, and logs it only the first
    /// time the handler of that message type throws that exception type in a session. A
    /// <see cref="FormatException"/> becomes a warning without a stack trace, since the sender
    /// wrote another payload than the handler reads and the message names the problem.
    /// </para>
    /// <para>
    /// Reading numbers, <see cref="bool"/>, the Unity types, and <see cref="ReadBytesSpan"/>
    /// allocates nothing; <see cref="ReadString"/> and <see cref="ReadBytes"/> allocate the value
    /// they return.
    /// </para>
    /// <para>
    /// <see cref="ReadFloat"/> turns the bits back into a <see cref="float"/> with
    /// <see cref="BitConverter.Int32BitsToSingle"/>, where the Mono runtime, which the editor uses,
    /// may turn a signaling NaN into a quiet one; the Unity types read their components the same
    /// way. Read bits that must arrive exactly with <see cref="ReadInt"/> or <see cref="ReadUInt"/>,
    /// as <see cref="MessageWriter"/> describes.
    /// </para>
    /// </remarks>
    public ref struct MessageReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        /// <summary>The bytes read so far.</summary>
        public int Position => _position;

        /// <summary>The bytes left to read.</summary>
        public int Remaining => _data.Length - _position;



        public MessageReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        #region Numbers
        /// <summary>Throws <see cref="FormatException"/> for a byte other than 0 or 1.</summary>
        public bool ReadBool()
        {
            byte value = ReadByte();
            if (value > 1)
            {
                throw new FormatException($"Invalid bool value {value}.");
            }

            return value == 1;
        }

        public byte ReadByte()
        {
            Require(sizeof(byte));
            return _data[_position++];
        }

        public sbyte ReadSByte() => unchecked((sbyte)ReadByte());

        public short ReadShort() => BinaryPrimitives.ReadInt16LittleEndian(Take(sizeof(short)));

        public ushort ReadUShort() => BinaryPrimitives.ReadUInt16LittleEndian(Take(sizeof(ushort)));

        public int ReadInt() => BinaryPrimitives.ReadInt32LittleEndian(Take(sizeof(int)));

        public uint ReadUInt() => BinaryPrimitives.ReadUInt32LittleEndian(Take(sizeof(uint)));

        public long ReadLong() => BinaryPrimitives.ReadInt64LittleEndian(Take(sizeof(long)));

        public ulong ReadULong() => BinaryPrimitives.ReadUInt64LittleEndian(Take(sizeof(ulong)));

        public float ReadFloat() => BitConverter.Int32BitsToSingle(ReadInt());

        public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadLong());
        #endregion

        #region Text And Bytes
        /// <summary>Reads a string, or null when null was written.</summary>
        public string ReadString()
        {
            int length = ReadInt();
            if (length == -1)
            {
                return null;
            }

            return Encoding.UTF8.GetString(TakeBlock(length));
        }

        /// <summary>Reads a byte block into a new array.</summary>
        public byte[] ReadBytes() => ReadBytesSpan().ToArray();

        /// <summary>
        /// Reads a byte block without copying it. The span points into the payload, so it is
        /// valid only as long as the payload is.
        /// </summary>
        public ReadOnlySpan<byte> ReadBytesSpan() => TakeBlock(ReadInt());
        #endregion

        #region Unity Types
        public Vector2 ReadVector2()
        {
            float x = ReadFloat();
            float y = ReadFloat();
            return new Vector2(x, y);
        }

        public Vector3 ReadVector3()
        {
            float x = ReadFloat();
            float y = ReadFloat();
            float z = ReadFloat();
            return new Vector3(x, y, z);
        }

        public Quaternion ReadQuaternion()
        {
            float x = ReadFloat();
            float y = ReadFloat();
            float z = ReadFloat();
            float w = ReadFloat();
            return new Quaternion(x, y, z, w);
        }

        public Color ReadColor()
        {
            float r = ReadFloat();
            float g = ReadFloat();
            float b = ReadFloat();
            float a = ReadFloat();
            return new Color(r, g, b, a);
        }
        #endregion

        private ReadOnlySpan<byte> TakeBlock(int length)
        {
            if (length < 0)
            {
                throw new FormatException($"Invalid block length {length}.");
            }

            return Take(length);
        }

        private ReadOnlySpan<byte> Take(int count)
        {
            Require(count);
            ReadOnlySpan<byte> span = _data.Slice(_position, count);
            _position += count;
            return span;
        }

        private void Require(int count)
        {
            if (count > Remaining)
            {
                throw new FormatException($"Payload ended early: {count} bytes needed, {Remaining} left.");
            }
        }
    }
}
