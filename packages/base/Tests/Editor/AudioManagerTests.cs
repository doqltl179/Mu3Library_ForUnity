using System;
using System.Collections;
using System.Collections.Generic;
using Mu3Library.Audio;
using Mu3Library.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Audio fades and the BGM playlist driven in edit mode. Each frame queues a player loop update,
    /// which moves every controller coroutine one step, and then runs the manager's Update. A batch
    /// mode editor keeps a source playing without advancing it, so a test ends a track by stopping
    /// its source.
    /// </summary>
    public class AudioManagerTests
    {
        // Fades advance on real time here, about 0.2 ms a frame, so the waits are bounded in seconds.
        private const float FadeTime = 0.05f;
        private const float LongFadeTime = 10.0f;
        private const float PlaylistInterval = 0.05f;
        private const double MaxSeconds = 2.0;

        private AudioManager _manager;
        private HashSet<GameObject> _sceneRoots;
        private float _timeScale;

        private AudioClip _clipA;
        private AudioClip _clipB;
        private AudioClip _bossClip;

        [SetUp]
        public void SetUp()
        {
            _sceneRoots = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
            _timeScale = Time.timeScale;

            _manager = new AudioManager();

            _clipA = CreateClip("A");
            _clipB = CreateClip("B");
            _bossClip = CreateClip("Boss");
        }

        [TearDown]
        public void TearDown()
        {
            // The manager root goes first: Dispose destroys it with Object.Destroy, which edit mode refuses.
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!_sceneRoots.Contains(root))
                {
                    Object.DestroyImmediate(root);
                }
            }

            _manager.Dispose();

            Object.DestroyImmediate(_clipA);
            Object.DestroyImmediate(_clipB);
            Object.DestroyImmediate(_bossClip);

            TestTimeScale.Restore(_timeScale);
        }

        private static AudioClip CreateClip(string name)
            => AudioClip.Create(name, 8000, 1, 8000, false);

        private IEnumerator RunUntil(Func<bool> condition, string description)
        {
            double start = EditorApplication.timeSinceStartup;
            while (!condition() && EditorApplication.timeSinceStartup - start < MaxSeconds)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;

                _manager.Update();
            }

            Assert.IsTrue(condition(), $"Waited {MaxSeconds} seconds until {description}.");
        }

        private IEnumerator NextFrame()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;

            _manager.Update();
        }

        private IEnumerator RunFor(double seconds)
        {
            double start = EditorApplication.timeSinceStartup;
            while (EditorApplication.timeSinceStartup - start < seconds)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;

                _manager.Update();
            }
        }

        private List<T> Controllers<T>() where T : AudioController
        {
            List<T> controllers = new();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!_sceneRoots.Contains(root))
                {
                    controllers.AddRange(root.GetComponentsInChildren<T>(true));
                }
            }

            return controllers;
        }

        private T Single<T>() where T : AudioController
        {
            List<T> controllers = Controllers<T>();
            Assert.AreEqual(1, controllers.Count, $"One {typeof(T).Name} exists.");

            return controllers[0];
        }

        private BgmController PlayingBgm(AudioClip clip)
        {
            foreach (BgmController controller in Controllers<BgmController>())
            {
                if (controller.IsPlaying && controller.IsSameClip(clip))
                {
                    return controller;
                }
            }

            return null;
        }

        private static float SourceVolume(AudioController controller)
            => controller.GetComponent<AudioSource>().volume;

        private static void EndTrack(AudioController controller)
            => controller.GetComponent<AudioSource>().Stop();

        [UnityTest]
        public IEnumerator PlaySfx_AfterFadeOutSfxAll_ReusesPooledSourceAtFullVolume()
        {
            _manager.PlaySfx(_clipA);
            SfxController controller = Single<SfxController>();

            _manager.FadeOutSfxAll(FadeTime);
            yield return RunUntil(() => !controller.gameObject.activeSelf, "the faded SFX went back to the pool");
            Assert.AreEqual(0.0f, controller.FadeVolume);

            _manager.PlaySfx(_clipB);

            Assert.AreSame(controller, Single<SfxController>(), "PlaySfx reused the pooled source.");
            Assert.AreEqual(1.0f, controller.FadeVolume);
            Assert.Greater(SourceVolume(controller), 0.0f);
        }

        [UnityTest]
        public IEnumerator PlayEnvironment_AfterFadeOutEnvironmentAll_ReusesPooledSourceAtFullVolume()
        {
            _manager.PlayEnvironment(_clipA);
            EnvironmentController controller = Single<EnvironmentController>();

            _manager.FadeOutEnvironmentAll(FadeTime);
            yield return RunUntil(() => !controller.gameObject.activeSelf, "the faded environment sound went back to the pool");
            Assert.AreEqual(0.0f, controller.FadeVolume);

            _manager.PlayEnvironment(_clipB);

            Assert.AreSame(controller, Single<EnvironmentController>(), "PlayEnvironment reused the pooled source.");
            Assert.AreEqual(1.0f, controller.FadeVolume);
            Assert.Greater(SourceVolume(controller), 0.0f);
        }

        [Test]
        public void PlaySfx_EvictingSourceThatFadesIn_PlaysAtFullVolume()
        {
            _manager.SfxInstanceCountMax = 1;

            _manager.FadeInSfx(_clipA, LongFadeTime);
            SfxController controller = Single<SfxController>();
            Assert.Less(controller.FadeVolume, 1.0f, "The source is still fading in.");

            _manager.PlaySfx(_clipB);

            Assert.AreSame(controller, Single<SfxController>(), "PlaySfx took over the oldest source.");
            Assert.IsTrue(controller.IsSameClip(_clipB));
            Assert.AreEqual(1.0f, controller.FadeVolume);
            Assert.Greater(SourceVolume(controller), 0.0f);
        }

        [Test]
        public void TransitionBgm_WithZeroTime_PlaysNewTrackAtFullVolume()
        {
            _manager.PlayBgm(_clipA);
            BgmController previous = PlayingBgm(_clipA);

            _manager.TransitionBgm(_clipB, 0.0f);

            BgmController next = PlayingBgm(_clipB);
            Assert.IsNotNull(next, "The new track plays.");
            Assert.AreEqual(1.0f, next.FadeVolume);
            Assert.Greater(SourceVolume(next), 0.0f);

            Assert.IsFalse(previous.IsPlaying, "The previous track stopped at once.");
            Assert.AreEqual(0.0f, previous.FadeVolume);
        }

        [Test]
        public void FadeOutBgm_WithZeroTime_SilencesAndPausesAtOnce()
        {
            _manager.PlayBgm(_clipA);
            BgmController controller = PlayingBgm(_clipA);

            _manager.FadeOutBgm(0.0f);

            Assert.IsTrue(controller.IsPaused);
            Assert.AreEqual(0.0f, controller.FadeVolume);
            Assert.AreEqual(0.0f, SourceVolume(controller));
        }

        [UnityTest]
        public IEnumerator PlayBgm_SameClipDuringFadeOut_CancelsTheFade()
        {
            _manager.PlayBgm(_clipA);
            BgmController controller = PlayingBgm(_clipA);

            // A long fade stays under way however long a frame takes, so the request below meets it.
            _manager.FadeOutBgm(LongFadeTime);
            yield return RunUntil(() => controller.FadeVolume < 1.0f, "the fade-out started");
            Assert.IsFalse(controller.IsPaused, "The fade-out is still running.");

            _manager.PlayBgm(_clipA);
            Assert.AreEqual(1.0f, controller.FadeVolume);

            yield return RunFor(FadeTime * 4);

            Assert.IsFalse(controller.IsPaused, "The canceled fade-out did not pause the track.");
            Assert.IsTrue(controller.IsPlaying);
            Assert.AreEqual(1.0f, controller.FadeVolume, "The canceled fade-out did not go on.");
            Assert.Greater(SourceVolume(controller), 0.0f);
        }

        [UnityTest]
        public IEnumerator PlaylistTrackEnd_PlaysTheNextTrack()
        {
            _manager.PlayBgmPlaylist(new[] { _clipA, _clipB }, PlaylistInterval);
            BgmController controller = PlayingBgm(_clipA);
            Assert.IsNotNull(controller, "The playlist started on its first track.");

            EndTrack(controller);

            yield return RunUntil(() => PlayingBgm(_clipB) != null, "the playlist played its next track");
        }

        [UnityTest]
        public IEnumerator PlayBgm_WhilePlaylistWaitsBetweenTracks_KeepsTheRequestedTrack()
        {
            _manager.PlayBgmPlaylist(new[] { _clipA, _clipB }, PlaylistInterval);
            BgmController controller = PlayingBgm(_clipA);

            EndTrack(controller);
            yield return RunUntil(() => controller.IsCompleted, "the first track ended");

            _manager.PlayBgm(_bossClip);
            yield return RunFor(PlaylistInterval * 4);

            Assert.IsNotNull(PlayingBgm(_bossClip), "The requested track still plays.");
            Assert.IsNull(PlayingBgm(_clipB), "The playlist did not move on.");
        }

        [UnityTest]
        public IEnumerator PlayBgm_ClipThePlaylistPlays_PlaysItUnderTheRequestedSettings()
        {
            _manager.PlayBgmPlaylist(new[] { _clipA, _clipB }, PlaylistInterval);
            BgmController controller = PlayingBgm(_clipA);

            AudioSourceSettings twice = AudioSourceSettings.BgmStandard;
            twice.LoopCount = 2;
            _manager.PlayBgm(_clipA, twice);

            // The playlist would have played the track once; the request plays it a second time.
            EndTrack(controller);
            yield return RunFor(PlaylistInterval * 4);

            Assert.AreSame(controller, PlayingBgm(_clipA), "The requested track plays again.");
            Assert.IsNull(PlayingBgm(_clipB), "The playlist did not move on.");
        }

        [UnityTest]
        public IEnumerator PlayBgm_DuringPlaylistTrack_DoesNotResumeThePlaylistWhenItEnds()
        {
            _manager.PlayBgmPlaylist(new[] { _clipA, _clipB }, 0.0f);

            AudioSourceSettings once = AudioSourceSettings.BgmStandard;
            once.LoopCount = 1;
            _manager.PlayBgm(_bossClip, once);

            BgmController controller = PlayingBgm(_bossClip);
            EndTrack(controller);
            yield return RunFor(PlaylistInterval * 4);

            Assert.IsNull(PlayingBgm(_clipB), "The playlist did not move on.");
            Assert.IsTrue(controller.IsCompleted, "The requested track ended.");
        }

        [UnityTest]
        public IEnumerator TransitionBgm_WhilePlaylistWaitsBetweenTracks_KeepsTheNewTrack()
        {
            _manager.PlayBgmPlaylist(new[] { _clipA, _clipB }, PlaylistInterval);
            BgmController controller = PlayingBgm(_clipA);

            EndTrack(controller);
            yield return RunUntil(() => controller.IsCompleted, "the first track ended");

            _manager.TransitionBgm(_bossClip, FadeTime);
            yield return RunFor(PlaylistInterval * 4);

            BgmController boss = PlayingBgm(_bossClip);
            Assert.IsNotNull(boss, "The new track still plays.");
            Assert.AreEqual(1.0f, boss.FadeVolume, "The new track finished fading in.");
            Assert.IsNull(PlayingBgm(_clipB), "The playlist did not move on.");
        }

        [UnityTest]
        public IEnumerator FadeOutBgm_AtTimeScaleZero_FadesOnUnscaledTime()
        {
            Time.timeScale = 0.0f;
            Assert.AreEqual(TimeMode.Unscaled, _manager.TimeMode, "Unscaled time is the default.");

            _manager.PlayBgm(_clipA);
            BgmController controller = PlayingBgm(_clipA);

            _manager.FadeOutBgm(FadeTime);
            yield return RunUntil(() => controller.IsPaused, "the fade-out ended");

            Assert.AreEqual(0.0f, controller.FadeVolume);
        }

        [UnityTest]
        public IEnumerator FadeOutBgm_OnScaledTime_WaitsWhileTimeScaleIsZero()
        {
            Time.timeScale = 0.0f;

            _manager.PlayBgm(_clipA);
            BgmController controller = PlayingBgm(_clipA);

            // Set after the controller exists, so the manager hands the mode to it.
            _manager.TimeMode = TimeMode.Scaled;

            // This frame measured its delta time before the time scale changed, so the fade starts
            // in the next one.
            yield return NextFrame();

            _manager.FadeOutBgm(FadeTime);
            yield return RunFor(FadeTime * 10);

            Assert.IsFalse(controller.IsPaused, "Game time does not move while the time scale is 0.");
            Assert.AreEqual(1.0f, controller.FadeVolume);

            Time.timeScale = 1.0f;
            yield return RunUntil(() => controller.IsPaused, "the fade-out ended");
        }

        [UnityTest]
        public IEnumerator PlaylistInterval_AtTimeScaleZero_RunsOnUnscaledTime()
        {
            Time.timeScale = 0.0f;

            _manager.PlayBgmPlaylist(new[] { _clipA, _clipB }, PlaylistInterval);
            EndTrack(PlayingBgm(_clipA));

            yield return RunUntil(() => PlayingBgm(_clipB) != null, "the playlist played its next track");
        }
    }
}
