using System.Runtime.CompilerServices;

// The edit-mode tests drive the Observable drawers' target lookup, the Mu3Window drawer list
// lookup, the link.xml that the DI linker processor writes, and the DI validator's checks of a
// core and its registrations, which stay internal so the editor keeps its public surface.
[assembly: InternalsVisibleTo("Mu3Library.Tests.Editor")]
