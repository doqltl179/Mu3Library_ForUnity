using System;

namespace Mu3Library.Extensions
{
    public static class intExtensions
    {
        /// <summary>
        /// <para>Checks whether the specified bit is set (1).</para>
        /// <para>Example:</para>
        /// <para><c>bool isSet = 6.IsBitSet(1);</c></para>
        /// <para>Result (binary): <c>0110</c> -> <c>true</c> (bit 1 is 1)</para>
        /// </summary>
        public static bool IsBitSet(this int value, int bitIndex)
        {
            ValidateBitIndex(bitIndex);
            return (value & (1 << bitIndex)) != 0;
        }

        /// <summary>
        /// <para>Returns a value with the specified bit set to 1.</para>
        /// <para>Example:</para>
        /// <para><c>int result = 4.SetBit(0);</c></para>
        /// <para>Result (binary): <c>0100</c> -> <c>0101</c></para>
        /// </summary>
        public static int SetBit(this int value, int bitIndex)
        {
            ValidateBitIndex(bitIndex);
            return value | (1 << bitIndex);
        }

        /// <summary>
        /// <para>Returns a value with the specified bit enabled or disabled.</para>
        /// <para>Examples:</para>
        /// <para><c>int enabled = 10.SetBit(1, true);</c></para>
        /// <para>Result (binary): <c>1010</c> -> <c>1010</c></para>
        /// <para><c>int disabled = 10.SetBit(1, false);</c></para>
        /// <para>Result (binary): <c>1010</c> -> <c>1000</c></para>
        /// </summary>
        public static int SetBit(this int value, int bitIndex, bool enabled)
        {
            ValidateBitIndex(bitIndex);
            int mask = 1 << bitIndex;
            return enabled ? (value | mask) : (value & ~mask);
        }

        /// <summary>
        /// <para>Returns a value with the specified bit cleared to 0.</para>
        /// <para>Example:</para>
        /// <para><c>int result = 7.ClearBit(1);</c></para>
        /// <para>Result (binary): <c>0111</c> -> <c>0101</c></para>
        /// </summary>
        public static int ClearBit(this int value, int bitIndex)
        {
            ValidateBitIndex(bitIndex);
            return value & ~(1 << bitIndex);
        }

        /// <summary>
        /// <para>Returns a value with the specified bit toggled (0 -> 1, 1 -> 0).</para>
        /// <para>Example:</para>
        /// <para><c>int result = 5.ToggleBit(2);</c></para>
        /// <para>Result (binary): <c>0101</c> -> <c>0001</c></para>
        /// </summary>
        public static int ToggleBit(this int value, int bitIndex)
        {
            ValidateBitIndex(bitIndex);
            return value ^ (1 << bitIndex);
        }

        /// <summary>
        /// <para>Returns a value produced by a bitwise AND with the specified mask.</para>
        /// <para>Example:</para>
        /// <para><c>int result = 13.And(10);</c></para>
        /// <para>Result (binary): <c>1101</c> &amp; <c>1010</c> = <c>1000</c></para>
        /// </summary>
        public static int And(this int value, int mask)
        {
            return value & mask;
        }

        /// <summary>
        /// <para>Returns a value produced by a bitwise OR with the specified mask.</para>
        /// <para>Example:</para>
        /// <para><c>int result = 9.Or(3);</c></para>
        /// <para>Result (binary): <c>1001</c> | <c>0011</c> = <c>1011</c></para>
        /// </summary>
        public static int Or(this int value, int mask)
        {
            return value | mask;
        }

        /// <summary>
        /// <para>Returns a value produced by a bitwise XOR with the specified mask.</para>
        /// <para>Example:</para>
        /// <para><c>int result = 12.Xor(10);</c></para>
        /// <para>Result (binary): <c>1100</c> ^ <c>1010</c> = <c>0110</c></para>
        /// </summary>
        public static int Xor(this int value, int mask)
        {
            return value ^ mask;
        }

        /// <summary>
        /// <para>Returns a value with all bits inverted.</para>
        /// <para>Example:</para>
        /// <para><c>int result = 5.Not();</c></para>
        /// <para>Result (binary): <c>0000_0101</c> -> <c>1111_1010</c></para>
        /// </summary>
        public static int Not(this int value)
        {
            return ~value;
        }

        /// <summary>
        /// <para>Returns a value shifted left by the specified number of bits.</para>
        /// <para>Example:</para>
        /// <para><c>int result = 3.ShiftLeft(2);</c></para>
        /// <para>Result (binary): <c>0011</c> &lt;&lt; 2 = <c>1100</c></para>
        /// </summary>
        public static int ShiftLeft(this int value, int shiftCount)
        {
            return value << shiftCount;
        }

        /// <summary>
        /// <para>Returns a value shifted right by the specified number of bits.</para>
        /// <para>Example:</para>
        /// <para><c>int result = 12.ShiftRight(2);</c></para>
        /// <para>Result (binary): <c>1100</c> &gt;&gt; 2 = <c>0011</c></para>
        /// </summary>
        public static int ShiftRight(this int value, int shiftCount)
        {
            return value >> shiftCount;
        }

        /// <summary>
        /// Validates that bitIndex is within the range of a 32-bit integer.
        /// </summary>
        private static void ValidateBitIndex(int bitIndex)
        {
            if (bitIndex < 0 || bitIndex > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(bitIndex), bitIndex, "bitIndex must be in range [0, 31].");
            }
        }
    }
}
