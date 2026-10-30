using System.Runtime.CompilerServices;

// The edit-mode tests reach internals that shipped code must not: the local network's lobbies
// and its switch past the lobby capacity check, and the protocol (reserved lobby keys, packets,
// and close reason codes) to act as a misbehaving player.
[assembly: InternalsVisibleTo("Mu3Library.Tests.Editor")]
