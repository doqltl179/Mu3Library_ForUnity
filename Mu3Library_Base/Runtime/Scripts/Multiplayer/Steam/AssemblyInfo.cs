using System.Runtime.CompilerServices;

// The edit-mode tests check how Steam's answers map to the library's reasons, which stay
// internal so the backend's public surface is only what a game uses.
[assembly: InternalsVisibleTo("Mu3Library.Tests.Editor")]
