using System.Linq;
using Mu3Library.Multiplayer;
using NUnit.Framework;
using UnityEngine;

namespace Mu3Library.Tests
{
    /// <summary>
    /// <see cref="VoicePlayer"/>: how it buffers a speaker's voice into its source's clip, when it
    /// starts and stops the source, and how it keeps the voice from falling behind (#576). Edit
    /// mode plays no audio, so the tests move the source's position by hand.
    /// </summary>
    public class VoicePlayerTests
    {
        private const int Rate = 16000;

        private GameObject _speaker;
        private AudioSource _source;
        private VoicePlayer _player;
        private int _playhead;



        [SetUp]
        public void SetUp()
        {
            _speaker = new GameObject("Speaker");
            _source = _speaker.AddComponent<AudioSource>();
            _player = new VoicePlayer(_source, Rate);
            _playhead = 0;
            _player.UsePlayheadForTests(() => _playhead, position => _playhead = position);
        }

        [TearDown]
        public void TearDown()
        {
            _player.Dispose();
            Object.DestroyImmediate(_speaker);
        }

        [Test]
        public void Voice_StartsOnceTheDelayIsBuffered_FromTheStartOfTheSourcesClip()
        {
            Assert.That(_source.clip, Is.Not.Null);
            Assert.That(_source.loop, Is.True);
            Assert.That(_source.clip.frequency, Is.EqualTo(Rate));

            float[] first = Ramp(Rate / 20, 0f);
            _player.Write(first);
            Assert.That(_player.IsPlaying, Is.False, "50 ms is less than the 100 ms delay");
            Assert.That(_player.BufferedSeconds, Is.EqualTo(0.05f).Within(1e-4f));

            float[] second = Ramp(Rate / 10, 0.5f);
            _player.Write(second);
            Assert.That(_player.IsPlaying, Is.True);
            Assert.That(_player.BufferedSeconds, Is.EqualTo(0.15f).Within(1e-4f));

            float[] written = new float[first.Length + second.Length];
            _source.clip.GetData(written, 0);
            Assert.That(written, Is.EqualTo(first.Concat(second).ToArray()).Within(1e-3f));
        }

        [Test]
        public void Voice_StopsOnceTheSourcePlayedEverything_AndWaitsForTheDelayAgain()
        {
            _player.Write(new float[Rate / 5]);
            Assert.That(_player.IsPlaying, Is.True);

            _playhead = Rate / 10;
            _player.Update();
            Assert.That(_player.IsPlaying, Is.True);
            Assert.That(_player.BufferedSeconds, Is.EqualTo(0.1f).Within(1e-4f));

            _playhead = Rate / 5;
            _player.Update();
            Assert.That(_player.IsPlaying, Is.False, "the speaker stopped talking");

            _player.Write(new float[Rate / 20]);
            Assert.That(_player.IsPlaying, Is.False, "the next voice waits for the delay again");
        }

        [Test]
        public void Voice_ThatPilesUp_SkipsAheadToTheNewestDelay()
        {
            _player.Write(new float[Rate / 5]);
            _player.Write(new float[Rate / 4]);

            // 0.45 seconds buffered, past the delay and a quarter second: the source jumps to the
            // newest 0.1 seconds.
            Assert.That(_player.BufferedSeconds, Is.EqualTo(_player.DelaySeconds).Within(1e-4f));
            Assert.That(_playhead, Is.EqualTo(Rate / 5 + Rate / 4 - Rate / 10));
            Assert.That(_player.IsPlaying, Is.True);
        }

        [Test]
        public void Dispose_TakesTheClipOffTheSource_AndDestroysIt()
        {
            AudioClip clip = _source.clip;

            _player.Dispose();

            Assert.That(_source.clip, Is.Null);
            Assert.That(clip == null, Is.True, "destroyed");
            Assert.That(_player.IsPlaying, Is.False);
        }

        private static float[] Ramp(int samples, float start)
        {
            return Enumerable.Range(0, samples).Select(i => start + 0.4f * i / samples).ToArray();
        }
    }
}
