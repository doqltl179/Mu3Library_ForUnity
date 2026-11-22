using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// What a game wires together for voice chat (#576): the host choosing who receives each
    /// message of a type (<see cref="IMultiplayerMessenger.SetRelayFilter"/>), and voice that the
    /// local backend records, a message carries, and another player decodes (<see cref="IVoiceBackend"/>).
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        private const ushort Voice = 40;

        /// <summary>Stands in for the microphone: hands out what the test recorded, 20 milliseconds at a time.</summary>
        private sealed class FakeVoiceSource : ILocalVoiceSource
        {
            private readonly Queue<float> _recorded = new Queue<float>();

            public bool IsStarted { get; private set; }

            public void Record(float[] samples)
            {
                if (IsStarted)
                {
                    foreach (float sample in samples)
                    {
                        _recorded.Enqueue(sample);
                    }
                }
            }

            public bool Start()
            {
                IsStarted = true;
                return true;
            }

            public void Stop()
            {
                IsStarted = false;
                _recorded.Clear();
            }

            public bool TryReadFrame(Span<float> frame)
            {
                if (_recorded.Count < frame.Length)
                {
                    return false;
                }

                for (int i = 0; i < frame.Length; i++)
                {
                    frame[i] = _recorded.Dequeue();
                }

                return true;
            }
        }

        #region Relay filter
        [Test]
        public void RelayFilter_OnTheHost_ChoosesWhoReceives_TheMessagesOfClientsAndOfTheHostItself()
        {
            // Proximity voice: b stands far from everyone, so nobody hears b and b hears nobody.
            (Node host, Node[] clients) = CreateSession(3);
            Node a = clients[0], b = clients[1], c = clients[2];
            Node[] everyone = clients.Prepend(host).ToArray();
            Dictionary<Node, Inbox> inboxes = ListenAll(Voice, everyone);
            Assert.That(host.Manager.SetRelayFilter(Voice, (sender, recipient) => sender != b.Id && recipient != b.Id), Is.True);

            Assert.That(a.Manager.Send(Voice, new byte[] { 1 }, MessageTarget.Others, DeliveryMode.Unreliable).IsSuccess, Is.True);
            Assert.That(b.Manager.Send(Voice, new byte[] { 2 }, MessageTarget.Others, DeliveryMode.Unreliable).IsSuccess, Is.True, "the sender learns nothing of the choice");
            Assert.That(host.Manager.Send(Voice, new byte[] { 3 }, MessageTarget.Others, DeliveryMode.Unreliable).IsSuccess, Is.True);
            Pump();

            Assert.That(inboxes[host].Senders, Is.EquivalentTo(new[] { a.Id }));
            Assert.That(inboxes[a].Senders, Is.EquivalentTo(new[] { host.Id }));
            Assert.That(inboxes[b].Received, Is.Empty);
            Assert.That(inboxes[c].Senders, Is.EquivalentTo(new[] { a.Id, host.Id }), "the senders the connections proved");
        }

        [Test]
        public void RelayFilter_CountsOnlyOnTheHost_AndANullFilterLetsEveryoneHearAgain()
        {
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];
            Assert.That(host.Manager.SetRelayFilter(Voice, (sender, recipient) => true), Is.False, "the type is not registered yet");

            Dictionary<Node, Inbox> inboxes = ListenAll(Voice, new[] { host, a, b });
            int clientFilterCalls = 0;
            a.Manager.SetRelayFilter(Voice, (sender, recipient) => ++clientFilterCalls < 0);
            b.Manager.SetRelayFilter(Voice, (sender, recipient) => ++clientFilterCalls < 0);
            host.Manager.SetRelayFilter(Voice, (sender, recipient) => recipient != b.Id);

            a.Manager.Send(Voice, new byte[] { 1 }, MessageTarget.Others);
            Pump();
            Assert.That(inboxes[host].Received.Count, Is.EqualTo(1));
            Assert.That(inboxes[b].Received, Is.Empty);
            Assert.That(clientFilterCalls, Is.EqualTo(0), "a client never asks its filter");

            Assert.That(host.Manager.SetRelayFilter(Voice, null), Is.True);
            a.Manager.Send(Voice, new byte[] { 2 }, MessageTarget.Others);
            Pump();
            Assert.That(inboxes[b].Senders, Is.EqualTo(new[] { a.Id }));

            Assert.That(host.Manager.UnregisterMessage(Voice), Is.True);
            Assert.That(host.Manager.SetRelayFilter(Voice, (sender, recipient) => true), Is.False, "unregistering dropped the type");
        }

        [Test]
        public void RelayFilter_ThatThrows_KeepsTheMessageFromThatPlayer_AndIsLoggedOnce()
        {
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];
            Dictionary<Node, Inbox> inboxes = ListenAll(Voice, new[] { host, a, b });
            host.Manager.SetRelayFilter(Voice, (sender, recipient) => recipient == b.Id ? throw new InvalidOperationException("No position for b yet.") : true);

            LogAssert.Expect(LogType.Exception, new Regex("No position for b yet"));
            LogAssert.Expect(LogType.Warning, new Regex("The relay filter of message type 40 threw InvalidOperationException"));
            a.Manager.Send(Voice, new byte[] { 1 }, MessageTarget.Others);
            a.Manager.Send(Voice, new byte[] { 2 }, MessageTarget.Others);
            Pump();

            Assert.That(inboxes[host].Received.Count, Is.EqualTo(2));
            Assert.That(inboxes[b].Received, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }
        #endregion

        #region Local voice
        [Test]
        public void Voice_ThatOnePlayerRecords_ReachesAnotherInAMessage_AndDecodesToWhatTheySaid()
        {
            (Node host, Node[] clients) = CreateSession(1);
            Node client = clients[0];
            FakeVoiceSource microphone = new FakeVoiceSource();
            client.Backend.UseVoiceSourceForTests(microphone);
            IVoiceBackend speaker = client.Backend;
            IVoiceBackend listener = host.Backend;

            List<PlayerId> speakers = new List<PlayerId>();
            List<float> heard = new List<float>();
            host.Manager.RegisterMessage(Voice, (context, payload) =>
            {
                speakers.Add(context.Sender);
                if (listener.TryDecodeVoice(payload, out ReadOnlySpan<float> samples))
                {
                    heard.AddRange(samples.ToArray());
                }
            });
            client.Manager.RegisterMessage(Voice, null);

            Assert.That(speaker.TryReadVoice(out _), Is.False, "nothing before recording");
            Assert.That(speaker.StartVoiceRecording(), Is.True);
            Assert.That(speaker.IsRecordingVoice, Is.True);

            float[] said = Tone(LocalVoice.FrameSamples * 3 + 100);
            microphone.Record(said);
            Assert.That(speaker.TryReadVoice(out ReadOnlySpan<byte> voice), Is.True);
            Assert.That(voice.Length, Is.EqualTo(LocalVoice.FrameSamples * 3), "whole frames, a byte a sample");
            Assert.That(client.Manager.Send(Voice, voice, MessageTarget.Host, DeliveryMode.Unreliable).IsSuccess, Is.True);
            Assert.That(speaker.TryReadVoice(out _), Is.False, "the rest is less than a frame");
            Pump();

            Assert.That(listener.VoiceSampleRate, Is.EqualTo(16000));
            Assert.That(speakers, Is.EqualTo(new[] { client.Id }));
            Assert.That(heard.Count, Is.EqualTo(LocalVoice.FrameSamples * 3));
            for (int i = 0; i < heard.Count; i++)
            {
                Assert.That(heard[i], Is.EqualTo(said[i]).Within(0.02f), $"sample {i}");
            }

            speaker.StopVoiceRecording();
            Assert.That(speaker.IsRecordingVoice, Is.False);
            Assert.That(microphone.IsStarted, Is.False);
            Assert.That(listener.TryDecodeVoice(ReadOnlySpan<byte>.Empty, out _), Is.False, "empty data decodes to nothing");
        }

        [Test]
        public void Voice_StopsWithTheBackend_AndADisposedBackendDoesNotStartIt()
        {
            LocalBackend backend = CreateRawBackend("Speaker");
            FakeVoiceSource microphone = new FakeVoiceSource();
            backend.UseVoiceSourceForTests(microphone);
            Assert.That(backend.StartVoiceRecording(), Is.True);

            backend.Dispose();

            Assert.That(microphone.IsStarted, Is.False);
            Assert.That(backend.IsRecordingVoice, Is.False);
            Assert.That(backend.StartVoiceRecording(), Is.False);
            Assert.That(microphone.IsStarted, Is.False);
        }

        [Test]
        public void MicrophoneAtAnotherRate_IsResampledTo20MillisecondsOfMonoAt16kHz()
        {
            // 20 milliseconds of a 48 kHz stereo microphone: a ramp on the left, silence on the right.
            const int frames = 960;
            float[] chunk = new float[frames * 2];
            for (int i = 0; i < frames; i++)
            {
                chunk[2 * i] = i / (float)frames;
            }

            float[] frame = new float[LocalVoice.FrameSamples];
            MicrophoneVoiceSource.Resample(chunk, frames, 2, frame);

            // Mixed down to half the ramp, spread over 320 samples from its first to its last.
            for (int i = 0; i < frame.Length; i++)
            {
                float at = i * (frames - 1) / (float)(frame.Length - 1);
                Assert.That(frame[i], Is.EqualTo(0.5f * at / frames).Within(1e-5f), $"sample {i}");
            }

            float[] mono = new float[LocalVoice.FrameSamples];
            float[] ramp = Enumerable.Range(0, LocalVoice.FrameSamples).Select(i => i / (float)LocalVoice.FrameSamples).ToArray();
            MicrophoneVoiceSource.Resample(ramp, LocalVoice.FrameSamples, 1, mono);
            Assert.That(mono, Is.EqualTo(ramp), "a microphone at 16 kHz passes through as it is");
        }
        #endregion

        /// <summary>A 440 Hz tone at half volume, at 16 kHz.</summary>
        private static float[] Tone(int samples)
        {
            return Enumerable.Range(0, samples).Select(i => 0.5f * Mathf.Sin(2f * Mathf.PI * 440f * i / LocalVoice.SampleRate)).ToArray();
        }
    }
}
