using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Mu3Library.Sample.Multiplayer
{
    /// <summary>
    /// Keeps the latest lines the sample shows on screen and writes every line to the Unity log
    /// with the <see cref="Prefix"/>, so the log of a player build tells what happened too.
    /// </summary>
    /// <remarks>
    /// Every line starts with the time it was logged at, in <see cref="TimeFormat"/>: the local
    /// time of day of this computer's clock, to the millisecond, such as <c>14:05:09.123</c>. The
    /// time since the start, such as <see cref="Time.realtimeSinceStartup"/>, would count from a
    /// different moment in each process, while the clock lets two computers whose clocks are
    /// synchronized and set to the same time zone tell from their lines which came first and how
    /// far apart, and compare a line with the time a player noted from the clock, such as when
    /// they ended the host's process. The milliseconds order the lines within one second; the
    /// lines of two computers are only as close as their clocks are.
    /// </remarks>
    public sealed class SampleLog
    {
        public const string Prefix = "[MultiplayerSample] ";

        /// <summary>The format of the time each line starts with: hours of 24, minutes, seconds, and milliseconds.</summary>
        public const string TimeFormat = "HH:mm:ss.fff";

        private const int MaxLines = 16;

        private readonly List<string> _lines = new List<string>();

        /// <summary>The latest lines, the oldest first, each starting with its time.</summary>
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>
        /// The time of day now as a line starts with it, for a line the sample writes to the Unity
        /// log without this log, such as <see cref="SampleBackend"/>'s warning that Steam cannot start.
        /// </summary>
        public static string Timestamp()
        {
            // The invariant culture keeps the separators those of the format, whatever the
            // computer's own culture writes between hours and minutes.
            return DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture);
        }

        public void Info(string line)
        {
            string time = Timestamp();
            Add($"{time} {line}");
            Debug.Log($"{time} {Prefix}{line}");
        }

        public void Warning(string line)
        {
            string time = Timestamp();
            Add($"{time} {line}");
            Debug.LogWarning($"{time} {Prefix}{line}");
        }

        private void Add(string line)
        {
            _lines.Add(line);
            if (_lines.Count > MaxLines)
            {
                _lines.RemoveAt(0);
            }
        }
    }
}
