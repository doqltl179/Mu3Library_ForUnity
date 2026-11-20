using System;

namespace Mu3Library.Foundation.Logging
{
    /// <summary>
    /// Log output the Foundation layer writes to. The layer is pure C# and cannot reach the
    /// Unity console itself, so whoever hosts it hands one in through
    /// <see cref="Mu3Logger.SetLogger"/>.
    /// </summary>
    public interface IMu3Logger
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogException(Exception exception);
    }
}
