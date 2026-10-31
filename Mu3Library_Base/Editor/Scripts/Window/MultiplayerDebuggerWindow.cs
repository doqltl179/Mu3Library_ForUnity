using System.Collections.Generic;
using System.Globalization;
using Mu3Library.DI;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using UnityEditor;
using UnityEngine;

// UnityEditor has a SessionState of its own.
using SessionState = Mu3Library.Multiplayer.Data.SessionState;

namespace Mu3Library.Editor.Window
{
    /// <summary>
    /// Shows the multiplayer sessions of the game in play mode: each manager's session state, its
    /// players with the quality of this player's connection to each, and how many messages of each
    /// type it sent and received. It finds the managers among the singletons that the cores under
    /// <see cref="CoreRoot"/> hold, as the Runtime Diagnostics drawer finds its services, and reads
    /// them again every <see cref="RefreshIntervalSeconds"/>. Reading changes nothing.
    /// </summary>
    internal sealed class MultiplayerDebuggerWindow : EditorWindow
    {
        private const string WindowName = "Multiplayer Debugger";
        private const string MenuName = "Mu3Library/Windows/" + WindowName;

        /// <summary>How often the window reads the managers again, in seconds.</summary>
        internal const double RefreshIntervalSeconds = 0.5;

        private const float NumberWidth = 72f;

        private readonly List<MultiplayerSessionView> _views = new List<MultiplayerSessionView>();
        private readonly List<MultiplayerSessionView> _previousViews = new List<MultiplayerSessionView>();
        private readonly List<(string Source, IMultiplayerManager Manager)> _found = new List<(string, IMultiplayerManager)>();
        private double _lastRefresh = double.NegativeInfinity;
        private Vector2 _scroll;

        internal IReadOnlyList<MultiplayerSessionView> Views => _views;



        [MenuItem(MenuName)]
        public static void ShowWindow()
        {
            GetWindow<MultiplayerDebuggerWindow>(false, WindowName);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            Refresh();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;

            // The views hold the managers, and through them the game's objects.
            _views.Clear();
            _previousViews.Clear();
            _found.Clear();
        }

        private void HandlePlayModeChanged(PlayModeStateChange change)
        {
            Refresh();
            Repaint();
        }

        // Unity calls it ten times a second while the window is open.
        private void OnInspectorUpdate()
        {
            if (EditorApplication.timeSinceStartup - _lastRefresh >= RefreshIntervalSeconds)
            {
                Refresh();
                Repaint();
            }
        }

        private void Refresh()
        {
            _found.Clear();
            if (EditorApplication.isPlaying)
            {
                FindManagers(_found);
            }

            Refresh(_found, EditorApplication.timeSinceStartup);
        }

        /// <summary>
        /// Captures the given managers. A manager shown before keeps its view, so its message
        /// rates follow on from the last capture.
        /// </summary>
        internal void Refresh(IReadOnlyList<(string Source, IMultiplayerManager Manager)> managers, double now)
        {
            _lastRefresh = now;
            _previousViews.Clear();
            _previousViews.AddRange(_views);
            _views.Clear();

            for (int i = 0; i < managers.Count; i++)
            {
                (string source, IMultiplayerManager manager) = managers[i];
                MultiplayerSessionView view = _previousViews.Find(previous => previous.Manager == manager)
                    ?? new MultiplayerSessionView(source, manager);
                view.Capture(now);
                _views.Add(view);
            }

            _previousViews.Clear();
        }

        /// <summary>
        /// The managers that the running cores hold as singletons, with the core that holds each,
        /// through the lookup that the play-mode tests check.
        /// </summary>
        internal static void FindManagers(List<(string Source, IMultiplayerManager Manager)> results)
        {
            MultiplayerManagerLookup.FindInCores(results);
        }

        #region GUI
        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Multiplayer sessions exist while the game runs. Enter play mode to inspect them.", MessageType.Info);
                return;
            }

