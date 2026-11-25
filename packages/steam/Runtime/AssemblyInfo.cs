using System.Runtime.CompilerServices;

// The package's other Steam assemblies start Steam and read Steam's exceptions through SteamCalls,
// and the edit-mode tests hand SteamManager a stand-in for Steam; all of it stays internal so the
// assembly's public surface is only what a game uses.
[assembly: InternalsVisibleTo("Mu3Library.Steam.Multiplayer")]
[assembly: InternalsVisibleTo("Mu3Library.Steam.Stats")]
[assembly: InternalsVisibleTo("Mu3Library.Steam.Cloud")]
[assembly: InternalsVisibleTo("Mu3Library.Steam.Tests.Editor")]
