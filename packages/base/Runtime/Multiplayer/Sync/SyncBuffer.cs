using System;

namespace Mu3Library.Multiplayer.Sync
{
    /// <summary>
    /// The bytes of a tick: the states' writes and the bundles built from them. It grows to the
    /// largest tick and then reuses its array, so a steady tick allocates nothing.
    /// </summary>
    /// <remarks>
    /// A bundle is the sender's tick as a variable-length number, then one entry per state: its ID
    /// shifted left by one with the lowest bit set for a whole state, the length of its bytes, both
    /// as variable-length numbers, and the bytes. A variable-length number takes seven bits a byte,
    /// lowest first, with the top bit set on every byte but the last.
    /// </remarks>
    internal sealed class SyncBuffer
    {
        private byte[] _buffer;
        private int _length;

        public int Length => _length;
        public bool IsEmpty => _length == 0;
        public ReadOnlySpan<byte> Written => new ReadOnlySpan<byte>(_buffer, 0, _length);



        public SyncBuffer(int initialCapacity)
        {
            _buffer = new byte[Math.Max(initialCapacity, 16)];
        }

        public void Clear()
        {
            _length = 0;
        }

        public ReadOnlySpan<byte> Slice(int offset, int length) => new ReadOnlySpan<byte>(_buffer, offset, length);

        public void WriteVarUInt(ulong value)
        {
            Ensure(VarUIntSize(value));
            while (value >= 0x80)
            {
                _buffer[_length++] = (byte)(value | 0x80);
                value >>= 7;
            }

            _buffer[_length++] = (byte)value;
        }

        public void WriteRaw(ReadOnlySpan<byte> bytes)
        {
            Ensure(bytes.Length);
            bytes.CopyTo(new Span<byte>(_buffer, _length, bytes.Length));
            _length += bytes.Length;
        }

        public static int VarUIntSize(ulong value)
        {
            int size = 1;
            while (value >= 0x80)
            {
                value >>= 7;
                size++;
            }

            return size;
        }

        private void Ensure(int additional)
        {
            int required = _length + additional;
            if (required > _buffer.Length)
            {
                Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, required));
            }
        }
    }

    /// <summary>Reads a bundle that <see cref="SyncBuffer"/> laid out, checking every bound.</summary>
    internal ref struct SyncBundleReader
    {
        // Ten bytes of seven bits hold every 64-bit value.
        private const int MaxVarUIntBytes = 10;

        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        public int Remaining => _data.Length - _position;



        public SyncBundleReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        /// <summary>Throws <see cref="FormatException"/> for a number that ends early or exceeds <paramref name="max"/>.</summary>
        public ulong ReadVarUInt(ulong max)
        {
            ulong value = 0;
            for (int i = 0; i < MaxVarUIntBytes; i++)
            {
                if (_position >= _data.Length)
                {
                    throw new PayloadFormatException("A sync bundle ended inside a number.");
                }

                byte next = _data[_position++];
                ulong bits = (ulong)(next & 0x7F);
                if (i == MaxVarUIntBytes - 1 && bits > 1)
                {
                    throw new PayloadFormatException("A sync bundle holds a number past 64 bits.");
                }

                value |= bits << (7 * i);
                if ((next & 0x80) == 0)
                {
                    if (value > max)
                    {
                        throw new PayloadFormatException($"A sync bundle holds {value}, past {max}.");
                    }

                    return value;
                }
            }

            throw new PayloadFormatException("A sync bundle holds a number past 64 bits.");
        }

        public ReadOnlySpan<byte> Take(int count)
        {
            if (count > Remaining)
            {
                throw new PayloadFormatException($"A sync bundle ended early: {count} bytes needed, {Remaining} left.");
            }

            ReadOnlySpan<byte> span = _data.Slice(_position, count);
            _position += count;
            return span;
        }
    }
}
