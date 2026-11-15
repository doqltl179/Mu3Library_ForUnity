using System;
using Mu3Library.Multiplayer;

namespace Mu3Library.Sample.Multiplayer
{
    /// <summary>
    /// The one game message of the sample: a text and a number. <see cref="Write"/> puts them into
    /// a <see cref="MessageWriter"/>, and <see cref="Read"/> takes them back out of the payload with
    /// a <see cref="MessageReader"/>, in the same order.
    /// </summary>
    public readonly struct SampleGreeting
    {
        /// <summary>
        /// The message type, a number the game chooses. Every player registers it with the same
        /// policy, or the host drops the message.
        /// </summary>
        public const ushort MessageType = 1;

        public string Text { get; }
        public int Number { get; }

        public SampleGreeting(string text, int number)
        {
            Text = text;
            Number = number;
        }

        public void Write(MessageWriter writer)
        {
            writer.WriteString(Text);
            writer.WriteInt(Number);
        }

        /// <summary>
        /// Reads a greeting. Another player's payload is untrusted: a payload that ends early
        /// throws <see cref="FormatException"/>, which the manager logs before it goes on with
        /// the next message.
        /// </summary>
        public static SampleGreeting Read(ReadOnlySpan<byte> payload)
        {
            MessageReader reader = new MessageReader(payload);
            string text = reader.ReadString();
            int number = reader.ReadInt();
            return new SampleGreeting(text, number);
        }
    }
}
