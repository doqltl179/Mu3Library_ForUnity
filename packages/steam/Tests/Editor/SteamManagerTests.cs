#if MU3LIBRARY_STEAMWORKS_SUPPORT && UNITY_STANDALONE
using System;
using System.Text.RegularExpressions;
using Mu3Library.Steam;
using NUnit.Framework;
using Steamworks;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// <see cref="SteamManager"/> on a stand-in for Steam's start, callbacks, and shutdown: which
    /// Steam it owns and shuts down, the reason a start that fails answers, and a Steam native
    /// library that fails, none of which reaches the game as an exception (#607).
    /// </summary>
    public class SteamManagerTests
    {
        private sealed class FakeSteam : ISteamApi
        {
            public bool IsRunning { get; set; }
            public ESteamAPIInitResult InitResult { get; set; } = ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
            public string InitMessage { get; set; }
            public Exception InitThrows { get; set; }
            public Exception CallbacksThrow { get; set; }
            public int Inits { get; private set; }
            public int CallbackRuns { get; private set; }
            public int Shutdowns { get; private set; }

            public ESteamAPIInitResult Init(out string message)
            {
                Inits++;
                if (InitThrows != null)
                {
                    throw InitThrows;
                }

                message = InitMessage;
                IsRunning = InitResult == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
                return InitResult;
            }

            public void RunCallbacks()
            {
                if (CallbacksThrow != null)
                {
                    throw CallbacksThrow;
                }

                CallbackRuns++;
            }

            public void Shutdown()
            {
                Shutdowns++;
                IsRunning = false;
            }
        }

        [Test]
        public void TryStart_StartsASteamThatDoesNotRun_RunsItsCallbacks_AndShutsItDownWhenDisposed()
        {
            FakeSteam steam = new FakeSteam();

            SteamStartResult result = SteamManager.TryStartWith(steam, 0, out SteamManager manager);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(steam.Inits, Is.EqualTo(1));
            Assert.That(manager.OwnsSteam, Is.True);
            Assert.That(manager.IsAvailable, Is.True);

            manager.Update();
            manager.Update();
            Assert.That(steam.CallbackRuns, Is.EqualTo(2));

            manager.Dispose();
            manager.Dispose();
            Assert.That(steam.Shutdowns, Is.EqualTo(1), "disposing twice shuts Steam down once");
            Assert.That(manager.IsAvailable, Is.False);

            manager.Update();
            Assert.That(steam.CallbackRuns, Is.EqualTo(2), "a disposed manager runs no callbacks");
        }

        [Test]
        public void TryStart_UsesASteamThatRunsAlready_AndLeavesItRunning()
        {
            // The game or SteamBackend started Steam: the manager runs its callbacks but does not own it.
            FakeSteam steam = new FakeSteam { IsRunning = true };

            SteamStartResult result = SteamManager.TryStartWith(steam, 0, out SteamManager manager);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(steam.Inits, Is.Zero);
            Assert.That(manager.OwnsSteam, Is.False);

            manager.Update();
            Assert.That(steam.CallbackRuns, Is.EqualTo(1));

            manager.Dispose();
            Assert.That(steam.Shutdowns, Is.Zero);
            Assert.That(steam.IsRunning, Is.True);
        }

        [TestCase(ESteamAPIInitResult.k_ESteamAPIInitResult_NoSteamClient, SteamStartFailureReason.SteamNotRunning)]
        [TestCase(ESteamAPIInitResult.k_ESteamAPIInitResult_FailedGeneric, SteamStartFailureReason.SteamNotRunning)]
        [TestCase(ESteamAPIInitResult.k_ESteamAPIInitResult_VersionMismatch, SteamStartFailureReason.VersionMismatch)]
        public void TryStart_ASteamThatDoesNotStart_AnswersItsReason_WithWhatSteamSaid(ESteamAPIInitResult answer, SteamStartFailureReason expected)
        {
            FakeSteam steam = new FakeSteam { InitResult = answer, InitMessage = "simulated" };
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Steam\] Steam could not start \(" + answer + @"\): simulated"));

            SteamStartResult result = SteamManager.TryStartWith(steam, 0, out SteamManager manager);

            Assert.That(result.Reason, Is.EqualTo(expected));
            Assert.That(result.Message, Is.EqualTo("simulated"));
            Assert.That(manager, Is.Null);
        }

        [TestCase(typeof(DllNotFoundException))]
        [TestCase(typeof(EntryPointNotFoundException))]
        [TestCase(typeof(BadImageFormatException))]
        [TestCase(typeof(InvalidOperationException))]
        public void TryStart_ASteamThatCannotLoad_AnswersSteamNotRunning_WithoutAnException(Type thrown)
        {
            // What the runtime throws for a steam_api64 it cannot load or that lacks a function,
            // also inside the TypeInitializationException of a type whose initializer met it.
            Exception exception = (Exception)Activator.CreateInstance(thrown, "simulated");
            FakeSteam steam = new FakeSteam { InitThrows = new TypeInitializationException("Steamworks.Simulated", exception) };
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Steam\] Steam could not start \(" + thrown.Name + @"\): simulated"));

            SteamStartResult result = SteamManager.TryStartWith(steam, 0, out SteamManager manager);

            Assert.That(result.Reason, Is.EqualTo(SteamStartFailureReason.SteamNotRunning));
            Assert.That(manager, Is.Null);
        }

        [Test]
        public void TryStart_AnyOtherException_ReachesTheCaller()
        {
            // A mistake in the code is not hidden behind a reason.
            FakeSteam steam = new FakeSteam { InitThrows = new NullReferenceException() };

            Assert.Throws<NullReferenceException>(() => SteamManager.TryStartWith(steam, 0, out _));
        }

        [Test]
        public void ACallbackRunThatFailsInTheNativeLibrary_WarnsOnce_StopsTheManager_AndDisposeStillShutsSteamDown()
        {
            FakeSteam steam = new FakeSteam();
            SteamManager.TryStartWith(steam, 0, out SteamManager manager);
            steam.CallbacksThrow = new EntryPointNotFoundException("simulated");
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Steam\] Steam's callbacks no longer run: a Steam call failed \(EntryPointNotFoundException\): simulated"));

            manager.Update();
            manager.Update();
            Assert.That(manager.IsAvailable, Is.False);

            manager.Dispose();
            Assert.That(steam.Shutdowns, Is.EqualTo(1), "the manager still shuts down the Steam it started");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AManagerThatFoundSteamShutDownUnderIt_NeitherRunsCallbacks_NorShutsDownASteamStartedAgain()
        {
            FakeSteam steam = new FakeSteam();
            SteamManager.TryStartWith(steam, 0, out SteamManager manager);

            steam.IsRunning = false;
            Assert.That(manager.IsAvailable, Is.False);

            steam.IsRunning = true;
            manager.Update();
            manager.Dispose();
            Assert.That(manager.IsAvailable, Is.False);
            Assert.That(steam.CallbackRuns, Is.Zero);
            Assert.That(steam.Shutdowns, Is.Zero, "the Steam started again is not the one the manager started");
        }
    }
}
#endif
