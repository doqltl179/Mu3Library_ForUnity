using System;

namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// The voice format of <see cref="LocalBackend"/>: mono at <see cref="SampleRate"/>, recorded
    /// in frames of <see cref="FrameSamples"/>, and one mu-law byte (G.711) per sample, so a second
    /// of voice takes 16,000 bytes.
    /// </summary>
    internal static class LocalVoice
    {
        public const int SampleRate = 16000;

        /// <summary>20 milliseconds, the part a voice source hands out at a time.</summary>
        public const int FrameSamples = SampleRate / 50;

        /// <summary>
        /// The most frames one read hands out, half a second, so a game that stalled sends what it
        /// missed in messages of at most 8,000 bytes.
        /// </summary>
        public const int MaxFramesPerRead = 25;

        private const int Bias = 0x84;
        private const int Clip = 32635;

        // The exponent of a biased magnitude by its top 8 bits, and each byte's sample.
        private static readonly byte[] Exponents = CreateExponents();
        private static readonly float[] Samples = CreateSamples();

        public static void Encode(ReadOnlySpan<float> samples, Span<byte> encoded)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                encoded[i] = Encode(samples[i]);
            }
        }

        public static void Decode(ReadOnlySpan<byte> encoded, Span<float> samples)
        {
            for (int i = 0; i < encoded.Length; i++)
            {
                samples[i] = Samples[encoded[i]];
            }
        }

        internal static byte Encode(float sample)
        {
            if (float.IsNaN(sample))
            {
                sample = 0f;
            }

            int pcm = (int)(Math.Max(-1f, Math.Min(1f, sample)) * 32767f);
            int sign = 0;
            if (pcm < 0)
            {
                sign = 0x80;
                pcm = -pcm;
            }

            pcm = Math.Min(pcm, Clip) + Bias;
            int exponent = Exponents[pcm >> 7];
            int mantissa = (pcm >> (exponent + 3)) & 0x0F;
            return (byte)~(sign | (exponent << 4) | mantissa);
        }

        private static byte[] CreateExponents()
        {
            byte[] exponents = new byte[256];
            for (int i = 1; i < exponents.Length; i++)
            {
                int exponent = 0;
                while ((i >> (exponent + 1)) != 0)
                {
                    exponent++;
                }

                exponents[i] = (byte)exponent;
            }

            return exponents;
        }

        private static float[] CreateSamples()
        {
            float[] samples = new float[256];
            for (int i = 0; i < samples.Length; i++)
            {
                int code = ~i & 0xFF;
                int exponent = (code >> 4) & 0x07;
                int magnitude = ((((code & 0x0F) << 3) + Bias) << exponent) - Bias;
                samples[i] = ((code & 0x80) != 0 ? -magnitude : magnitude) / 32768f;
            }

            return samples;
        }
    }
}
