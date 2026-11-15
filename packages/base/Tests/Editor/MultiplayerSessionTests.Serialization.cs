using System;
using System.Collections.Generic;
using System.Linq;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
// Unity's Is extends NUnit's with AllocatingGCMemory.
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Message payloads through <see cref="MessageWriter"/> and <see cref="MessageReader"/>
    /// (FR-35, NFR-1, NFR-5).
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        private const ushort Snapshot = 4;

        // Zero, negative zero, the smallest and largest subnormals, the largest finite value,
        // both infinities, quiet NaNs with a sign and a payload, one, and a signaling NaN.
        private static readonly int[] FloatBits =
        {
            0x00000000, unchecked((int)0x80000000), 0x00000001, unchecked((int)0x807FFFFF), 0x7F7FFFFF,
            0x7F800000, unchecked((int)0xFF800000), 0x7FC00000, unchecked((int)0xFFC00001), 0x7FC12345,
            0x3F800000, 0x7F800001,
        };

        private static readonly long[] DoubleBits =
        {
            0L, unchecked((long)0x8000000000000000UL), 1L, 0x000FFFFFFFFFFFFFL, 0x7FEFFFFFFFFFFFFFL,
            0x7FF0000000000000L, unchecked((long)0xFFF0000000000000UL), unchecked((long)0xFFF8000000000001UL),
            0x7FF8000000000123L, 0x3FF0000000000000L, 0x7FF0000000000001L,
        };

        private static readonly string[] Texts = { null, string.Empty, "Ready", "준비 완료", "\U0001F3AE game", new string('x', 300) };

        #region Serialization
        [Test]
        public void MixedPayload_ReadsBackEveryValue_WithFloatsBitForBit()
        {
            // FR-35
            MessageWriter writer = new MessageWriter(initialCapacity: 1);
            WriteEveryType(writer);

            MessageReader reader = new MessageReader(writer.Written);
            AssertReadsEveryType(ref reader);
            Assert.That(reader.Remaining, Is.EqualTo(0));
            Assert.That(reader.Position, Is.EqualTo(writer.Length));
        }

        [Test]
        public void SignalingNaN_IsWrittenAsTheRuntimeHoldsIt()
        {
            // FR-35: the writer keeps the bits it is handed; whether a signaling NaN stays
            // signaling up to that point is the runtime's doing, which this records.
            MessageWriter writer = new MessageWriter();
            float single = BitConverter.Int32BitsToSingle(0x7F800001);
            double wide = BitConverter.Int64BitsToDouble(0x7FF0000000000001L);
            writer.WriteFloat(single);
            writer.WriteDouble(wide);

            MessageReader reader = new MessageReader(writer.Written);
            Assert.That(reader.ReadInt(), Is.EqualTo(BitConverter.SingleToInt32Bits(single)));
            Assert.That(reader.ReadLong(), Is.EqualTo(BitConverter.DoubleToInt64Bits(wide)));
            TestContext.WriteLine($"Signaling NaN held as float 0x{BitConverter.SingleToInt32Bits(single):X8}, as double 0x{BitConverter.DoubleToInt64Bits(wide):X16}.");
        }

        [Test]
        public void MixedPayload_FromAClient_ReadsTheSameEverywhere()
        {
            // FR-35: the payload passes through the host to every other player.
            (Node host, Node[] clients) = CreateSession(2);
            List<Node> readers = new List<Node> { host, clients[1] };
            int checkedCount = 0;
            foreach (Node node in readers)
            {
                node.Manager.RegisterMessage(Snapshot, (context, payload) =>
                {
                    MessageReader reader = new MessageReader(payload);
                    AssertReadsEveryType(ref reader);
                    Assert.That(reader.Remaining, Is.EqualTo(0));
                    checkedCount++;
                });
            }

            clients[0].Manager.RegisterMessage(Snapshot, null);
            MessageWriter writer = new MessageWriter();
            WriteEveryType(writer);

            Assert.That(clients[0].Manager.Send(Snapshot, writer.Written, MessageTarget.Others).IsSuccess, Is.True);
            writer.Clear().WriteInt(-1);
            Pump();

            // The manager logs a handler's exception instead of passing it on, so the count proves every reader ran to the end.
            Assert.That(checkedCount, Is.EqualTo(readers.Count));
        }

        [Test]
        public void Writer_LaysOutValuesLittleEndian_WithLengthPrefixes()
        {
            // FR-35: the layout every platform shares.
            MessageWriter writer = new MessageWriter();
            writer.WriteInt(0x01020304);
            writer.WriteUShort(0xABCD);
            writer.WriteFloat(1f);
            writer.WriteBool(true);
            writer.WriteString("A");
            writer.WriteString(null);
            writer.WriteBytes(new byte[] { 9, 8 });
            writer.WriteSByte(-2);

            Assert.That(writer.Written.ToArray(), Is.EqualTo(new byte[]
            {
                0x04, 0x03, 0x02, 0x01,
                0xCD, 0xAB,
                0x00, 0x00, 0x80, 0x3F,
                0x01,
                0x01, 0x00, 0x00, 0x00, 0x41,
                0xFF, 0xFF, 0xFF, 0xFF,
                0x02, 0x00, 0x00, 0x00, 0x09, 0x08,
                0xFE,
            }));

            writer.Clear();
            Assert.That(writer.Length, Is.EqualTo(0));
            Assert.That(writer.Written.IsEmpty, Is.True);
        }

        [Test]
        public void Reader_RefusesAShortOrMalformedPayload()
        {
            // NFR-5. The reader throws its own kind of FormatException, which the manager logs
            // without a stack trace.
            Assert.Catch<FormatException>(() => new MessageReader(new byte[3]).ReadInt());
            Assert.Catch<FormatException>(() => new MessageReader(new byte[7]).ReadVector2());
            Assert.Catch<FormatException>(() => new MessageReader(new byte[] { 2 }).ReadBool());
            Assert.Catch<FormatException>(() => new MessageReader(new byte[] { 0xFE, 0xFF, 0xFF, 0xFF }).ReadString());
            Assert.Catch<FormatException>(() => new MessageReader(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }).ReadBytes());
            Assert.Catch<FormatException>(() => new MessageReader(new byte[] { 0x05, 0x00, 0x00, 0x00, 0x41 }).ReadString());
            Assert.Catch<FormatException>(() => new MessageReader(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F, 0x41 }).ReadBytesSpan());
        }

        [Test]
        public void WritingAndReadingNumbersAndUnityTypes_AllocatesNothingOnceWarm()
        {
            // NFR-1: the message path, apart from the strings and arrays a read returns.
            MessageWriter writer = new MessageWriter();
            byte[] block = { 1, 2, 3 };
            float sum = 0f;

            void WriteAndRead()
            {
                writer.Clear();
                writer.WriteBool(true);
                writer.WriteByte(1);
                writer.WriteSByte(-1);
                writer.WriteShort(-2);
                writer.WriteUShort(2);
                writer.WriteInt(-3);
                writer.WriteUInt(3);
                writer.WriteLong(-4);
                writer.WriteULong(4);
                writer.WriteFloat(0.5f);
                writer.WriteDouble(0.25);
                writer.WriteVector2(Vector2.one);
                writer.WriteVector3(Vector3.up);
                writer.WriteQuaternion(Quaternion.identity);
                writer.WriteColor(Color.red);
                writer.WriteString("ready");
                writer.WriteBytes(block);

                MessageReader reader = new MessageReader(writer.Written);
                sum += reader.ReadBool() ? 1f : 0f;
                sum += reader.ReadByte() + reader.ReadSByte() + reader.ReadShort() + reader.ReadUShort();
                sum += reader.ReadInt() + reader.ReadUInt();
                sum += reader.ReadLong();
                sum += reader.ReadULong();
                sum += reader.ReadFloat() + (float)reader.ReadDouble();
                sum += reader.ReadVector2().x + reader.ReadVector3().y + reader.ReadQuaternion().w + reader.ReadColor().r;
                // A string is laid out as a byte block, so reading it as one skips it without allocating it.
                sum += reader.ReadBytesSpan().Length;
                sum += reader.ReadBytesSpan().Length;
            }

            for (int i = 0; i < 8; i++)
            {
                WriteAndRead();
            }

            Assert.That(WriteAndRead, Is.Not.AllocatingGCMemory());
            Assert.That(sum, Is.GreaterThan(0f));
        }
        #endregion

        #region Serialization Helpers
        private static void WriteEveryType(MessageWriter writer)
        {
            writer.WriteBool(true);
            writer.WriteBool(false);
            writer.WriteByte(byte.MaxValue);
            writer.WriteSByte(sbyte.MinValue);
            writer.WriteShort(short.MinValue);
            writer.WriteUShort(ushort.MaxValue);
            writer.WriteInt(int.MinValue);
            writer.WriteUInt(uint.MaxValue);
            writer.WriteLong(long.MinValue);
            writer.WriteULong(ulong.MaxValue);

            foreach (int bits in FloatBits)
            {
                writer.WriteFloat(BitConverter.Int32BitsToSingle(bits));
            }

            foreach (long bits in DoubleBits)
            {
                writer.WriteDouble(BitConverter.Int64BitsToDouble(bits));
            }

            foreach (string text in Texts)
            {
                writer.WriteString(text);
            }

            writer.WriteBytes(ReadOnlySpan<byte>.Empty);
            writer.WriteBytes(Enumerable.Range(0, 1000).Select(i => (byte)i).ToArray());

            writer.WriteVector2(new Vector2(Float(1), Float(2)));
            writer.WriteVector3(new Vector3(Float(3), Float(7), Float(8)));
            writer.WriteQuaternion(new Quaternion(Float(9), Float(10), Float(0), Float(11)));
            writer.WriteColor(new Color(Float(4), Float(5), Float(6), Float(10)));
        }

        private static void AssertReadsEveryType(ref MessageReader reader)
        {
            Assert.That(reader.ReadBool(), Is.True);
            Assert.That(reader.ReadBool(), Is.False);
            Assert.That(reader.ReadByte(), Is.EqualTo(byte.MaxValue));
            Assert.That(reader.ReadSByte(), Is.EqualTo(sbyte.MinValue));
            Assert.That(reader.ReadShort(), Is.EqualTo(short.MinValue));
            Assert.That(reader.ReadUShort(), Is.EqualTo(ushort.MaxValue));
            Assert.That(reader.ReadInt(), Is.EqualTo(int.MinValue));
            Assert.That(reader.ReadUInt(), Is.EqualTo(uint.MaxValue));
            Assert.That(reader.ReadLong(), Is.EqualTo(long.MinValue));
            Assert.That(reader.ReadULong(), Is.EqualTo(ulong.MaxValue));

            foreach (int bits in FloatBits)
            {
                Assert.That(BitConverter.SingleToInt32Bits(reader.ReadFloat()), Is.EqualTo(HeldBits(bits)), $"float bits 0x{bits:X8}");
            }

            foreach (long bits in DoubleBits)
            {
                Assert.That(BitConverter.DoubleToInt64Bits(reader.ReadDouble()), Is.EqualTo(HeldBits(bits)), $"double bits 0x{bits:X16}");
            }

            foreach (string text in Texts)
            {
                Assert.That(reader.ReadString(), Is.EqualTo(text));
            }

            Assert.That(reader.ReadBytes(), Is.Empty);
            Assert.That(reader.ReadBytesSpan().ToArray(), Is.EqualTo(Enumerable.Range(0, 1000).Select(i => (byte)i).ToArray()));

            Vector2 vector2 = reader.ReadVector2();
            AssertBits(new[] { vector2.x, vector2.y }, 1, 2);
            Vector3 vector3 = reader.ReadVector3();
            AssertBits(new[] { vector3.x, vector3.y, vector3.z }, 3, 7, 8);
            Quaternion quaternion = reader.ReadQuaternion();
            AssertBits(new[] { quaternion.x, quaternion.y, quaternion.z, quaternion.w }, 9, 10, 0, 11);
            Color color = reader.ReadColor();
            AssertBits(new[] { color.r, color.g, color.b, color.a }, 4, 5, 6, 10);
        }

        /// <summary>
        /// The bits a float has once the runtime holds it. They are the bits it was made from,
        /// except that a runtime may quiet a signaling NaN before any writer sees it.
        /// </summary>
        private static int HeldBits(int bits)
        {
            int held = BitConverter.SingleToInt32Bits(BitConverter.Int32BitsToSingle(bits));
            if (!IsSignalingNaN(bits))
            {
                Assert.That(held, Is.EqualTo(bits), $"the runtime changed float bits 0x{bits:X8}");
            }

            return held;
        }

        private static long HeldBits(long bits)
        {
            long held = BitConverter.DoubleToInt64Bits(BitConverter.Int64BitsToDouble(bits));
            if (!IsSignalingNaN(bits))
            {
                Assert.That(held, Is.EqualTo(bits), $"the runtime changed double bits 0x{bits:X16}");
            }

            return held;
        }

        private static bool IsSignalingNaN(int bits) => (bits & 0x7F800000) == 0x7F800000 && (bits & 0x007FFFFF) != 0 && (bits & 0x00400000) == 0;

        private static bool IsSignalingNaN(long bits) => (bits & 0x7FF0000000000000L) == 0x7FF0000000000000L && (bits & 0x000FFFFFFFFFFFFFL) != 0 && (bits & 0x0008000000000000L) == 0;

        /// <summary>The special float at that index of <see cref="FloatBits"/>.</summary>
        private static float Float(int index) => BitConverter.Int32BitsToSingle(FloatBits[index]);

        /// <summary>Checks that each component carries the bits of the special float at that index.</summary>
        private static void AssertBits(float[] values, params int[] indices)
        {
            Assert.That(values.Select(BitConverter.SingleToInt32Bits), Is.EqualTo(indices.Select(index => HeldBits(FloatBits[index]))));
        }
        #endregion
    }
}
