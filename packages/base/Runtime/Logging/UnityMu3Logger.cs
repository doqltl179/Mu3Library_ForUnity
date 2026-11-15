using System;
using Mu3Library.Foundation.Logging;
using UnityEngine;

namespace Mu3Library.Logging
{
    /// <summary>
    /// Routes the Foundation layer's logs to the Unity console. Registered on startup, so the
    /// pure C# layer logs without knowing the engine exists.
    /// </summary>
    public sealed class UnityMu3Logger : IMu3Logger
    {
        public void Log(string message) => Debug.Log(message);
        public void LogWarning(string message) => Debug.LogWarning(message);
        public void LogError(string message) => Debug.LogError(message);
        public void LogException(Exception exception) => Debug.LogException(exception);
    }

    internal static class UnityMu3LoggerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            Mu3Logger.SetLogger(new UnityMu3Logger());
        }
    }
}
