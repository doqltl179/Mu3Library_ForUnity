using System.Runtime.CompilerServices;

// The edit-mode tests drive the Observable drawers' target lookup and the Mu3Window drawer list
// lookup, which stay internal so the drawers and the window keep their public surface.
[assembly: InternalsVisibleTo("Mu3Library.Tests.Editor")]
