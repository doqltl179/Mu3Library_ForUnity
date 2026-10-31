using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Sample.Multiplayer
{
    /// <summary>
    /// Keeps the latest lines the sample shows on screen and writes every line to the Unity log
    /// with the <see cref="Prefix"/>, so the log of a player build tells what happened too.
    /// </summary>
    public sealed class SampleLog
    {
        public const string Prefix = "[MultiplayerSample] ";

        private const int MaxLines = 16;

        private readonly List<string> _lines = new List<string>();

        /// <summary>The latest lines, the oldest first.</summary>
        public IReadOnlyList<string> Lines => _lines;

        public void Info(string line)
        {
            Add(line);
            Debug.Log(Prefix + line);
        }

        public void Warning(string line)
        {
            Add(line);
            Debug.LogWarning(Prefix + line);
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
