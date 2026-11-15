using System.Runtime.CompilerServices;

// The edit-mode tests reach internals that shipped code must not: the local network's lobbies
// and its switch past the lobby capacity check, and the protocol (reserved lobby keys, packets,
// and close reason codes) to act as a misbehaving player.
[assembly: InternalsVisibleTo("Mu3Library.Tests.Editor")]

// The Steam backend writes the library's reserved lobby keys and snapshots search queries the
// same way the local backend does; it ships with this assembly, only behind its own gate.
[assembly: InternalsVisibleTo("Mu3Library.Multiplayer.Steam")]

// The editor's Multiplayer Debugger window finds the managers through MultiplayerManagerLookup,
// which the play-mode tests check in play mode, where editor code is out of their reach.
[assembly: InternalsVisibleTo("Mu3Library.Editor")]
[assembly: InternalsVisibleTo("Mu3Library.Tests.Runtime")]
