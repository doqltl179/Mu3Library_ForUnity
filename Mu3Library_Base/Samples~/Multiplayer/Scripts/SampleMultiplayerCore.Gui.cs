using System.Collections.Generic;
using System.Globalization;
using Mu3Library.Multiplayer.Data;
using UnityEngine;

namespace Mu3Library.Sample.Multiplayer
{
    /// <summary>
    /// The sample's buttons, drawn with IMGUI. IMGUI needs no <c>EventSystem</c>, no input module,
    /// and no TextMesh Pro resources, so the scene responds to input with or without the Input
    /// System package and draws its text in a player as it does in the editor.
    /// </summary>
    public partial class SampleMultiplayerCore
    {
        private const float ReferenceHeight = 720f;
        private const float PanelWidth = 460f;
        private const float Margin = 12f;
        private const float LabelWidth = 90f;

        private string _lobbyNameText = string.Empty;
        private string _lobbyIdText = string.Empty;
        private Vector2 _panelScroll;



        private void OnGUI()
        {
            // Scale everything up on tall screens, so the buttons stay readable.
            float scale = Mathf.Max(1f, Screen.height / ReferenceHeight);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            Rect panelRect;
            Rect logRect;
            if (width >= PanelWidth * 2f + Margin * 3f)
            {
                panelRect = new Rect(Margin, Margin, PanelWidth, height - Margin * 2f);
                logRect = new Rect(panelRect.xMax + Margin, Margin, width - panelRect.xMax - Margin * 2f, height - Margin * 2f);
            }
            else
            {
                panelRect = new Rect(Margin, Margin, width - Margin * 2f, (height - Margin * 3f) * 0.6f);
                logRect = new Rect(Margin, panelRect.yMax + Margin, panelRect.width, height - panelRect.yMax - Margin * 2f);
            }

            GUILayout.BeginArea(panelRect, GUI.skin.box);
            _panelScroll = GUILayout.BeginScrollView(_panelScroll);
            DrawPanel();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUILayout.BeginArea(logRect, GUI.skin.box);
            GUILayout.Label("Log");
            IReadOnlyList<string> lines = _log.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                GUILayout.Label(lines[i]);
            }
            GUILayout.EndArea();
        }

        private void DrawPanel()
        {
            GUILayout.Label("Mu3Library Multiplayer Sample");

            if (_manager == null || !_manager.IsInitialized)
            {
                GUILayout.Label("The multiplayer manager did not start. The log tells why.");
                return;
            }

            GUILayout.Label($"Backend: {_backend.Backend.Name}    Player: {LocalName}");
            GUILayout.Label($"State: {_manager.State}{(_manager.IsHost ? " (host)" : string.Empty)}");
            if (_commandLine.AutoRole != SampleAutoRole.None)
            {
                GUILayout.Label($"Automatic run as {_commandLine.AutoRole}: {AutoRunStatus}");
            }

            GUILayout.Space(8f);
            switch (_manager.State)
            {
                case SessionState.Offline:
                    DrawOffline();
                    break;
                case SessionState.Lobby:
                case SessionState.InGame:
                    DrawSession(_manager.State == SessionState.InGame);
                    break;
                default:
                    GUILayout.Label("Please wait...");
                    if (GUILayout.Button("Leave"))
                    {
                        Leave();
                    }
                    break;
            }
        }

        private void DrawOffline()
        {
            GUILayout.Label("Host a lobby");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Lobby name", GUILayout.Width(LabelWidth));
            _lobbyNameText = GUILayout.TextField(_lobbyNameText, LobbyCreateOptions.MaxNameLength);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Create a public lobby"))
            {
                string lobbyName = _lobbyNameText.Trim();
                CreateLobby(lobbyName.Length > 0 ? lobbyName : $"{LocalName}'s lobby");
            }

            GUILayout.Space(8f);
            GUILayout.Label("Join a lobby");
            GUI.enabled = !_isSearching;
            if (GUILayout.Button(_isSearching ? "Searching..." : "Search lobbies"))
            {
                SearchLobbies();
            }
            GUI.enabled = true;

            for (int i = 0; i < _foundLobbies.Count; i++)
            {
                LobbyInfo lobby = _foundLobbies[i];
                string inGame = lobby.IsInGame ? ", in game" : string.Empty;
                if (GUILayout.Button($"Join \"{lobby.Name}\" ({lobby.MemberCount}/{lobby.Capacity}{inGame}), ID {lobby.Id}"))
                {
                    JoinLobby(lobby.Id);
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Lobby ID", GUILayout.Width(LabelWidth));
            _lobbyIdText = GUILayout.TextField(_lobbyIdText, 20);
            bool isLobbyId = ulong.TryParse(_lobbyIdText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ulong lobbyId) && lobbyId != 0;
            GUI.enabled = isLobbyId;
            if (GUILayout.Button("Join by ID", GUILayout.Width(100f)))
            {
                JoinLobby(new LobbyId(lobbyId));
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label(_backend.SupportsInvites
                ? "A friend's Steam invite joins by itself once accepted."
                : "The local backend has no invites: the host copies its lobby ID, and the other player joins by ID.");
        }

        private void DrawSession(bool isInGame)
        {
            GUILayout.Label($"Lobby ID: {_manager.CurrentLobbyId}");
            if (_backend.SupportsInvites)
            {
                if (GUILayout.Button("Invite friends (Steam overlay)"))
                {
                    OpenInviteDialog();
                }
            }
            else if (GUILayout.Button("Copy the lobby ID"))
            {
                GUIUtility.systemCopyBuffer = _manager.CurrentLobbyId.ToString();
                _log.Info("Copied the lobby ID. Paste it into the Lobby ID field of another instance to join.");
            }

            GUILayout.Space(8f);
            IReadOnlyList<Participant> participants = _manager.Participants;
            GUILayout.Label($"Players {participants.Count}/{_manager.Rules.Capacity}");

            // Kicking changes the roster at once, so it waits until the roster is drawn.
            PlayerId kickTarget = PlayerId.None;
            for (int i = 0; i < participants.Count; i++)
            {
                Participant participant = participants[i];
                string host = participant.IsHost ? " [host]" : string.Empty;
                string you = participant.IsLocal ? " [you]" : string.Empty;
                string ready = isInGame ? string.Empty : participant.IsReady ? "  ready" : "  not ready";
                string away = participant.IsConnected ? string.Empty : "  away";

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{participant.Name}{host}{you}{ready}{away}");
                if (_manager.IsHost && !participant.IsLocal && GUILayout.Button("Kick", GUILayout.Width(60f)))
                {
                    kickTarget = participant.Id;
                }
                GUILayout.EndHorizontal();
            }

            if (kickTarget.IsValid)
            {
                Kick(kickTarget);
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("Send a message to the others"))
            {
                SendGreeting(MessageTarget.Others);
            }

            if (!isInGame)
            {
                bool isReady = _manager.TryGetParticipant(_manager.LocalPlayerId, out Participant local) && local.IsReady;
                if (GUILayout.Button(isReady ? "Not ready" : "Ready"))
                {
                    SetReady(!isReady);
                }

                if (_manager.IsHost && GUILayout.Button("Start the game"))
                {
                    StartGame();
                }
            }
            else if (_manager.IsHost && GUILayout.Button("End the game"))
            {
                EndGame();
            }

            if (GUILayout.Button("Leave"))
            {
                Leave();
            }
        }
    }
}
