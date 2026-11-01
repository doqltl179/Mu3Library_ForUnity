using System.Runtime.CompilerServices;

// The edit-mode tests drive the Observable drawers' target lookup, the Mu3Window drawer list
// lookup, the link.xml that the DI linker processor writes, and the DI validator's check of
// [Inject] properties without a setter, which stay internal so the editor keeps its public surface.
[assembly: InternalsVisibleTo("Mu3Library.Tests.Editor")]
