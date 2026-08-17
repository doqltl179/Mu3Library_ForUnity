using System.Runtime.CompilerServices;

// The edit-mode tests exercise internal surfaces such as Container.CreateScope, which stays
// internal so a core remains the only scope owner in shipped code.
[assembly: InternalsVisibleTo("Mu3Library.Tests.Editor")]
