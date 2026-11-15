using System.Runtime.CompilerServices;

// The edit-mode tests drive the Observable drawers' target lookup and the Mu3Window drawer list
// lookup, which stay internal so the editor keeps its public surface.
[assembly: InternalsVisibleTo("Mu3Library.Tests.Editor")]
