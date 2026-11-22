using System;

namespace Mu3Library.Steam.Cloud
{
    internal static class SteamCalls
    {
        /// <summary>
        /// True for an exception that tells Steam cannot run in this process, as
        /// <c>SteamBackend</c> reads one: Steamworks.NET's check that Steam started
        /// (<see cref="InvalidOperationException"/>), and a native library that is missing, lacks a
        /// function, or is built for another architecture, also inside a
        /// <see cref="TypeInitializationException"/>.
        /// </summary>
        public static bool IsSteamUnavailable(Exception exception)
        {
            Exception cause = exception;
            while (cause is TypeInitializationException && cause.InnerException != null)
            {
                cause = cause.InnerException;
            }

            return cause is DllNotFoundException
                || cause is EntryPointNotFoundException
                || cause is BadImageFormatException
                || cause is InvalidOperationException;
        }
    }
}
