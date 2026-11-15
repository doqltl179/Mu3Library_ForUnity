using System.Runtime.CompilerServices;

// The edit-mode tests exercise internal surfaces such as Container.CreateScope, which stays
// internal so a core remains the only scope owner in shipped code. The play-mode tests watch a
// core's OnPrepared event and mark the shutdown that Application.quitting marks.
[assembly: InternalsVisibleTo("Mu3Library.Tests.Editor")]
[assembly: InternalsVisibleTo("Mu3Library.Tests.Runtime")]