            if (_views.Count == 0)
            {
                EditorGUILayout.HelpBox("No core under CoreRoot holds a multiplayer manager. The debugger lists the managers that cores register as singletons.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _views.Count; i++)
            {
                DrawView(_views[i]);
                GUILayout.Space(12f);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawView(MultiplayerSessionView view)
        {
            EditorGUILayout.LabelField($"[ {view.Source} ]  {view.State}", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                string role = view.State == SessionState.Offline ? "-" : view.IsHost ? "Host" : "Client";
                EditorGUILayout.LabelField("Role", role);
                EditorGUILayout.LabelField("Local player", view.LocalPlayerId.ToString());
                EditorGUILayout.LabelField("Lobby", view.Lobby.IsValid ? view.Lobby.ToString() : "-");
                EditorGUILayout.LabelField("Players", view.State == SessionState.Offline
                    ? "-"
                    : string.Format(CultureInfo.InvariantCulture, "{0} / {1}", view.Players.Count, view.Rules.Capacity));
            }

            if (view.Players.Count > 0)
            {
                GUILayout.Space(4f);
                DrawRow(EditorStyles.boldLabel, "Player", "Ping", "Loss", "Sent/s", "Recv/s");
                for (int i = 0; i < view.Players.Count; i++)
                {
                    MultiplayerSessionView.PlayerRow row = view.Players[i];
                    Participant participant = row.Participant;
                    string name = Describe(participant);
                    if (row.HasQuality)
                    {
                        ConnectionQuality quality = row.Quality;
                        DrawRow(EditorStyles.label, name,
                            string.Format(CultureInfo.InvariantCulture, "{0} ms", quality.PingMilliseconds),
                            string.Format(CultureInfo.InvariantCulture, "{0:0.#}%", quality.PacketLoss * 100f),
                            Bytes(quality.BytesSentPerSecond),
                            Bytes(quality.BytesReceivedPerSecond));
                    }
                    else
                    {
                        // The host's own row, and on a client the other clients, whom it reaches through the host.
                        DrawRow(EditorStyles.label, name, "-", "-", "-", "-");
                    }
                }
            }

            if (view.Messages.Count > 0)
            {
                GUILayout.Space(4f);
                DrawRow(EditorStyles.boldLabel, "Message type", "Sent", "Received", "Sent/s", "Recv/s");
                for (int i = 0; i < view.Messages.Count; i++)
                {
                    MultiplayerSessionView.MessageRow row = view.Messages[i];
                    DrawRow(EditorStyles.label,
                        row.Counts.MessageType.ToString(CultureInfo.InvariantCulture),
                        row.Counts.Sent.ToString(CultureInfo.InvariantCulture),
                        row.Counts.Received.ToString(CultureInfo.InvariantCulture),
                        row.SentPerSecond.ToString("0.#", CultureInfo.InvariantCulture),
                        row.ReceivedPerSecond.ToString("0.#", CultureInfo.InvariantCulture));
                }
            }
        }

        private static void DrawRow(GUIStyle style, string first, params string[] numbers)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(first, style, GUILayout.MinWidth(120f), GUILayout.ExpandWidth(true));
                for (int i = 0; i < numbers.Length; i++)
                {
                    GUILayout.Label(numbers[i], style, GUILayout.Width(NumberWidth));
                }
            }
        }

        /// <summary>The player's name and ID, then what the session says of them: host, this player, ready, late.</summary>
        internal static string Describe(Participant participant)
        {
            List<string> flags = new List<string>(4);
            if (participant.IsHost)
            {
                flags.Add("host");
            }

            if (participant.IsLocal)
            {
                flags.Add("you");
            }

            if (participant.IsReady)
            {
                flags.Add("ready");
            }

            if (participant.IsLateJoiner)
            {
                flags.Add("late");
            }

            string name = $"{participant.Name} ({participant.Id})";
            return flags.Count > 0 ? $"{name} [{string.Join(", ", flags)}]" : name;
        }

        private static string Bytes(float bytesPerSecond)
        {
            return bytesPerSecond >= 1024f
                ? string.Format(CultureInfo.InvariantCulture, "{0:0.0} KB", bytesPerSecond / 1024f)
                : string.Format(CultureInfo.InvariantCulture, "{0:0} B", bytesPerSecond);
        }
        #endregion
    }
}
