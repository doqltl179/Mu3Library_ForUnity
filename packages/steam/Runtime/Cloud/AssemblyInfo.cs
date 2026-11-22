using System.Runtime.CompilerServices;

// The edit-mode tests hand the storage a stand-in for Steam Remote Storage, which stays internal
// so the storage's public surface is only what a game uses.
[assembly: InternalsVisibleTo("Mu3Library.Steam.Tests.Editor")]
