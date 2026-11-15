using System.Runtime.CompilerServices;

// The edit-mode tests reach internals that shipped code must not: the local network's lobbies
// and its switch past the lobby capacity check, and the protocol (packets and close reason
// codes) to act as a misbehaving player.
[assembly: InternalsVisibleTo("Mu3Library.Tests.Editor")]

// The editor's Multiplayer Debugger window reads the managers from MultiplayerManagerRegistry.
[assembly: InternalsVisibleTo("Mu3Library.Editor")]
