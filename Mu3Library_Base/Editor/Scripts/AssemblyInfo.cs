using System.Runtime.CompilerServices;

// The edit-mode tests drive the Observable drawers' target lookup and the link.xml that the DI
// linker processor writes, which stay internal so the editor keeps its public surface.
[assembly: InternalsVisibleTo("Mu3Library.Tests.Editor")]
